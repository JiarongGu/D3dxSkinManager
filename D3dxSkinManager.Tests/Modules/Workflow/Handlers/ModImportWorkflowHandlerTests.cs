using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Workflow.Handlers;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Services;
using SharpSevenZip;

namespace D3dxSkinManager.Tests.Modules.Workflow.Handlers;

/// <summary>
/// Tests for ModImportWorkflowHandler focusing on race conditions and temp file naming
/// Key fixes verified:
/// 1. Temp files use workflowId.mic naming (not random GUID)
/// 2. TempArchivePath is set BEFORE compression starts (prevents race condition)
/// 3. Progress callbacks preserve TempArchivePath in context
/// </summary>
public class ModImportWorkflowHandlerTests
{
    private readonly Mock<IWorkflowRepository> _mockWorkflowRepository;
    private readonly Mock<IModImportService> _mockModImportService;
    private readonly Mock<IModMetadataService> _mockMetadataService;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IProfilePathService> _mockProfilePathService;
    private readonly Mock<IProfileService> _mockProfileService;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<IArchiveHelper> _mockArchiveHelper;
    private readonly Mock<IFileHelper> _mockFileHelper;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IModEnrichmentService> _mockEnrichmentService;
    private readonly Mock<IWorkflowConcurrencyManager> _mockConcurrencyManager;
    private readonly Mock<ICategoryService> _mockCategoryService;
    private readonly ModImportWorkflowHandler _handler;

    public ModImportWorkflowHandlerTests()
    {
        _mockWorkflowRepository = new Mock<IWorkflowRepository>();
        _mockModImportService = new Mock<IModImportService>();
        _mockMetadataService = new Mock<IModMetadataService>();
        _mockModRepository = new Mock<IModRepository>();
        _mockProfilePathService = new Mock<IProfilePathService>();
        _mockProfileService = new Mock<IProfileService>();
        _mockProfileContext = new Mock<IProfileContext>();
        _mockArchiveHelper = new Mock<IArchiveHelper>();
        _mockFileHelper = new Mock<IFileHelper>();
        _mockHashHelper = new Mock<IHashHelper>();
        _mockEventBus = new Mock<IEventBus>();
        _mockLogger = new Mock<ILogHelper>();
        _mockEnrichmentService = new Mock<IModEnrichmentService>();
        _mockConcurrencyManager = new Mock<IWorkflowConcurrencyManager>();
        _mockCategoryService = new Mock<ICategoryService>();

        // Setup default temp directory
        _mockProfilePathService.Setup(x => x.TempDirectory).Returns("C:\\temp");
        _mockProfileContext.Setup(x => x.ProfileId).Returns("test-profile");

        _handler = new ModImportWorkflowHandler(
            _mockWorkflowRepository.Object,
            _mockModImportService.Object,
            _mockMetadataService.Object,
            _mockModRepository.Object,
            _mockProfilePathService.Object,
            _mockProfileService.Object,
            _mockProfileContext.Object,
            _mockArchiveHelper.Object,
            _mockFileHelper.Object,
            _mockHashHelper.Object,
            _mockEventBus.Object,
            _mockLogger.Object,
            _mockEnrichmentService.Object,
            _mockConcurrencyManager.Object,
            _mockCategoryService.Object
        );
    }

    #region Temp File Naming Tests

    [Fact]
    public void TempFileConstants_GetModImportCompressTempName_ShouldUseWorkflowId()
    {
        // Arrange
        var workflowId = "workflow-abc-123";

        // Act
        var tempName = TempFileConstants.GetModImportCompressTempName(workflowId);

        // Assert
        tempName.Should().Be($"{workflowId}.mic",
            "temp file should use workflowId.mic naming pattern for easier debugging");
        tempName.Should().EndWith(".mic");
        tempName.Should().NotContain("Guid", "should not use random GUID");
    }

