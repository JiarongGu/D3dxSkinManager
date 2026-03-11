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

        // Mock concurrency manager
        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>()))
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

        _mockConcurrencyManager.Setup(x => x.TryAcquireSlotAsync(It.IsAny<string>()))
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
}
