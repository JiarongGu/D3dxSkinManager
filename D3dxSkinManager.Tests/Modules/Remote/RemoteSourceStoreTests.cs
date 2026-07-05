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

/// <summary>Seed + load behaviour of the remote source adapter store (temp dir; no real data).</summary>
public class RemoteSourceStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly RemoteSourceStore _store;

    public RemoteSourceStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-remote-test-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.RemoteSourcesDirectory).Returns(_dir);
        _store = new RemoteSourceStore(paths.Object, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void GetAll_SeedsTheBuiltInAdapter_OnFirstRun()
    {
        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.Id.Should().Be("huihui");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeTrue();
        sources[0].Resolvers.Should().Contain(r => r.Type == "cloudreve");
    }

    [Fact]
    public void GetAll_DoesNotReseed_WhenAConfigExists()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "custom.json"),
            """{"id":"custom","name":"Custom","baseUrl":"https://example.com","lists":[]}""");

        var sources = _store.GetAll();

        sources.Should().ContainSingle().Which.Id.Should().Be("custom");
        File.Exists(Path.Combine(_dir, "huihui.json")).Should().BeFalse("seeding only happens into an empty dir");
    }

    [Fact]
    public void GetAll_SkipsMalformedConfigs()
    {
        Directory.CreateDirectory(_dir);
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
}
