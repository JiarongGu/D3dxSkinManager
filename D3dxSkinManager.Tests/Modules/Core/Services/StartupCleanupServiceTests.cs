using System;
using System.Collections.Generic;
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
/// The CENTRALIZED startup cleanup/migration pipeline: the runner executes every registered
/// IStartupCleanupStep in order, each isolated (one failure never blocks the rest). Steps covered:
/// stale managed downloads, orphaned update staging (kept when a complete pending update exists),
/// and the legacy process-state.json removal (registry is in-memory; profile DBs hold checkpoints).
/// </summary>
public class StartupCleanupServiceTests : IDisposable
{
    private readonly string _installDir;
    private readonly string _dataDir;
    private readonly Mock<IDownloadService> _download = new();
    private readonly StartupCleanupService _service;

    public StartupCleanupServiceTests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), "d3dx-startup-" + Guid.NewGuid().ToString("N"));
        _dataDir = Path.Combine(_installDir, "data");
        Directory.CreateDirectory(_installDir);
        Directory.CreateDirectory(_dataDir);

        _download.Setup(d => d.CleanupManaged(It.IsAny<TimeSpan?>()))
            .Returns(new DownloadCleanupResult { DeletedCount = 0, BytesFreed = 0 });

        var appEnv = new Mock<IAppEnvironment>();
        appEnv.Setup(e => e.BaseDirectory).Returns(_installDir);
        var globalPaths = new Mock<IGlobalPathService>();
        globalPaths.Setup(p => p.BaseDataPath).Returns(_dataDir);

        var logger = new Mock<ILogHelper>().Object;
        _service = new StartupCleanupService(new IStartupCleanupStep[]
        {
            new ManagedDownloadsCleanupStep(_download.Object, logger),
            new OrphanedUpdateStagingCleanupStep(appEnv.Object, logger),
            new LegacyProcessStateCleanupStep(globalPaths.Object, logger),
        }, logger);
    }

    [Fact]
    public async Task RunAsync_ExecutesAllSteps_CleansDownloads()
    {
        await _service.RunAsync();

        _download.Verify(d => d.CleanupManaged(It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AFailingStep_NeverBlocksTheRest()
    {
        _download.Setup(d => d.CleanupManaged(It.IsAny<TimeSpan?>())).Throws(new IOException("disk gone"));
        var legacy = Path.Combine(_dataDir, "process-state.json");
        File.WriteAllText(legacy, "[]");

        await _service.RunAsync(); // must not throw

        File.Exists(legacy).Should().BeFalse("later steps still run after an earlier step fails");
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

    [Fact]
    public async Task RunAsync_RemovesLegacyProcessStateFile()
    {
        var legacy = Path.Combine(_dataDir, "process-state.json");
        File.WriteAllText(legacy, "[{\"Id\":\"x\"}]");

        await _service.RunAsync();

        File.Exists(legacy).Should().BeFalse("the registry is in-memory; profile DBs hold resumable checkpoints");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_installDir)) Directory.Delete(_installDir, true); } catch { }
    }
}
