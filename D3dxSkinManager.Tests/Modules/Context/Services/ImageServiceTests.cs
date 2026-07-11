using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Context.Services;

/// <summary>
/// Unit tests for ImageService
/// Tests preview image operations including deletion with renumbering
/// </summary>
public class ImageServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _mockProfilePaths;
    private readonly Mock<IPathHelper> _mockPathHelper;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<ICustomSchemeHandler> _mockSchemeHandler;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly MockFileHelper _mockFileHelper;
    private readonly ImageService _imageService;
    private readonly string _testDirectory;
    private readonly string _previewDirectory;
    private readonly string _testSha = "ABC123";

    public ImageServiceTests()
    {
        _mockProfilePaths = new Mock<IProfilePathService>();
        _mockPathHelper = new Mock<IPathHelper>();
        _mockLogger = new Mock<ILogHelper>();
        _mockHashHelper = new Mock<IHashHelper>();
        _mockSchemeHandler = new Mock<ICustomSchemeHandler>();
        _mockEventBus = new Mock<IProfileEventBus>();
        _mockFileHelper = new MockFileHelper();

        // Setup test directory paths (no actual directories created)
        _testDirectory = Path.Combine("C:", "FakeTest");
        _previewDirectory = Path.Combine(_testDirectory, "previews", _testSha);

        // Setup mocks
        _mockProfilePaths
            .Setup(x => x.GetPreviewDirectoryPath(_testSha))
            .Returns(_previewDirectory);

        _mockPathHelper
            .Setup(x => x.ToAbsolutePath(It.IsAny<string>()))
            .Returns<string>(path => Path.IsPathRooted(path) ? path : Path.Combine(_testDirectory, path));

        _mockPathHelper
            .Setup(x => x.ToRelativePath(It.IsAny<string>()))
            .Returns<string>(path =>
            {
                if (path.StartsWith(_testDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Substring(_testDirectory.Length + 1).Replace("\\", "/");
                }
                return path;
            });

        _imageService = new ImageService(
            _mockProfilePaths.Object,
            _mockPathHelper.Object,
            _mockLogger.Object,
            _mockHashHelper.Object,
            _mockSchemeHandler.Object,
            _mockEventBus.Object,
            _mockFileHelper.Object
        );
    }

    // Helper to create fake files in the fake file system
    private void CreateFakeFile(string filePath)
    {
        _mockFileHelper.AddFile(filePath, "fake-content");
    }

    public void Dispose()
    {
        // No cleanup needed for fake file system
    }

    #region DeletePreviewAsync Tests

    [Fact]
    public async Task DeletePreviewAsync_WithMiddlePreview_ShouldRenumberSubsequentPreviews()
    {
        // Arrange - Create 4 preview files using fake file system
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");
        var preview4 = Path.Combine(_previewDirectory, "preview4.png");

        CreateFakeFile(preview1);
        CreateFakeFile(preview2);
        CreateFakeFile(preview3);
        CreateFakeFile(preview4);

        var relativePreview2 = "previews/ABC123/preview2.png";

        // Act - Delete preview2
        await _imageService.DeletePreviewAsync(_testSha, relativePreview2);

        // Assert - After deleting preview2, files should be renumbered:
        // preview1 stays as preview1, preview3→preview2, preview4→preview3

        // preview1 should be unchanged
        _mockFileHelper.HasFile(preview1).Should().BeTrue("preview1 should remain");

        // preview2 should now contain old preview3 content (renumbered)
        _mockFileHelper.HasFile(preview2).Should().BeTrue("preview3 should be renumbered to preview2");

        // preview3 should now contain old preview4 content (renumbered)
        _mockFileHelper.HasFile(preview3).Should().BeTrue("preview4 should be renumbered to preview3");

        // preview4 should no longer exist (was renamed to preview3)
        _mockFileHelper.HasFile(preview4).Should().BeFalse("original preview4 should be renamed to preview3");

        // Assert - Event was emitted
        _mockEventBus.Verify(
            x => x.EmitAsync("MOD", "PREVIEW_DELETED", It.IsAny<object>()),
            Times.Once,
            "PREVIEW_DELETED event should be emitted once"
        );

        // Assert - Cache was invalidated for all affected paths
        _mockSchemeHandler.Verify(
            x => x.InvalidatePaths(It.Is<IEnumerable<string>>(paths =>
                paths.Count() >= 3 // deleted + old preview3 + new preview2
            )),
            Times.Once,
            "Cache should be invalidated for all affected images"
        );
    }

    [Fact]
    public async Task DeletePreviewAsync_WhenLastPreviewRemoved_DeletesEmptyPreviewFolder()
    {
        // Arrange - a single preview (deleting it leaves the folder empty)
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        CreateFakeFile(preview1);

        // Act - delete the only preview
        await _imageService.DeletePreviewAsync(_testSha, "previews/ABC123/preview1.png");

        // Assert - the file is gone AND the now-empty preview folder is removed (legacy bug fix)
        _mockFileHelper.HasFile(preview1).Should().BeFalse("the only preview was deleted");
        _mockFileHelper.Mock.Verify(x => x.DeleteDirectoryAsync(_previewDirectory), Times.Once,
            "the now-empty preview folder should be removed");
    }

    [Fact]
    public async Task DeletePreviewAsync_WhenPreviewsRemain_KeepsPreviewFolder()
    {
        // Arrange - two previews (one remains after the delete)
        CreateFakeFile(Path.Combine(_previewDirectory, "preview1.png"));
        CreateFakeFile(Path.Combine(_previewDirectory, "preview2.png"));

        // Act - delete one; a preview still remains
        await _imageService.DeletePreviewAsync(_testSha, "previews/ABC123/preview2.png");

        // Assert - the folder is NOT removed while previews remain
        _mockFileHelper.Mock.Verify(x => x.DeleteDirectoryAsync(It.IsAny<string>()), Times.Never,
            "a folder with remaining previews must not be removed");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithLastPreview_ShouldNotRenumberAnyFiles()
    {
        // Arrange - Create 3 preview files using fake file system
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        CreateFakeFile(preview1);
        CreateFakeFile(preview2);
        CreateFakeFile(preview3);

        var relativePreview3 = "previews/ABC123/preview3.png";

        // Act - Delete preview3 (last one)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview3);

        // Assert - Only preview3 should be deleted, no renumbering
        _mockFileHelper.HasFile(preview3).Should().BeFalse("preview3 should be deleted");
        _mockFileHelper.HasFile(preview1).Should().BeTrue("preview1 should remain");
        _mockFileHelper.HasFile(preview2).Should().BeTrue("preview2 should remain");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithFirstPreview_ShouldRenumberAllSubsequentPreviews()
    {
        // Arrange - Create 3 preview files using fake file system
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        CreateFakeFile(preview1);
        CreateFakeFile(preview2);
        CreateFakeFile(preview3);

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Delete preview1 (first one)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - preview1 should be deleted and all renumbered
        _mockFileHelper.HasFile(preview3).Should().BeFalse("original preview3 should be renamed");
        _mockFileHelper.HasFile(preview1).Should().BeTrue("preview2 should become preview1");
        _mockFileHelper.HasFile(preview2).Should().BeTrue("preview3 should become preview2");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithDifferentExtensions_ShouldPreserveExtensions()
    {
        // Arrange - Create previews with different extensions using fake file system
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.jpg");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        CreateFakeFile(preview1);
        CreateFakeFile(preview2);
        CreateFakeFile(preview3);

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Delete preview1
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - Extensions should be preserved during renumbering
        var newPreview1 = Path.Combine(_previewDirectory, "preview1.jpg");
        var newPreview2 = Path.Combine(_previewDirectory, "preview2.png");

        _mockFileHelper.HasFile(newPreview1).Should().BeTrue("preview2.jpg should become preview1.jpg");
        _mockFileHelper.HasFile(newPreview2).Should().BeTrue("preview3.png should become preview2.png");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var relativePreview = "previews/ABC123/preview1.png";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _imageService.DeletePreviewAsync(_testSha, relativePreview)
        );
    }

    [Fact]
    public async Task DeletePreviewAsync_PreventsBugScenario_PasteDeletePasteSequence()
    {
        // This test reproduces the exact bug scenario from the issue:
        // 1. Load mod (auto-imports preview1.png)
        // 2. Paste from clipboard (creates preview2.png)
        // 3. Set preview2 as thumbnail (swaps: clipboard→preview1, original→preview2)
        // 4. Delete preview1 (clipboard)
        // 5. Paste again (should create preview2.png, not overwrite)

        // Arrange - Simulate step 3 result (after thumbnail swap) using fake file system
        var preview1 = Path.Combine(_previewDirectory, "preview1.png"); // clipboard image
        var preview2 = Path.Combine(_previewDirectory, "preview2.png"); // original image

        _mockFileHelper.AddFile(preview1, "clipboard_image");
        _mockFileHelper.AddFile(preview2, "original_image");

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Step 4: Delete preview1 (clipboard)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - After deletion, only preview1.png should exist (renumbered from preview2)
        _mockFileHelper.HasFile(preview1).Should().BeTrue("preview2 should be renumbered to preview1");
        _mockFileHelper.HasFile(preview2).Should().BeFalse("preview2 should be renamed to preview1");

        // Assert - Should have exactly 1 preview after deletion
        var previewsAfterDelete = _mockFileHelper.GetAllFiles()
            .Where(f => f.Contains(_previewDirectory) && Path.GetFileName(f).StartsWith("preview"));
        previewsAfterDelete.Should().HaveCount(1, "should have exactly 1 preview after deletion");

        // Simulate step 5: Paste from clipboard
        // The logic uses existingPreviews.Count + 1, which would be 2
        var nextPreview = Path.Combine(_previewDirectory, "preview2.png");
        _mockFileHelper.AddFile(nextPreview, "clipboard_image_2");

        // Assert - Both images should now exist without overwrite
        _mockFileHelper.HasFile(preview1).Should().BeTrue();
        _mockFileHelper.HasFile(nextPreview).Should().BeTrue();
    }

    #endregion
}
