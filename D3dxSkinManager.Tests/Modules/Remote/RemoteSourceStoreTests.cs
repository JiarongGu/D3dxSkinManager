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
/// The remote source adapter store: the GLOBAL {data}/remote-sources/*.json files are the editable
/// DEFINITION; the per-profile RemoteSources SQLite table is the runtime store the app reads from, synced
/// on load (JSON changed → upsert; JSON dropped → row removed). Seeding still copies shipped adapters in
/// only when their id has no config yet, so user edits are never overwritten.
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
        _store = new RemoteSourceStore(repo, globalPaths.Object, Mock.Of<ILogHelper>());
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

    private void WriteSeed(string fileName, string id) =>
        File.WriteAllText(Path.Combine(_seedsDir, fileName),
            $$"""{"id":"{{id}}","name":"Seed {{id}}","baseUrl":"https://seed.example"}""");

    [Fact]
    public void GetAll_CopiesShippedSeeds_WhenTheirIdIsNotConfigured()
    {
        WriteSeed("huihui.json", "huihui");

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.Id.Should().Be("huihui");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeTrue();
    }

    [Fact]
    public void GetAll_NeverOverwritesAnExistingConfigWithTheSameId()
    {
        WriteSeed("huihui.json", "huihui");
        File.WriteAllText(Path.Combine(_dir, "mine.json"),
            """{"id":"huihui","name":"Edited","baseUrl":"https://my-mirror.example"}""");

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.BaseUrl.Should().Be("https://my-mirror.example");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeFalse("the id is already configured");
    }

    [Fact]
    public void GetAll_AddsNewShippedAdapters_NextToExistingConfigs()
    {
        File.WriteAllText(Path.Combine(_dir, "huihui.json"),
            """{"id":"huihui","name":"Existing","baseUrl":"https://existing.example"}""");
        WriteSeed("newsite.json", "newsite");

        var sources = _store.GetAll();

        sources.Select(s => s.Id).Should().BeEquivalentTo("huihui", "newsite");
    }

    [Fact]
    public void GetAll_SkipsMalformedConfigs()
    {
        File.WriteAllText(Path.Combine(_dir, "bad.json"), "{not json");
        File.WriteAllText(Path.Combine(_dir, "ok.json"),
            """{"id":"ok","name":"OK","baseUrl":"https://example.com"}""");

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

        // Edit the adapter (bump mtime — same-tick writes must not fool the signature).
        File.WriteAllText(file, """{"id":"site","name":"V2","baseUrl":"https://example.com"}""");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddSeconds(2));

        _store.GetAll().Single().Name.Should().Be("V2", "an edited JSON re-syncs into SQLite (drop-a-file contract)");
    }

    [Fact]
    public void GetAll_ReloadsWhenAFileIsDroppedIn_OrRemoved()
    {
        File.WriteAllText(Path.Combine(_dir, "a.json"),
            """{"id":"a","name":"A","baseUrl":"https://a.example"}""");
        _store.GetAll().Should().ContainSingle();

        File.WriteAllText(Path.Combine(_dir, "b.json"),
            """{"id":"b","name":"B","baseUrl":"https://b.example"}""");
        _store.GetAll().Select(s => s.Id).Should().BeEquivalentTo("a", "b");

        File.Delete(Path.Combine(_dir, "b.json"));
        // Deleting b.json changes the file SET → the signature differs → re-sync (removes b's SQLite row).
        _store.GetAll().Select(s => s.Id).Should().BeEquivalentTo(new[] { "a" }, "a dropped JSON removes its SQLite row");
    }

    [Fact]
    public void Save_PersistsAValidConfig_ById()
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
    public void Delete_RemovesTheConfigFile_ByAdapterId()
    {
        var config = RemoteBrowseServiceTests.LoadHuihuiSeed();
        config.Id = "gone";
        _store.Save(config);

        _store.Delete("gone").Should().BeTrue();
        Directory.GetFiles(_dir, "*.json").Should().BeEmpty();
        _store.Delete("gone").Should().BeFalse();
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
