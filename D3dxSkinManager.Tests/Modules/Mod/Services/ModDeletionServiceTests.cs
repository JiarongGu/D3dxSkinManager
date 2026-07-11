using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for ModDeletionService file-operation serialization.
/// Regression guard: preview-folder deletion must go through the FileOperationPlanner (like cache +
/// archive deletion), never a raw Directory.Delete that races the planner worker.
/// See .claude/knowledge/filesystem-operation-serialization.md
/// </summary>
public class ModDeletionServiceTests : IDisposable
{
    private readonly Mock<IModRepository> _mockRepository = new();
    private readonly Mock<IModCacheService> _mockCache = new();
    private readonly Mock<IModArchiveService> _mockArchive = new();
    private readonly Mock<IModEnrichmentService> _mockEnrichment = new();
    private readonly Mock<IProfilePathService> _mockProfilePaths = new();
    private readonly Mock<IFileOperationPlanner> _mockPlanner = new();
    private readonly Mock<IModOperationQueue> _mockQueue = new();
    private readonly Mock<ILogHelper> _mockLogger = new();
    private readonly Mock<IProfileEventBus> _mockEventBus = new();
    private readonly Mock<IProcessRegistry> _mockRegistry = new();
    private readonly ModDeletionService _service;
    private readonly string _previewDir;

