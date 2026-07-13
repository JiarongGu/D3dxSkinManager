using System;
using System.Collections.Generic;
using System.Linq;
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
/// Tests for ModImportWorkflowHandler's LEG logic + temp-file handling. Scheduling (bounded concurrency,
/// priority, cancel-while-queued) is now the ImportQueueActor's job — verified in ImportQueueActorTests —
/// so the handler is tested by driving its <see cref="ModImportWorkflowHandler.ProcessAsync"/> (one leg)
/// directly with a mock queue. Key fixes still verified here:
/// 1. Temp files use workflowId.mic naming.
/// 2. TempArchivePath is set BEFORE compression starts.
/// 3. Compression + progress callbacks re-read from DB so they never overwrite user edits.
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
    private readonly Mock<IImportQueueActor> _mockQueue;
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
        _mockQueue = new Mock<IImportQueueActor>();
        _mockCategoryService = new Mock<ICategoryService>();

        _mockProfilePathService.Setup(x => x.TempDirectory).Returns("C:\\temp");
        _mockProfileContext.Setup(x => x.ProfileId).Returns("test-profile");
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _handler = new ModImportWorkflowHandler(
            _mockWorkflowRepository.Object,
            _mockModImportService.Object,
            _mockMetadataService.Object,
            _mockProfilePathService.Object,
            _mockProfileService.Object,
            _mockProfileContext.Object,
            _mockArchiveHelper.Object,
            _mockFileHelper.Object,
            _mockEventBus.Object,
            _mockLogger.Object,
            _mockEnrichmentService.Object,
            _mockQueue.Object,
            _mockCategoryService.Object
        );
    }

    #region Temp File Naming Tests

    [Fact]
    public void TempFileConstants_GetModImportCompressTempName_ShouldUseWorkflowId()
    {
        var workflowId = "workflow-abc-123";
        var tempName = TempFileConstants.GetModImportCompressTempName(workflowId);

        tempName.Should().Be($"{workflowId}.mic",
            "temp file should use workflowId.mic naming pattern for easier debugging");
        tempName.Should().EndWith(".mic");
        tempName.Should().NotContain("Guid", "should not use random GUID");
    }

    [Fact]
    public void TempFileConstants_GetModImportCompressTempName_WithSpecialCharacters_ShouldPreserveWorkflowId()
    {
        var workflowId = "workflow-with-dashes-and-numbers-123";
        var tempName = TempFileConstants.GetModImportCompressTempName(workflowId);
        tempName.Should().Be($"{workflowId}.mic");
    }

    #endregion

    #region StartImport enqueues onto the shared queue

    [Fact]
    public async Task StartImportAsync_CreatesPendingRow_AndEnqueuesOntoTheQueue()
    {
        var folderPath = "C:\\test\\my-mod";
        WorkflowInfo? created = null;
        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockWorkflowRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInfo>()))
            .Callback<WorkflowInfo>(w => created = w)
            .ReturnsAsync((WorkflowInfo w) => w);

        var result = await _handler.StartImportAsync(folderPath);

        created.Should().NotBeNull("the import row is created up front");
        created!.Status.Should().Be(WorkflowStatus.Pending, "it is queued, not run inline");
        _mockQueue.Verify(q => q.Enqueue(result.Id, "MOD_IMPORT", It.IsAny<WorkflowPriority>()), Times.Once,
            "the handler enqueues onto the shared import queue instead of spawning its own Task.Run");
    }

    #endregion

    #region ProcessAsync leg: TempArchivePath tracking

    [Fact]
    public async Task ProcessAsync_FolderImport_SetsTempArchivePathBeforeCompression()
    {
        var folderPath = "C:\\test\\my-mod";
        string? capturedTempPath = null;

        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt", "file2.txt" });

        var workflow = SeedWorkflow(ModImportWorkflowSteps.ExtractMetadata, folderPath);

        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>()))
            .Callback<WorkflowInfo>(w =>
            {
                var context = JsonHelper.Deserialize<ModImportWorkflowContext>(w.Context);
                if (context?.TempArchivePath != null && capturedTempPath == null)
                    capturedTempPath = context.TempArchivePath;
            })
            .Returns(Task.CompletedTask);

        SetupCompression((_, _) => { });
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-id-256");
        _mockModRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((ModEntity?)null);

        await _handler.ProcessAsync(workflow.Id, CancellationToken.None);

        capturedTempPath.Should().NotBeNull("TempArchivePath should be set before compression");
        capturedTempPath.Should().EndWith($"{workflow.Id}.mic",
            "temp file should use workflow ID with .mic extension");
    }

    [Fact]
    public async Task ProcessAsync_ProgressUpdates_PreserveTempArchivePath()
    {
        var folderPath = "C:\\test\\my-mod";
        var contextUpdatesFromProgress = new List<ModImportWorkflowContext>();

        _mockFileHelper.Setup(x => x.FileExists(folderPath)).Returns(false);
        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockFileHelper.Setup(x => x.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories))
            .Returns(new[] { "file1.txt" });

        var workflow = SeedWorkflow(ModImportWorkflowSteps.ExtractMetadata, folderPath);

        _mockWorkflowRepository.Setup(x => x.UpdateContextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contextJson) =>
            {
                var context = JsonHelper.Deserialize<ModImportWorkflowContext>(contextJson);
                if (context != null) contextUpdatesFromProgress.Add(context);
            })
            .Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);

        SetupCompression((callback, _) =>
        {
            foreach (var p in new[] { 10, 20, 30, 50, 70, 90 }) callback?.Invoke(p);
        });
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-id");
        _mockModRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((ModEntity?)null);

        await _handler.ProcessAsync(workflow.Id, CancellationToken.None);
        await Task.Delay(300); // let the FIRE-AND-FORGET progress-callback context writes land before asserting

        if (contextUpdatesFromProgress.Count > 0)
            contextUpdatesFromProgress.Should().AllSatisfy(ctx =>
                ctx.TempArchivePath.Should().NotBeNull(
                    "all progress callback context updates must have TempArchivePath set to prevent a race"));
    }

    #endregion

    #region ProcessAsync leg: user edits during compression are preserved

    [Fact]
    public async Task ProcessAsync_WhenUserEditsOccurDuringCompression_PreservesEditsAfterCompressionFinishes()
    {
        var folderPath = "C:\\test\\my-mod";
        const string userEditedName = "User Edited Name";
        const string userEditedCategory = "cat-user-123";
        var capturedUpdates = new List<(WorkflowStatus status, string contextJson)>();

        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true); // compress step guards on this

        // The DB always returns the user-edited mid-compress workflow (as if UpdateContextAsync ran during compression).
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
                    TempArchivePath = $"temp/{id}.mic",
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
        SetupCompression((_, _) => { });

        await _handler.ProcessAsync("wf-edit", CancellationToken.None);

        var waitingUpdates = capturedUpdates.Where(x => x.status == WorkflowStatus.WaitingForInput).ToList();
        waitingUpdates.Should().HaveCount(1, "compression must transition to WaitingForInput exactly once");

        var waitingCtx = JsonHelper.Deserialize<ModImportWorkflowContext>(waitingUpdates[0].contextJson);
        waitingCtx.Should().NotBeNull();
        waitingCtx!.Name.Should().Be(userEditedName, "user Name edits during compression must survive");
        waitingCtx.Category.Should().Be(userEditedCategory, "user Category edits during compression must survive");
        waitingCtx.Progress.Should().Be(100, "progress must be 100% after compression");
    }

    [Fact]
    public async Task ProcessAsync_ProgressCallbacks_DoNotOverwriteUserEditsInDatabase()
    {
        var folderPath = "C:\\test\\my-mod";
        const string userEditedName = "Live Edit During Compression";
        var contextsSavedByProgressCallbacks = new List<ModImportWorkflowContext>();

        _mockFileHelper.Setup(x => x.DirectoryExists(folderPath)).Returns(true); // compress step guards on this

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

        _mockWorkflowRepository.Setup(x => x.UpdateContextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contextJson) =>
            {
                var ctx = JsonHelper.Deserialize<ModImportWorkflowContext>(contextJson);
                if (ctx != null) contextsSavedByProgressCallbacks.Add(ctx);
            })
            .Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        SetupCompression((callback, _) =>
        {
            foreach (var p in new[] { 10, 30, 60, 90 }) callback?.Invoke(p);
        });

        await _handler.ProcessAsync("wf-live", CancellationToken.None);

        if (contextsSavedByProgressCallbacks.Count > 0)
            contextsSavedByProgressCallbacks.Should().AllSatisfy(ctx =>
                ctx.Name.Should().Be(userEditedName,
                    "progress callbacks must re-read from DB before writing so they never overwrite user edits"));
    }

    #endregion

    #region CancelAsync cleanup

    [Fact]
    public async Task CancelAsync_WithFolderImport_DeletesTempFileWithWorkflowIdName()
    {
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
                IsArchiveFile = false
            })
        };

        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.DeleteAsync(workflowId)).Returns(Task.CompletedTask);
        _mockFileHelper.Setup(x => x.FileExists(expectedTempPath)).Returns(true);
        _mockFileHelper.Setup(x => x.DeleteFileAsync(expectedTempPath)).ReturnsAsync(true);

        await _handler.CancelAsync(workflowId);
        await Task.Delay(300); // cleanup is fire-and-forget

        _mockQueue.Verify(q => q.Cancel(workflowId), Times.Once, "cancellation goes through the queue");
        _mockFileHelper.Verify(x => x.DeleteFileAsync(expectedTempPath), Times.Once,
            "temp file with workflowId.mic naming should be deleted during cleanup");
    }

    [Fact]
    public async Task CancelAsync_WithArchiveFile_DoesNotDeleteUserOriginalFile()
    {
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
                IsArchiveFile = true
            })
        };

        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflowId)).ReturnsAsync(workflow);
        _mockWorkflowRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _mockWorkflowRepository.Setup(x => x.DeleteAsync(workflowId)).Returns(Task.CompletedTask);
        _mockFileHelper.Setup(x => x.FileExists(userOriginalPath)).Returns(true);

        await _handler.CancelAsync(workflowId);
        await Task.Delay(300);

        _mockFileHelper.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Never,
            "user's original archive file should NOT be deleted");
    }

    #endregion

    // ---- helpers ----

    /// <summary>Seed a workflow that GetByIdAsync returns (so ProcessAsync can load + run its leg).</summary>
    private WorkflowInfo SeedWorkflow(string step, string folderPath)
    {
        var workflow = new WorkflowInfo
        {
            Id = Guid.NewGuid().ToString(),
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Pending,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext { Step = step, FolderPath = folderPath }),
            CreatedAt = DateTime.UtcNow
        };
        _mockWorkflowRepository.Setup(x => x.GetByIdAsync(workflow.Id)).ReturnsAsync(workflow);
        return workflow;
    }

    private void SetupCompression(Action<Action<int>?, CancellationToken> onCompress)
    {
        _mockArchiveHelper.Setup(x => x.CompressFolderAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ArchiveFormat>(),
                It.IsAny<CompressionLevel>(), It.IsAny<Action<int>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ArchiveFormat, CompressionLevel, Action<int>, CancellationToken>(
                (_, _, _, _, callback, ct) => onCompress(callback, ct))
            .ReturnsAsync((string src, string dest, ArchiveFormat fmt, CompressionLevel lvl, Action<int>? cb, CancellationToken ct) => dest);
    }
}
