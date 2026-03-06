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

        // Setup temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ImageServiceTests_{Guid.NewGuid()}");
        _previewDirectory = Path.Combine(_testDirectory, "previews", _testSha);
        Directory.CreateDirectory(_previewDirectory);

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
                if (path.StartsWith(_testDirectory))
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
            _mockEventBus.Object
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region DeletePreviewAsync Tests

    [Fact]
    public async Task DeletePreviewAsync_WithMiddlePreview_ShouldRenumberSubsequentPreviews()
    {
        // Arrange - Create 4 preview files
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");
        var preview4 = Path.Combine(_previewDirectory, "preview4.png");

        await File.WriteAllTextAsync(preview1, "image1");
        await File.WriteAllTextAsync(preview2, "image2");
        await File.WriteAllTextAsync(preview3, "image3");
        await File.WriteAllTextAsync(preview4, "image4");

        var relativePreview2 = "previews/ABC123/preview2.png";

        // Act - Delete preview2
        await _imageService.DeletePreviewAsync(_testSha, relativePreview2);

        // Assert - preview2 should be deleted
        File.Exists(preview2).Should().BeFalse("preview2 should be deleted");

        // Assert - preview3 should be renumbered to preview2
        File.Exists(preview3).Should().BeFalse("preview3 should be renamed");
        File.Exists(preview2).Should().BeTrue("preview3 should become preview2");
        (await File.ReadAllTextAsync(preview2)).Should().Be("image3", "preview2 should contain image3 content");

        // Assert - preview4 should be renumbered to preview3
        File.Exists(preview4).Should().BeFalse("preview4 should be renamed");
        var newPreview3 = Path.Combine(_previewDirectory, "preview3.png");
        File.Exists(newPreview3).Should().BeTrue("preview4 should become preview3");
        (await File.ReadAllTextAsync(newPreview3)).Should().Be("image4", "preview3 should contain image4 content");

        // Assert - preview1 should be unchanged
        File.Exists(preview1).Should().BeTrue("preview1 should remain");
        (await File.ReadAllTextAsync(preview1)).Should().Be("image1", "preview1 content should be unchanged");

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
    public async Task DeletePreviewAsync_WithLastPreview_ShouldNotRenumberAnyFiles()
    {
        // Arrange - Create 3 preview files
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        await File.WriteAllTextAsync(preview1, "image1");
        await File.WriteAllTextAsync(preview2, "image2");
        await File.WriteAllTextAsync(preview3, "image3");

        var relativePreview3 = "previews/ABC123/preview3.png";

        // Act - Delete preview3 (last one)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview3);

        // Assert - Only preview3 should be deleted, no renumbering
        File.Exists(preview3).Should().BeFalse("preview3 should be deleted");
        File.Exists(preview1).Should().BeTrue("preview1 should remain");
        File.Exists(preview2).Should().BeTrue("preview2 should remain");

        (await File.ReadAllTextAsync(preview1)).Should().Be("image1");
        (await File.ReadAllTextAsync(preview2)).Should().Be("image2");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithFirstPreview_ShouldRenumberAllSubsequentPreviews()
    {
        // Arrange - Create 3 preview files
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.png");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        await File.WriteAllTextAsync(preview1, "image1");
        await File.WriteAllTextAsync(preview2, "image2");
        await File.WriteAllTextAsync(preview3, "image3");

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Delete preview1 (first one)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - preview1 should be deleted and all renumbered
        File.Exists(preview3).Should().BeFalse("preview3 should be renamed");
        File.Exists(preview1).Should().BeTrue("preview2 should become preview1");
        File.Exists(preview2).Should().BeTrue("preview3 should become preview2");

        (await File.ReadAllTextAsync(preview1)).Should().Be("image2", "new preview1 should contain image2");
        (await File.ReadAllTextAsync(preview2)).Should().Be("image3", "new preview2 should contain image3");
    }

    [Fact]
    public async Task DeletePreviewAsync_WithDifferentExtensions_ShouldPreserveExtensions()
    {
        // Arrange - Create previews with different extensions
        var preview1 = Path.Combine(_previewDirectory, "preview1.png");
        var preview2 = Path.Combine(_previewDirectory, "preview2.jpg");
        var preview3 = Path.Combine(_previewDirectory, "preview3.png");

        await File.WriteAllTextAsync(preview1, "image1");
        await File.WriteAllTextAsync(preview2, "image2");
        await File.WriteAllTextAsync(preview3, "image3");

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Delete preview1
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - Extensions should be preserved during renumbering
        var newPreview1 = Path.Combine(_previewDirectory, "preview1.jpg");
        var newPreview2 = Path.Combine(_previewDirectory, "preview2.png");

        File.Exists(newPreview1).Should().BeTrue("preview2.jpg should become preview1.jpg");
        File.Exists(newPreview2).Should().BeTrue("preview3.png should become preview2.png");

        (await File.ReadAllTextAsync(newPreview1)).Should().Be("image2");
        (await File.ReadAllTextAsync(newPreview2)).Should().Be("image3");
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

        // Arrange - Simulate step 3 result (after thumbnail swap)
        var preview1 = Path.Combine(_previewDirectory, "preview1.png"); // clipboard image
        var preview2 = Path.Combine(_previewDirectory, "preview2.png"); // original image

        await File.WriteAllTextAsync(preview1, "clipboard_image");
        await File.WriteAllTextAsync(preview2, "original_image");

        var relativePreview1 = "previews/ABC123/preview1.png";

        // Act - Step 4: Delete preview1 (clipboard)
        await _imageService.DeletePreviewAsync(_testSha, relativePreview1);

        // Assert - After deletion, only preview1.png should exist (renumbered from preview2)
        File.Exists(preview1).Should().BeTrue("preview2 should be renumbered to preview1");
        File.Exists(preview2).Should().BeFalse("preview2 should be renamed to preview1");
        (await File.ReadAllTextAsync(preview1)).Should().Be("original_image",
            "preview1 should now contain the original image");

        // Assert - ImportPreviewFromClipboardAsync would now see 1 existing preview
        // and create preview2.png (not overwriting preview1)
        var previews = Directory.GetFiles(_previewDirectory, "preview*.*");
        previews.Should().HaveCount(1, "should have exactly 1 preview after deletion");

        // Simulate step 5: Paste from clipboard
        // The logic uses existingPreviews.Count + 1, which would be 2
        var nextPreview = Path.Combine(_previewDirectory, "preview2.png");
        await File.WriteAllTextAsync(nextPreview, "clipboard_image_2");

        // Assert - Both images should now exist without overwrite
        File.Exists(preview1).Should().BeTrue();
        File.Exists(nextPreview).Should().BeTrue();
        (await File.ReadAllTextAsync(preview1)).Should().Be("original_image",
            "original image should not be overwritten");
        (await File.ReadAllTextAsync(nextPreview)).Should().Be("clipboard_image_2",
            "new clipboard image should be in preview2");
    }

    #endregion
}