    [Fact]
    public void TempFileConstants_GetModImportCompressTempName_WithSpecialCharacters_ShouldPreserveWorkflowId()
    {
        // Arrange
        var workflowId = "workflow-with-dashes-and-numbers-123";

        // Act
        var tempName = TempFileConstants.GetModImportCompressTempName(workflowId);

        // Assert
        tempName.Should().Be($"{workflowId}.mic");
    }

    #endregion

    #region Integration Test: Full Workflow with TempArchivePath Tracking

    [Fact]
    public async Task StartImportAsync_FolderImport_ShouldSetTempArchivePathBeforeCompression()
    {
        // Arrange
        var folderPath = "C:\\test\\my-mod";
        string? capturedTempPath = null;
        var workflowCreated = false;

        // Setup mocks
        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt", "file2.txt" });

        // Capture workflow when created
        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .Callback<WorkflowInfo>(w =>
            {
                workflowCreated = true;
            })
            .ReturnsAsync((WorkflowInfo w) => w);

        // Capture TempArchivePath when it's first set
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Callback<WorkflowInfo>(w =>
            {
                var context = JsonHelper.Deserialize<ModImportWorkflowContext>(w.Context);
                if (context?.TempArchivePath != null && capturedTempPath == null)
                {
                    capturedTempPath = context.TempArchivePath;
                }
            })
            .Returns(Task.CompletedTask);

        // Mock concurrency manager (signature updated to include CancellationToken)
        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup compression to simulate progress callbacks
        _mockArchiveHelper.Setup(x => x.CompressFolderAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ArchiveFormat>(),
            It.IsAny<CompressionLevel>(),
            It.IsAny<Action<int>>(),
            It.IsAny<CancellationToken>()
        ))
        .Callback<string, string, ArchiveFormat, CompressionLevel, Action<int>, CancellationToken>((src, dest, fmt, lvl, callback, ct) =>
        {
            // Simulate progress during compression
            callback?.Invoke(10);
            callback?.Invoke(50);
            callback?.Invoke(90);
        })
        .ReturnsAsync((string src, string dest, ArchiveFormat fmt, CompressionLevel lvl, Action<int>? cb, CancellationToken ct) => dest);

        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-id-256");
        _mockModRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ModEntity?)null);

        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.StartImportAsync(folderPath);

        // Wait for async processing to complete
        await Task.Delay(500);

        // Assert
        result.Should().NotBeNull();
        workflowCreated.Should().BeTrue("workflow should be created");
        capturedTempPath.Should().NotBeNull("TempArchivePath should be set before compression");
        capturedTempPath.Should().EndWith($"{result.Id}.mic",
            "temp file should use workflow ID with .mic extension");
        capturedTempPath.Should().Contain(result.Id,
            "temp file name should contain the workflow ID for easy tracking");
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task CancelAsync_WithFolderImport_ShouldDeleteTempFileWithWorkflowIdName()
    {
        // Arrange
        var workflowId = "workflow-cleanup-123";
        var expectedTempPath = $"C:\\temp\\{workflowId}.mic";

        var workflow = new WorkflowInfo
        {
            Id = workflowId,
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Processing,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext
            {
                Step = ModImportWorkflowSteps.CompressFolder,
                FolderPath = "C:\\test\\my-mod",
                TempArchivePath = expectedTempPath,
                IsArchiveFile = false  // We created the temp file, should delete it
            })
        };

        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.DeleteAsync(workflowId)).Returns(Task.CompletedTask);
        _mockFileHelper.Setup(x => x.FileExists(expectedTempPath)).Returns(true);
        _mockFileHelper.Setup(x => x.DeleteFileAsync(expectedTempPath)).ReturnsAsync(true);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.CancelAsync(workflowId);

        // Wait for async cleanup to complete
        await Task.Delay(300);

        // Assert
        _mockFileHelper.Verify(x => x.DeleteFileAsync(expectedTempPath), Times.Once,
            "temp file with workflowId.mic naming should be deleted during cleanup");
    }

    [Fact]
    public async Task CancelAsync_WithArchiveFile_ShouldNotDeleteUserOriginalFile()
    {
        // Arrange
        var workflowId = "workflow-archive-123";
        var userOriginalPath = "C:\\user\\original-mod.7z";

        var workflow = new WorkflowInfo
        {
            Id = workflowId,
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.WaitingForInput,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext
            {
                Step = ModImportWorkflowSteps.ExtractMetadata,
                FolderPath = userOriginalPath,
                TempArchivePath = userOriginalPath,
                IsArchiveFile = true  // User's original file, should NOT delete
            })
        };

        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.DeleteAsync(workflowId)).Returns(Task.CompletedTask);
        _mockFileHelper.Setup(x => x.FileExists(userOriginalPath)).Returns(true);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.CancelAsync(workflowId);

        // Wait for async cleanup to complete
        await Task.Delay(300);

        // Assert
        _mockFileHelper.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Never,
            "user's original archive file should NOT be deleted");
    }

    #endregion

    #region Queued-Cancellation Tests

    /// <summary>
    /// Regression test for the "cancelled tasks still running" bug.
    ///
    /// Before the fix: TryAcquireSlotAsync had no CancellationToken parameter.
    /// A queued Task.Run would eventually acquire a semaphore slot even after
    /// CancelAsync fired, then overwrite WorkflowStatus.Deleting → Processing in
    /// the DB and emit a spurious STATUS_CHANGED event.
    ///
    /// After the fix: TryAcquireSlotAsync accepts a CancellationToken.
    /// When the token is cancelled, _semaphore.WaitAsync(cancellationToken) throws
    /// OperationCanceledException and the Task.Run exits before touching the DB.
    /// </summary>
    [Fact]
    public async Task CancelAsync_WhenWorkflowIsQueued_ShouldNotSetStatusToProcessing()
    {
        // Arrange: concurrency manager blocks forever until its token is cancelled,
        // simulating a workflow that is stuck waiting for a free slot.
        _mockConcurrencyManager
            .Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token); // blocks until CancelAsync fires
                return true;
            });

        _mockFileHelper.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .ReturnsAsync((WorkflowInfo w) => w);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.DeleteAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Start the workflow — its Task.Run immediately blocks in TryAcquireSlotAsync
        var workflow = await _handler.StartImportAsync("C:\\test\\queued-mod");

        // Wire GetByIdAsync so CancelAsync can load the workflow
        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflow.Id))
            .ReturnsAsync(workflow);

        // Act: cancel while the workflow is still queued
        await _handler.CancelAsync(workflow.Id);

        // Wait long enough for the Task.Run to receive the cancellation and exit
        await Task.Delay(300);

        // Assert: the queued task must NOT have written Processing to the repository.
        // CancelAsync writes Deleting (that UpdateAsync call is expected), but the
        // Task.Run's own "workflow.Status = Processing" block must never execute.
        _mockWorkflowRepository.Verify(
            x => x.UpdateAsync(It.Is<WorkflowInfo>(w => w.Status == WorkflowStatus.Processing)),
            Times.Never,
            "a workflow cancelled while queued must exit before overwriting Deleting with Processing");
    }

    #endregion

    #region Race Condition Prevention Tests

    [Fact]
    public async Task StartImportAsync_ProgressUpdates_ShouldPreserveTempArchivePath()
    {
        // Arrange
        var folderPath = "C:\\test\\my-mod";
        var contextUpdatesFromProgress = new List<ModImportWorkflowContext>();

        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt" });

        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .ReturnsAsync((WorkflowInfo w) => w);

        // Capture ALL context updates (including fire-and-forget progress updates)
        _mockWorkflowRepository.Setup(x => x.UpdateContextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((id, contextJson) =>
            {
                var context = JsonHelper.Deserialize<ModImportWorkflowContext>(contextJson);
                if (context != null)
                {
                    contextUpdatesFromProgress.Add(context);
                }
            })
            .Returns(Task.CompletedTask);

        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Returns(Task.CompletedTask);

        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Simulate compression with multiple progress callbacks
        _mockArchiveHelper.Setup(x => x.CompressFolderAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ArchiveFormat>(),
            It.IsAny<CompressionLevel>(),
            It.IsAny<Action<int>>(),
            It.IsAny<CancellationToken>()
        ))
        .Callback<string, string, ArchiveFormat, CompressionLevel, Action<int>, CancellationToken>((src, dest, fmt, lvl, callback, ct) =>
        {
            // Simulate rapid progress callbacks (potential race condition scenario)
            callback?.Invoke(10);
            callback?.Invoke(20);
            callback?.Invoke(30);
            callback?.Invoke(50);
            callback?.Invoke(70);
            callback?.Invoke(90);
        })
        .ReturnsAsync((string src, string dest, ArchiveFormat fmt, CompressionLevel lvl, Action<int>? cb, CancellationToken ct) => dest);

        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-id");
        _mockModRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ModEntity?)null);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.StartImportAsync(folderPath);

        // Wait for all async progress updates to complete
        await Task.Delay(1000);

        // Assert
        result.Should().NotBeNull();

        // Verify that progress updates preserve TempArchivePath
        if (contextUpdatesFromProgress.Count > 0)
        {
            contextUpdatesFromProgress.Should().AllSatisfy(ctx =>
            {
                ctx.TempArchivePath.Should().NotBeNull(
                    "all progress callback context updates must have TempArchivePath set to prevent race condition");
                ctx.TempArchivePath.Should().EndWith($"{result.Id}.mic",
                    "TempArchivePath should remain consistent across all progress updates");
            });
        }
    }

    #endregion

    #region User Edit Preservation During Compression

    /// <summary>
    /// Regression test for: user edits (Name, Category) made while compression is running
    /// were silently overwritten by stale in-memory context when compression finished and
    /// wrote the final WaitingForInput status back to the database.
    ///
    /// Fix: CompressFolderAsync re-reads the workflow from DB at the end of compression and
    /// only updates system-owned fields (Progress, Status), preserving any user edits.
    /// </summary>
    [Fact]
    public async Task StartImportAsync_WhenUserEditsOccurDuringCompression_PreservesEditsAfterCompressionFinishes()
    {
        // Arrange
        var folderPath = "C:\\test\\my-mod";
        const string userEditedName = "User Edited Name";
        const string userEditedCategory = "cat-user-123";

        // Capture (status, contextJson) at call time — WorkflowStatus is a value type and
        // string is immutable, so these are NOT affected by later mutations to the workflow object
        // (avoiding the Moq gotcha where Verify re-evaluates conditions on already-mutated objects).
        var capturedUpdates = new List<(WorkflowStatus status, string contextJson)>();

        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt" });

        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .ReturnsAsync((WorkflowInfo w) => w);

        // Simulate the DB containing user-edited fields — as if the user called
        // UpdateContextAsync (Name, Category) while compression was running.
        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .Returns<string>(id => Task.FromResult<WorkflowInfo?>(new WorkflowInfo
            {
                Id = id,
                Type = "MOD_IMPORT",
                Status = WorkflowStatus.Processing,
                Context = JsonHelper.Serialize(new ModImportWorkflowContext
                {
                    Step = ModImportWorkflowSteps.CompressFolder,
                    FolderPath = folderPath,
                    Name = userEditedName,
                    Category = userEditedCategory,
                    TempArchivePath = $"temp/{id}.mic",  // already persisted before compression started
                    Progress = 70,
                    IsArchiveFile = false
                }),
                CreatedAt = DateTime.UtcNow
            }));

        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Callback<WorkflowInfo>(w => capturedUpdates.Add((w.Status, w.Context ?? "")))
            .Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.UpdateContextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockArchiveHelper.Setup(x => x.CompressFolderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ArchiveFormat>(),
            It.IsAny<CompressionLevel>(), It.IsAny<Action<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string src, string dest, ArchiveFormat fmt, CompressionLevel lvl, Action<int>? cb, CancellationToken ct) => dest);

        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.StartImportAsync(folderPath);
        await Task.Delay(1000); // Wait for async Task.Run processing to complete

        // Assert: exactly one WaitingForInput write; it must carry the user-edited fields
        // re-read from DB, not the stale in-memory values the handler held before the edit.
        var waitingUpdates = capturedUpdates.Where(x => x.status == WorkflowStatus.WaitingForInput).ToList();
        waitingUpdates.Should().HaveCount(1, "compression must transition to WaitingForInput exactly once");

        var waitingCtx = JsonHelper.Deserialize<ModImportWorkflowContext>(waitingUpdates[0].contextJson);
        waitingCtx.Should().NotBeNull();
        waitingCtx!.Name.Should().Be(userEditedName,
            "user edits (Name) made during compression must not be overwritten when compression finishes");
        waitingCtx.Category.Should().Be(userEditedCategory,
            "user edits (Category) made during compression must not be overwritten when compression finishes");
        waitingCtx.Progress.Should().Be(100, "progress must be 100% after compression");
    }

    /// <summary>
    /// Verifies that progress-callback DB writes (fire-and-forget) also re-read from DB
    /// so that rapid progress updates cannot overwrite user edits saved during compression.
    /// </summary>
    [Fact]
    public async Task StartImportAsync_ProgressCallbacks_ShouldNotOverwriteUserEditsInDatabase()
    {
        // Arrange
        var folderPath = "C:\\test\\my-mod";
        const string userEditedName = "Live Edit During Compression";
        var contextsSavedByProgressCallbacks = new List<ModImportWorkflowContext>();

        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt" });

        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .ReturnsAsync((WorkflowInfo w) => w);

        // DB always returns the user-edited workflow (simulates a live edit)
        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
            .Returns<string>(id => Task.FromResult<WorkflowInfo?>(new WorkflowInfo
            {
                Id = id,
                Type = "MOD_IMPORT",
                Status = WorkflowStatus.Processing,
                Context = JsonHelper.Serialize(new ModImportWorkflowContext
                {
                    Step = ModImportWorkflowSteps.CompressFolder,
                    FolderPath = folderPath,
                    Name = userEditedName,
                    TempArchivePath = $"temp/{id}.mic",
                    Progress = 50,
                    IsArchiveFile = false
                }),
                CreatedAt = DateTime.UtcNow
            }));

        // Capture all context strings written by progress callbacks
        _mockWorkflowRepository.Setup(x => x.UpdateContextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contextJson) =>
            {
                var ctx = JsonHelper.Deserialize<ModImportWorkflowContext>(contextJson);
                if (ctx != null) contextsSavedByProgressCallbacks.Add(ctx);
            })
            .Returns(Task.CompletedTask);

        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Returns(Task.CompletedTask);

        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Fire multiple progress callbacks to exercise the fire-and-forget path
        _mockArchiveHelper.Setup(x => x.CompressFolderAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ArchiveFormat>(),
            It.IsAny<CompressionLevel>(), It.IsAny<Action<int>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ArchiveFormat, CompressionLevel, Action<int>, CancellationToken>(
                (_, _, _, _, callback, _) =>
                {
                    callback?.Invoke(10);
                    callback?.Invoke(30);
                    callback?.Invoke(60);
                    callback?.Invoke(90);
                })
            .ReturnsAsync((string _, string dest, ArchiveFormat _, CompressionLevel _, Action<int>? _, CancellationToken _) => dest);

        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.StartImportAsync(folderPath);
        await Task.Delay(1000);

        // Assert: every context written by progress callbacks must carry the user-edited Name,
        // not the original stale in-memory value.
        if (contextsSavedByProgressCallbacks.Count > 0)
        {
            contextsSavedByProgressCallbacks.Should().AllSatisfy(ctx =>
                ctx.Name.Should().Be(userEditedName,
                    "progress callbacks must re-read from DB before writing so they never overwrite user edits"));
        }
    }

    #endregion
}
