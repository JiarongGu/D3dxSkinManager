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

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Seeder + load behaviour of the remote source adapter store: shipped adapters
/// ({data}/remote-source-seeds) are copied in only when their id has no config yet, so user edits
/// are never overwritten. Temp dirs; no real data.
/// </summary>
public class RemoteSourceStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _seedsDir;
    private readonly RemoteSourceStore _store;

    public RemoteSourceStoreTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "d3dx-remote-test-" + Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(root, "remote-sources");
        _seedsDir = Path.Combine(root, "remote-source-seeds");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(_seedsDir);
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.RemoteSourcesDirectory).Returns(_dir);
        paths.Setup(p => p.RemoteSourceSeedsDirectory).Returns(_seedsDir);
        _store = new RemoteSourceStore(paths.Object, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_dir)!, true); } catch { }
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
        // The user edited their copy (different baseUrl) — the seed must NOT clobber it.
        File.WriteAllText(Path.Combine(_dir, "mine.json"),
            """{"id":"huihui","name":"Edited","baseUrl":"https://my-mirror.example"}""");

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.BaseUrl.Should().Be("https://my-mirror.example");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeFalse("the id is already configured");
    }

    [Fact]
    public void GetAll_AddsNewShippedAdapters_NextToExistingConfigs()
    {
        // App update ships a second adapter — it appears without touching the first.
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
    public void ShippedHuihuiSeed_Deserializes_WithIndexPatterns()
    {
        // Sanity over the REAL shipped file (copied to test output via csproj Content).
        var seed = RemoteBrowseServiceTests.LoadHuihuiSeed();
        seed.Id.Should().Be("huihui");
        seed.EntryIdPattern.Should().NotBeNullOrEmpty();
        seed.ImageDatePattern.Should().NotBeNullOrEmpty();
        seed.Resolvers.Should().Contain(r => r.Type == "cloudreve");
    }
}