    public ModDeletionServiceTests()
    {
        _previewDir = Path.Combine(Path.GetTempPath(), "d3dx-del-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_previewDir);

        _mockProfilePaths.Setup(x => x.GetPreviewDirectoryPath(It.IsAny<string>())).Returns(_previewDir);
        _mockEventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _mockRegistry
            .Setup(x => x.Start(It.IsAny<ProcessType>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("proc1");
        _mockRegistry.Setup(x => x.GetToken(It.IsAny<string>())).Returns(CancellationToken.None);

        // Enrichment marks only the preview folder present (cache + archive absent so those steps skip).
        _mockEnrichment
            .Setup(x => x.PopulateStatusFlags(It.IsAny<List<ModInfo>>()))
            .Callback<List<ModInfo>>(list =>
            {
                foreach (var m in list)
                {
                    m.HasPreviewFolder = true;
                    m.HasCache = false;
                    m.IsAvailable = false;
                }
            });

        // By default the queue just runs the operation inline (and records the locked mod id).
        _mockQueue
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<Func<Task<bool>>>()))
            .Returns<string, Func<Task<bool>>>((id, op) => { _queuedIds.Add(id); return op(); });

        _service = new ModDeletionService(
            _mockRepository.Object,
            _mockCache.Object,
            _mockArchive.Object,
            _mockEnrichment.Object,
            _mockProfilePaths.Object,
            _mockPlanner.Object,
            _mockQueue.Object,
            _mockLogger.Object,
            _mockEventBus.Object,
            _mockRegistry.Object);
    }

    private readonly List<string> _queuedIds = new();

    [Fact]
    public async Task DeleteAsync_DeletesPreviewFolderViaPlanner_NotRawFileSystem()
    {
        // Arrange
        const string id = "mod1";
        _mockRepository.Setup(x => x.GetByIdAsync(id))
            .ReturnsAsync(new ModEntity { Id = id, Name = "Test", Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.DeleteAsync(id)).ReturnsAsync(true);

        FileSystemOperation? submitted = null;
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .Callback<FileSystemOperation>(op => submitted = op)
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var result = await _service.DeleteAsync(id);

        // Assert: preview deletion was routed through the planner as a DeleteDirectory op
        result.Should().BeTrue();
        submitted.Should().NotBeNull();
        submitted!.OperationType.Should().Be(FileSystemOperationType.DeleteDirectory);
        submitted.SourcePath.Should().Be(_previewDir);
    }

    [Fact]
    public async Task BatchDeleteAsync_SerializesEachDeletionThroughThePerModQueue()
    {
        // Arrange: three mods, all deletable
        var ids = new List<string> { "a", "b", "c" };
        _mockRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ModEntity { Id = id, Name = id, Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var result = await _service.BatchDeleteAsync(ids);

        // Assert: every id was deleted under its own per-mod queue lock
        result.SuccessCount.Should().Be(3);
        result.FailedCount.Should().Be(0);
        _queuedIds.Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task DeleteAsync_RegistersModDeleteProcess_AndCompletesOnSuccess()
    {
        // Arrange
        const string id = "mod1";
        _mockRepository.Setup(x => x.GetByIdAsync(id))
            .ReturnsAsync(new ModEntity { Id = id, Name = "Test", Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.DeleteAsync(id)).ReturnsAsync(true);
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        await _service.DeleteAsync(id);

        // Assert: the deletion is tracked as ONE ModDelete process and completed
        _mockRegistry.Verify(x => x.Start(ProcessType.ModDelete, It.Is<string>(t => t.Contains("Test")),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        _mockRegistry.Verify(x => x.Complete("proc1"), Times.Once);
        _mockRegistry.Verify(x => x.Fail(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OnStepFailure_FailsProcess_AndEmitsRefreshedToRollBackOptimisticUi()
    {
        // Arrange: DB delete fails → DELETE_DATABASE_FAILED
        const string id = "mod1";
        _mockRepository.Setup(x => x.GetByIdAsync(id))
            .ReturnsAsync(new ModEntity { Id = id, Name = "Test", Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.DeleteAsync(id)).ReturnsAsync(false);
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var act = () => _service.DeleteAsync(id);

        // Assert: exception surfaces, the process is Failed (not Completed), and REFRESHED is
        // emitted so the frontend's optimistic row removal is restored via MOD_LIST_UPDATED.
        await act.Should().ThrowAsync<OperationException>();
        _mockRegistry.Verify(x => x.Fail("proc1", It.IsAny<string>()), Times.Once);
        _mockRegistry.Verify(x => x.Complete(It.IsAny<string>()), Times.Never);
        _mockEventBus.Verify(x => x.EmitAsync(ModuleNames.MOD, ModEvents.REFRESHED, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task BatchDeleteAsync_TracksOneCancellableProcess_WithPerItemProgress()
    {
        // Arrange
        var ids = new List<string> { "a", "b", "c" };
        _mockRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ModEntity { Id = id, Name = id, Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var result = await _service.BatchDeleteAsync(ids);

        // Assert: ONE cancellable process for the whole batch (never one per item), per-item
        // progress reports, and Complete at the end.
        result.SuccessCount.Should().Be(3);
        _mockRegistry.Verify(x => x.Start(ProcessType.ModDelete, It.Is<string>(t => t.Contains("3")),
            true, It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        _mockRegistry.Verify(x => x.Report("proc1", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Exactly(3));
        _mockRegistry.Verify(x => x.Complete("proc1"), Times.Once);
    }

    [Fact]
    public async Task BatchDeleteAsync_WithPartialFailure_FailsProcessWithSummary()
    {
        // Arrange: mod "b" is missing from the DB → that item fails, others succeed
        var ids = new List<string> { "a", "b" };
        _mockRepository.Setup(x => x.GetByIdAsync("a"))
            .ReturnsAsync(new ModEntity { Id = "a", Name = "a", Type = "7z", Grading = "G" });
        _mockRepository.Setup(x => x.GetByIdAsync("b")).ReturnsAsync((ModEntity?)null);
        _mockRepository.Setup(x => x.DeleteAsync("a")).ReturnsAsync(true);
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var result = await _service.BatchDeleteAsync(ids);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.FailedIds.Should().ContainSingle().Which.Should().Be("b");
        _mockRegistry.Verify(x => x.Fail("proc1", It.Is<string>(m => m.Contains("1"))), Times.Once);
        _mockRegistry.Verify(x => x.Complete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BatchDeleteAsync_StopsProcessingWhenCancelled()
    {
        // Arrange: token already cancelled → the loop must not delete anything
        var ids = new List<string> { "a", "b", "c" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockRegistry.Setup(x => x.GetToken("proc1")).Returns(cts.Token);

        // Act
        var result = await _service.BatchDeleteAsync(ids);

        // Assert: no deletions ran, nothing reported failed (cancel is not failure)
        result.SuccessCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        _mockRepository.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        _mockQueue.Verify(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<Func<Task<bool>>>()), Times.Never);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_previewDir)) Directory.Delete(_previewDir, true); } catch { }
    }
}
