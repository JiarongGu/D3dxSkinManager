using System;
using System.Collections.Generic;
using System.IO;
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
using D3dxSkinManager.Modules.Plugin.Models;
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
        Build(downloads, registry, LoaderWith(), Mock.Of<IProfilePathService>());

    private static PluginInstallService Build(Mock<IDownloadService> downloads, IPluginRegistry registry,
        IPluginLoader loader, IProfilePathService paths) =>
        new(downloads.Object, Mock.Of<IProcessRegistry>(), paths,
            loader, registry, new ReleaseEndpointConfig((EndpointConfig?)null), Mock.Of<ILogHelper>());

    private static IPluginLoader LoaderWith(params PluginLoadFailure[] failures)
    {
        var m = new Mock<IPluginLoader>();
        m.Setup(l => l.LoadFailures).Returns(failures);
        return m.Object;
    }

    private static string ContractManifest(string id, string version, string contract, string name) =>
        $$"""{ "plugins": [ { "id": "{{id}}", "name": "{{name}}", "version": "{{version}}", "asset": "x.zip", "sdkContractVersion": "{{contract}}" } ] }""";

    private static PluginLoadFailure Failure(string packId) =>
        new() { PackId = packId, DllName = packId + ".dll", Reason = "Core contract mismatch" };

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
        // The compatible pack's sdkContractVersion major MUST track PluginContract.Version (currently
        // "2.0" — the v2 bool-verdict contract); a matching major = compatible, a different one = gated out.
        const string manifest = """
        { "plugins": [
            { "id": "content-veil-ai", "name": "Content Veil AI", "description": "detector",
              "version": "1.1", "asset": "ContentVeil-AI-Plugin.zip", "sdkContractVersion": "2.0" },
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
        veil.Compatible.Should().BeTrue("SDK contract 2.0 matches the host major");
        veil.Installed.Should().BeTrue("it's registered in this profile");

        var future = packs.Single(p => p.Id == "future-pack");
        future.Compatible.Should().BeFalse("contract major 9 != the host major");
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

    // ---- GetLoadFailuresAsync: surface installed-but-failed packs, enriched with catalog fixability ----

    [Fact]
    public async Task GetLoadFailures_CompatibleBuildInCatalog_MarksUpdateAvailable()
    {
        var loader = LoaderWith(Failure("content-veil-ai"));
        var svc = Build(Downloads(ReleaseJson(), ContractManifest("content-veil-ai", "1.1", "2.0", "Content Veil AI")),
            new PluginRegistry(Mock.Of<ILogHelper>()), loader, Mock.Of<IProfilePathService>());

        var failures = await svc.GetLoadFailuresAsync();

        var f = failures.Should().ContainSingle().Subject;
        f.PackId.Should().Be("content-veil-ai");
        f.Reason.Should().Be("Core contract mismatch");
        f.Name.Should().Be("Content Veil AI");
        f.UpdateAvailable.Should().BeTrue("a compatible newer build exists to fix it");
        f.AvailableVersion.Should().Be("1.1");
    }

    [Fact]
    public async Task GetLoadFailures_IncompatibleCatalogBuild_NoUpdateOffered()
    {
        // The catalog has the pack but only an INCOMPATIBLE build (contract major 9) — nothing to download yet.
        var loader = LoaderWith(Failure("content-veil-ai"));
        var svc = Build(Downloads(ReleaseJson(), ContractManifest("content-veil-ai", "9.0", "9.0", "Content Veil AI")),
            new PluginRegistry(Mock.Of<ILogHelper>()), loader, Mock.Of<IProfilePathService>());

        var f = (await svc.GetLoadFailuresAsync()).Should().ContainSingle().Subject;
        f.Name.Should().Be("Content Veil AI");
        f.UpdateAvailable.Should().BeFalse();
        f.AvailableVersion.Should().BeNull();
    }

    [Fact]
    public async Task GetLoadFailures_PackNotInCatalog_ReturnedUnenriched()
    {
        var loader = LoaderWith(Failure("unknown-pack"));
        var svc = Build(Downloads(ReleaseJson(), ContractManifest("content-veil-ai", "1.1", "2.0", "Veil")),
            new PluginRegistry(Mock.Of<ILogHelper>()), loader, Mock.Of<IProfilePathService>());

        var f = (await svc.GetLoadFailuresAsync()).Should().ContainSingle().Subject;
        f.PackId.Should().Be("unknown-pack");
        f.Name.Should().BeNull();
        f.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetLoadFailures_NoFailures_ReturnsEmpty_WithoutFetching()
    {
        var d = Downloads(ReleaseJson(), ContractManifest("content-veil-ai", "1.1", "2.0", "Veil"));
        var svc = Build(d, new PluginRegistry(Mock.Of<ILogHelper>()), LoaderWith(), Mock.Of<IProfilePathService>());

        (await svc.GetLoadFailuresAsync()).Should().BeEmpty();
        d.Verify(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLoadFailures_NetworkFailure_ReturnsRawFailuresUnenriched()
    {
        var d = new Mock<IDownloadService>();
        d.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("offline"));
        var svc = Build(d, new PluginRegistry(Mock.Of<ILogHelper>()), LoaderWith(Failure("content-veil-ai")), Mock.Of<IProfilePathService>());

        var f = (await svc.GetLoadFailuresAsync()).Should().ContainSingle().Subject;
        f.PackId.Should().Be("content-veil-ai", "the failure is still surfaced even when the catalog is unreachable");
        f.UpdateAvailable.Should().BeFalse();
        f.Name.Should().BeNull();
    }

    // ---- GetPendingUpdates: pack ids staged in {plugins}/.pending awaiting a restart ----

    [Fact]
    public void GetPendingUpdates_ListsStagedPackDirs()
    {
        var pluginsDir = Path.Combine(Path.GetTempPath(), "d3dx-plugintest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pending = Path.Combine(pluginsDir, PluginLoader.PendingDirName);
            Directory.CreateDirectory(Path.Combine(pending, "content-veil-ai"));
            Directory.CreateDirectory(Path.Combine(pending, "another-pack"));
            var paths = new Mock<IProfilePathService>();
            paths.Setup(p => p.PluginsDirectory).Returns(pluginsDir);
            var svc = Build(new Mock<IDownloadService>(), new PluginRegistry(Mock.Of<ILogHelper>()), LoaderWith(), paths.Object);

            svc.GetPendingUpdates().Should().BeEquivalentTo("content-veil-ai", "another-pack");
        }
        finally { Directory.Delete(pluginsDir, recursive: true); }
    }

    [Fact]
    public void GetPendingUpdates_NoPendingDir_ReturnsEmpty()
    {
        var pluginsDir = Path.Combine(Path.GetTempPath(), "d3dx-plugintest-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.PluginsDirectory).Returns(pluginsDir); // never created
        var svc = Build(new Mock<IDownloadService>(), new PluginRegistry(Mock.Of<ILogHelper>()), LoaderWith(), paths.Object);

        svc.GetPendingUpdates().Should().BeEmpty();
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
