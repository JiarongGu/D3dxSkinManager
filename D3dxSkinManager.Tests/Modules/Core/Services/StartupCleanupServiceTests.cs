using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// Tests for the startup self-cleanup: stale managed downloads, orphaned update staging (kept when a
/// complete pending update exists), and stale process purge. Each step is mocked except the staging
/// dir, which is exercised against a real temp install dir.
/// </summary>
public class StartupCleanupServiceTests : IDisposable
{
    private readonly string _installDir;
    private readonly Mock<IDownloadService> _download = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly StartupCleanupService _service;

    public StartupCleanupServiceTests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), "d3dx-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_installDir);

        _download.Setup(d => d.CleanupManaged(It.IsAny<TimeSpan?>()))
            .Returns(new DownloadCleanupResult { DeletedCount = 0, BytesFreed = 0 });

        var appEnv = new Mock<IAppEnvironment>();
        appEnv.Setup(e => e.BaseDirectory).Returns(_installDir);

        _service = new StartupCleanupService(
            _download.Object, appEnv.Object, _registry.Object, new Mock<ILogHelper>().Object);
    }

    [Fact]
    public async Task RunAsync_CleansDownloads_AndPurgesProcesses()
    {
        await _service.RunAsync();

        _download.Verify(d => d.CleanupManaged(It.IsAny<TimeSpan?>()), Times.Once);
        _registry.Verify(r => r.PurgeStaleProcesses(), Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletesOrphanedStaging_WhenNoReadyMarker()
    {
        var staging = Path.Combine(_installDir, ".update");
        Directory.CreateDirectory(Path.Combine(staging, "staged"));
        File.WriteAllText(Path.Combine(staging, "staged", "x.bin"), "partial");

        await _service.RunAsync();

        Directory.Exists(staging).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_KeepsStaging_WhenReadyMarkerPresent()
    {
        var staging = Path.Combine(_installDir, ".update");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "ready.json"), "{\"version\":\"2.5\"}");

        await _service.RunAsync();

        Directory.Exists(staging).Should().BeTrue(); // a complete pending update is left for the launcher
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { }
    }
}
