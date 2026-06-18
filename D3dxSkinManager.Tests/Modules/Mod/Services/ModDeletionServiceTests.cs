using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
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
/// See .claude/rules/filesystem-operation-serialization.md
/// </summary>
public class ModDeletionServiceTests : IDisposable
{
    private readonly Mock<IModRepository> _mockRepository = new();
    private readonly Mock<IModCacheService> _mockCache = new();
    private readonly Mock<IModArchiveService> _mockArchive = new();
    private readonly Mock<IModEnrichmentService> _mockEnrichment = new();
    private readonly Mock<IProfilePathService> _mockProfilePaths = new();
    private readonly Mock<IFileOperationPlanner> _mockPlanner = new();
    private readonly Mock<ILogHelper> _mockLogger = new();
    private readonly Mock<IProfileEventBus> _mockEventBus = new();
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

        _service = new ModDeletionService(
            _mockRepository.Object,
            _mockCache.Object,
            _mockArchive.Object,
            _mockEnrichment.Object,
            _mockProfilePaths.Object,
            _mockPlanner.Object,
            _mockLogger.Object,
            _mockEventBus.Object);
    }

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

    public void Dispose()
    {
        try { if (Directory.Exists(_previewDir)) Directory.Delete(_previewDir, true); } catch { }
    }
}
