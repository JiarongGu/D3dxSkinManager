using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// PluginInstallService.CheckUpdatesAsync — compares each INSTALLED official pack's version against
/// the version advertised by the latest release's public plugins-manifest.json. Drives an Update
/// badge/button on the UI only when a NEWER version exists. Network failure is non-fatal (empty list).
/// </summary>
public class PluginInstallServiceTests
{
    private const string ReleaseApi = "https://api.github.com/repos/JiarongGu/D3dxSkinManager.Plugins/releases/latest";
    private const string ManifestUrl = "https://github.com/JiarongGu/D3dxSkinManager.Plugins/releases/download/v1.1/plugins-manifest.json";

    private static string ReleaseJson() =>
        $$"""{ "assets": [ { "name": "plugins-manifest.json", "browser_download_url": "{{ManifestUrl}}" } ] }""";

    private static string ManifestJson(string version) =>
        $$"""{ "plugins": [ { "id": "content-veil-ai", "version": "{{version}}" } ] }""";

    private static PluginInstallService Build(Mock<IDownloadService> downloads, IPluginRegistry registry) =>
        new(downloads.Object, Mock.Of<IProcessRegistry>(), Mock.Of<IProfilePathService>(),
            Mock.Of<IPluginLoader>(), registry, Mock.Of<ILogHelper>());

    private static Mock<IDownloadService> Downloads(string releaseJson, string manifestJson)
    {
        var d = new Mock<IDownloadService>();
        d.Setup(x => x.GetStringAsync(ReleaseApi, It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(releaseJson);
        d.Setup(x => x.GetStringAsync(ManifestUrl, It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifestJson);
        return d;
    }

    private static PluginRegistry RegistryWith(string version)
    {
        var reg = new PluginRegistry(Mock.Of<ILogHelper>());
        reg.RegisterPlugin(new StubPlugin { Id = "d3dx.content-veil-ai", Version = version });
        return reg;
    }

    [Fact]
    public async Task NewerAvailable_MarksUpdateAvailable()
    {
        var svc = Build(Downloads(ReleaseJson(), ManifestJson("1.1")), RegistryWith("1.0"));

        var updates = await svc.CheckUpdatesAsync();

        var u = updates.Should().ContainSingle().Subject;
        u.PluginId.Should().Be("d3dx.content-veil-ai");
        u.PackId.Should().Be("content-veil-ai");
        u.InstalledVersion.Should().Be("1.0");
        u.AvailableVersion.Should().Be("1.1");
        u.UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task SameVersion_NoUpdate()
    {
        var svc = Build(Downloads(ReleaseJson(), ManifestJson("1.0")), RegistryWith("1.0"));

        var updates = await svc.CheckUpdatesAsync();

        updates.Should().ContainSingle().Which.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task NetworkFailure_ReturnsEmpty_NotThrow()
    {
        var d = new Mock<IDownloadService>();
        d.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("offline"));
        var svc = Build(d, RegistryWith("1.0"));

        (await svc.CheckUpdatesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NoInstalledPacks_ReturnsEmpty_WithoutFetching()
    {
        var d = Downloads(ReleaseJson(), ManifestJson("1.1"));
        var svc = Build(d, new PluginRegistry(Mock.Of<ILogHelper>())); // nothing installed

        (await svc.CheckUpdatesAsync()).Should().BeEmpty();
        d.Verify(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailablePacks_ReadsManifest_MarksCompatibilityAndInstalled()
    {
        const string manifest = """
        { "plugins": [
            { "id": "content-veil-ai", "name": "Content Veil AI", "description": "detector",
              "version": "1.1", "asset": "ContentVeil-AI-Plugin.zip", "sdkContractVersion": "1.0" },
            { "id": "future-pack", "name": "Future", "version": "2.0", "asset": "future.zip",
              "sdkContractVersion": "9.0" }
          ] }
        """;
        var svc = Build(Downloads(ReleaseJson(), manifest), RegistryWith("1.0")); // content-veil-ai installed

        var packs = await svc.GetAvailablePacksAsync();

        packs.Should().HaveCount(2);
        var veil = packs.Single(p => p.Id == "content-veil-ai");
        veil.Name.Should().Be("Content Veil AI");
        veil.Asset.Should().Be("ContentVeil-AI-Plugin.zip");
        veil.Compatible.Should().BeTrue("SDK contract 1.0 matches the host major");
        veil.Installed.Should().BeTrue("it's registered in this profile");

        var future = packs.Single(p => p.Id == "future-pack");
        future.Compatible.Should().BeFalse("contract major 9 != host major 1");
        future.Installed.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailablePacks_NetworkFailure_ReturnsEmpty()
    {
        var d = new Mock<IDownloadService>();
        d.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("offline"));
        (await Build(d, RegistryWith("1.0")).GetAvailablePacksAsync()).Should().BeEmpty();
    }

    private sealed class StubPlugin : IPlugin
    {
        public string Id { get; init; } = "stub";
        public string Name => "Stub";
        public string Version { get; init; } = "1.0";
        public string Description => string.Empty;
        public string Author => string.Empty;
        public Task InitAsync(IPluginContext context) => Task.CompletedTask;
        public IEnumerable<string> GetHandledMessageTypes() => Array.Empty<string>();
        public Task<IpcResponse> HandleMessageAsync(IpcRequest request) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
