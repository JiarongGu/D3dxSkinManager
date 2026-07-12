using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// The remote source adapter store — 2-tier (remote-library-redesign.md): the SHIPPED res seed is the
/// BASE; a {data}/remote-sources overlay overrides it per-field (SPARSE, so res updates to untouched
/// fields flow through); a data file with no matching res is a full CUSTOM source. Effective configs
/// mirror into the per-profile RemoteSources SQLite table. Save writes only the sparse diff vs res.
/// </summary>
public class RemoteSourceStoreTests : InMemoryDatabaseTestBase
{
    private readonly string _root;
    private readonly string _dir;
    private readonly string _seedsDir;
    private readonly RemoteSourceStore _store;

    public RemoteSourceStoreTests()
    {
        CreateRemoteSourcesTable();

        _root = Path.Combine(Path.GetTempPath(), "d3dx-remote-test-" + Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(_root, "remote-sources");
        _seedsDir = Path.Combine(_root, "remote-source-seeds");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(_seedsDir);

        var globalPaths = new Mock<IGlobalPathService>();
        globalPaths.Setup(p => p.RemoteSourcesDirectory).Returns(_dir);
        globalPaths.Setup(p => p.RemoteSourceSeedsDirectory).Returns(_seedsDir);

        var repo = new RemoteSourceRepository(MockProfilePathService.Object);
        _store = new RemoteSourceStore(repo, new RemoteSourceResolver(), globalPaths.Object, Mock.Of<ILogHelper>());
    }

    private void CreateRemoteSourcesTable()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE RemoteSources (Id TEXT PRIMARY KEY NOT NULL, ConfigJson TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    public override void Dispose()
    {
        base.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void WriteSeed(string id, string json) => File.WriteAllText(Path.Combine(_seedsDir, $"{id}.json"), json);
    private void WriteData(string fileName, string json) => File.WriteAllText(Path.Combine(_dir, fileName), json);
    private static string Seed(string id, string extra = "") =>
        $$"""{"id":"{{id}}","name":"Seed {{id}}","baseUrl":"https://seed.example"{{extra}}}""";

    [Fact]
    public void GetAll_ResSource_AppearsFromBase_WithoutCopyingToData()
    {
        WriteSeed("huihui", Seed("huihui"));

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.Id.Should().Be("huihui");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeFalse("res loads directly — no copy into data/");
    }

    [Fact]
    public void GetAll_SparseOverlay_OverridesOneField_RestInheritRes()
    {
        WriteSeed("huihui", """{"id":"huihui","name":"Hui站","baseUrl":"https://seed.example","lists":[{"id":"2","name":"ZZZ"}]}""");
        // A SPARSE overlay carries ONLY the overridden key.
        WriteData("huihui.json", """{"id":"huihui","baseUrl":"https://my-mirror.example"}""");

        var s = _store.GetAll().Single();
        s.BaseUrl.Should().Be("https://my-mirror.example", "overlay overrides baseUrl");
        s.Name.Should().Be("Hui站", "unset overlay field inherits res");
        s.Lists.Should().ContainSingle().Which.Id.Should().Be("2", "overlay omits lists → res lists inherited");
    }

    [Fact]
    public void GetAll_SparseOverlay_InheritsResListChanges_IncludingNewGames()
    {
        // Overlay renames the library but does NOT touch lists → new res games flow through live.
        WriteSeed("gamebanana", """{"id":"gamebanana","name":"GameBanana","baseUrl":"https://gamebanana.com","engine":"gamebanana","lists":[{"id":"8552","name":"Genshin Impact"},{"id":"21842","name":"Arknights: Endfield"}]}""");
        WriteData("gamebanana.json", """{"id":"gamebanana","name":"My GB"}""");

        var s = _store.GetAll().Single();
        s.Name.Should().Be("My GB", "overlay overrides name");
        s.Lists.Select(l => l.Id).Should().BeEquivalentTo(new[] { "8552", "21842" }, "res list additions flow into a sparse overlay");
    }

    [Fact]
    public void GetAll_RemovesNoOpOverlay_OnLoad_SoItInheritsRes()
    {
        WriteSeed("huihui", Seed("huihui"));
        // A legacy FULL copy identical to res = a pure seed with no real override.
        WriteData("huihui.json", Seed("huihui"));

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.Id.Should().Be("huihui");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeFalse("a no-op overlay is dropped → the source inherits res");
    }

    [Fact]
    public void GetAll_CustomDataOnlySource_AppearsAsIs()
    {
        WriteData("mine.json", """{"id":"mine","name":"Mine","baseUrl":"https://mine.example"}""");

        _store.GetAll().Should().ContainSingle().Which.BaseUrl.Should().Be("https://mine.example");
    }

    [Fact]
    public void GetAll_SkipsMalformedConfigs()
    {
        WriteData("bad.json", "{not json");
        WriteData("ok.json", """{"id":"ok","name":"OK","baseUrl":"https://example.com"}""");

        _store.GetAll().Should().ContainSingle().Which.Id.Should().Be("ok");
    }

    [Fact]
    public void GetById_UnknownSource_Throws()
    {
        var act = () => _store.GetById("nope");
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_SOURCE_NOT_FOUND");
    }

    [Fact]
    public void GetAll_ReloadsFromSqlite_AndPicksUpAnEditedJson()
    {
        var file = Path.Combine(_dir, "site.json");
        File.WriteAllText(file, """{"id":"site","name":"V1","baseUrl":"https://example.com"}""");
        _store.GetAll().Single().Name.Should().Be("V1");

        File.WriteAllText(file, """{"id":"site","name":"V2","baseUrl":"https://example.com"}""");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(2)); // bump mtime past same-tick

        _store.GetAll().Single().Name.Should().Be("V2", "an edited JSON re-syncs (drop-a-file contract)");
    }

    [Fact]
    public void GetAll_ReSyncsWhenResChanges_SoUpdatesFlow()
    {
        WriteSeed("huihui", """{"id":"huihui","name":"V1","baseUrl":"https://seed.example"}""");
        _store.GetAll().Single().Name.Should().Be("V1");

        var seedFile = Path.Combine(_seedsDir, "huihui.json");
        File.WriteAllText(seedFile, """{"id":"huihui","name":"V2","baseUrl":"https://seed.example"}""");
        File.SetLastWriteTimeUtc(seedFile, DateTime.UtcNow.AddSeconds(2));

        _store.GetAll().Single().Name.Should().Be("V2", "a res change re-syncs (no overlay → inherited)");
    }

    [Fact]
    public void Save_ResBacked_WritesSparseDiff_AndResFieldsStillFlow()
    {
        WriteSeed("gamebanana", """{"id":"gamebanana","name":"GameBanana","baseUrl":"https://gamebanana.com","engine":"gamebanana","lists":[{"id":"8552","name":"Genshin"}]}""");

        var edited = _store.GetById("gamebanana");
        edited.BaseUrl = "https://mirror.example";
        _store.Save(edited);

        var written = File.ReadAllText(Path.Combine(_dir, "gamebanana.json"));
        written.Should().Contain("mirror.example");
        written.Should().NotContain("8552", "unchanged lists are NOT in the sparse overlay (they inherit res)");

        var s = _store.GetById("gamebanana");
        s.BaseUrl.Should().Be("https://mirror.example");
        s.Lists.Should().ContainSingle().Which.Id.Should().Be("8552", "lists still inherit res through the sparse overlay");
    }

    [Fact]
    public void Save_RevertsToMaster_DropsOverlay_AndOriginBecomesDefault()
    {
        WriteSeed("gb", """{"id":"gb","name":"GB","baseUrl":"https://gb.com","engine":"gamebanana","lists":[{"id":"1","name":"G"}]}""");
        WriteData("gb.json", """{"id":"gb","name":"Mine"}"""); // a real override → customized
        _store.GetOrigins()["gb"].Should().Be("customized");

        // Save the config back at its shipped master (the "take all / no difference" case).
        _store.Save(_store.GetDefault("gb")!);

        File.Exists(Path.Combine(_dir, "gb.json")).Should().BeFalse("no real override → the overlay is dropped");
        _store.GetOrigins()["gb"].Should().Be("default", "with no overlay the source reads as default, not modified");
    }

    [Fact]
    public void Save_EmptyStringVsMasterAbsent_IsNotAnOverride_DropsOverlay()
    {
        WriteSeed("s", """{"id":"s","name":"S","baseUrl":"https://s.example","lists":[{"id":"1","name":"G"}],"listUrlFirstPage":"/?l_{list}/","cardPattern":"c","detailTitlePattern":"d","downloadLinkPattern":"x"}""");
        var cfg = _store.GetById("s");
        cfg.SearchUrlTemplate = ""; // a blank the form fills in — master omits it → NOT a real override

        _store.Save(cfg);

        File.Exists(Path.Combine(_dir, "s.json")).Should().BeFalse("empty-string vs absent is not a diff → overlay dropped");
        _store.GetOrigins()["s"].Should().Be("default");
    }

    [Fact]
    public void Save_CustomSource_WritesFullConfig()
    {
        var config = RemoteBrowseServiceTests.LoadHuihuiSeed();
        config.Id = "mysite";

        _store.Save(config);

        File.Exists(Path.Combine(_dir, "mysite.json")).Should().BeTrue();
        _store.GetById("mysite").Name.Should().Be(config.Name);
    }

    [Theory]
    [InlineData("bad id!", "https://ok.example")]
    [InlineData("ok", "not-a-url")]
    public void Save_RejectsInvalidConfigs(string id, string baseUrl)
    {
        var config = RemoteBrowseServiceTests.LoadHuihuiSeed();
        config.Id = id;
        config.BaseUrl = baseUrl;

        var act = () => _store.Save(config);
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_SOURCE_INVALID");
    }

    [Fact]
    public void Save_RejectsNonCompilingRegex()
    {
        var config = RemoteBrowseServiceTests.LoadHuihuiSeed();
        config.CardPattern = "(unclosed";

        var act = () => _store.Save(config);
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_SOURCE_INVALID");
    }

    [Fact]
    public void Delete_RemovesCustomOverlay_ByAdapterId()
    {
        var config = RemoteBrowseServiceTests.LoadHuihuiSeed();
        config.Id = "gone";
        _store.Save(config);

        _store.Delete("gone").Should().BeTrue();
        Directory.GetFiles(_dir, "*.json").Should().BeEmpty();
        _store.Delete("gone").Should().BeFalse();
    }

    [Fact]
    public void Delete_ResBackedOverlay_RevertsToResDefault()
    {
        WriteSeed("huihui", """{"id":"huihui","name":"Default","baseUrl":"https://seed.example"}""");
        WriteData("huihui.json", """{"id":"huihui","name":"Overridden"}""");
        _store.GetById("huihui").Name.Should().Be("Overridden");

        _store.Delete("huihui").Should().BeTrue();

        _store.GetById("huihui").Name.Should().Be("Default", "deleting the overlay reverts to the shipped res source");
    }

    [Fact]
    public void GetDefault_ResBacked_ReturnsResBase_IgnoringOverlay()
    {
        WriteSeed("huihui", """{"id":"huihui","name":"Default","baseUrl":"https://seed.example"}""");
        WriteData("huihui.json", """{"id":"huihui","name":"Overridden","baseUrl":"https://mirror.example"}""");
        _store.GetById("huihui").Name.Should().Be("Overridden", "the effective config has the overlay applied");

        var def = _store.GetDefault("huihui");

        def.Should().NotBeNull();
        def!.Name.Should().Be("Default", "GetDefault returns the shipped res base, not the overlaid effective config");
        def.BaseUrl.Should().Be("https://seed.example");
    }

    [Fact]
    public void GetOrigins_RealOverlay_IsCustomized_NoOpOverlay_IsDefault_DataOnly_IsCustom()
    {
        WriteSeed("real", """{"id":"real","name":"Master","baseUrl":"https://seed.example"}""");
        WriteData("real.json", """{"id":"real","name":"Mine"}""");            // real override
        WriteSeed("noop", """{"id":"noop","name":"Same","baseUrl":"https://seed.example"}""");
        WriteData("noop.json", """{"id":"noop","name":"Same"}""");            // overlay == master
        WriteData("mine.json", """{"id":"mine","name":"Mine","baseUrl":"https://mine.example"}""");

        var origins = _store.GetOrigins();

        origins["real"].Should().Be("customized", "the overlay really differs from master");
        origins["noop"].Should().Be("default", "a sparse overlay that resolves back to master is NOT customized");
        origins["mine"].Should().Be("custom", "a data-only source has no res master");
    }

    [Fact]
    public void GetAll_DropsSparseNoOpOverlay_ThatResolvesBackToMaster()
    {
        WriteSeed("s", """{"id":"s","name":"Same","baseUrl":"https://seed.example"}""");
        WriteData("s.json", """{"id":"s","name":"Same"}"""); // sparse overlay setting name to the SAME value

        _store.GetAll();

        File.Exists(Path.Combine(_dir, "s.json")).Should().BeFalse("a no-op sparse overlay is dropped → refers to master");
        _store.GetOrigins()["s"].Should().Be("default");
    }

    [Fact]
    public void GetDefault_CustomSource_ReturnsNull()
    {
        WriteData("mine.json", """{"id":"mine","name":"Mine","baseUrl":"https://mine.example"}""");
        _store.GetAll(); // materialize

        _store.GetDefault("mine").Should().BeNull("a data-only custom source has no res default to compare against");
    }

    [Fact]
    public void ShippedHuihuiSeed_Deserializes_WithIndexPatterns()
    {
        var seed = RemoteBrowseServiceTests.LoadHuihuiSeed();
        seed.Id.Should().Be("huihui");
        seed.EntryIdPattern.Should().NotBeNullOrEmpty();
        seed.ImageDatePattern.Should().NotBeNullOrEmpty();
        seed.Resolvers.Should().Contain(r => r.Type == "cloudreve");
    }
}
