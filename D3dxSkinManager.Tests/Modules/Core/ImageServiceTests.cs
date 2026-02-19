using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core;

/// <summary>
/// Unit tests for ImageService - tests preview scanning, generation, and caching
/// </summary>
public class ImageServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly PathHelper _pathHelper;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly ImageService _service;
    private readonly string _testSha = "abc123def456";

    public ImageServiceTests()
    {
        // Arrange - Create temp directory for each test
        _testDataPath = Path.Combine(Path.GetTempPath(), $"test_imageservice_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);

        _pathHelper = new PathHelper(_testDataPath);

        // Mock ProfileContext
        _mockProfileContext = new Mock<IProfileContext>();
        _mockProfileContext.Setup(x => x.ProfilePath).Returns(_testDataPath);

        _service = new ImageService(_mockProfileContext.Object, _pathHelper);
    }

    public void Dispose()
    {
        // Cleanup - Delete temp directory after test
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region GetPreviewPathsAsync Tests

    [Fact]
    public async Task GetPreviewPathsAsync_WhenFolderDoesNotExist_ShouldReturnEmptyList()
    {
        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPreviewPathsAsync_WhenFolderIsEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPreviewPathsAsync_WithMultiplePreviews_ShouldReturnAllPaths()
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        // Create multiple preview files
        var preview1 = Path.Combine(previewFolder, "preview1.png");
        var preview2 = Path.Combine(previewFolder, "preview2.png");
        var preview3 = Path.Combine(previewFolder, "preview3.jpg");

        File.WriteAllText(preview1, "fake image data 1");
        File.WriteAllText(preview2, "fake image data 2");
        File.WriteAllText(preview3, "fake image data 3");

        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(preview1);
        result.Should().Contain(preview2);
        result.Should().Contain(preview3);
    }

    [Fact]
    public async Task GetPreviewPathsAsync_WithNonImageFiles_ShouldOnlyReturnImageFiles()
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        var preview1 = Path.Combine(previewFolder, "preview1.png");
        var textFile = Path.Combine(previewFolder, "readme.txt");
        var execFile = Path.Combine(previewFolder, "script.exe");

        File.WriteAllText(preview1, "fake image data");
        File.WriteAllText(textFile, "text content");
        File.WriteAllText(execFile, "executable");

        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(preview1);
        result.Should().NotContain(textFile);
        result.Should().NotContain(execFile);
    }

    [Fact]
    public async Task GetPreviewPathsAsync_ShouldReturnPathsInAlphabeticalOrder()
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        // Create files in non-alphabetical order
        var preview3 = Path.Combine(previewFolder, "preview3.png");
        var preview1 = Path.Combine(previewFolder, "preview1.png");
        var preview2 = Path.Combine(previewFolder, "preview2.png");

        File.WriteAllText(preview3, "data");
        File.WriteAllText(preview1, "data");
        File.WriteAllText(preview2, "data");

        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Should().Contain("preview1.png");
        result[1].Should().Contain("preview2.png");
        result[2].Should().Contain("preview3.png");
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    public async Task GetPreviewPathsAsync_ShouldSupportVariousImageFormats(string extension)
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        var previewFile = Path.Combine(previewFolder, $"preview1{extension}");
        File.WriteAllText(previewFile, "fake image data");

        // Act
        var result = await _service.GetPreviewPathsAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(previewFile);
    }

    #endregion

    #region ClearModCacheAsync Tests

    [Fact]
    public async Task ClearModCacheAsync_WhenPreviewFolderExists_ShouldDeleteEntireFolder()
    {
        // Arrange
        var thumbnailPath = Path.Combine(_testDataPath, "thumbnails", $"{_testSha}.png");
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        Directory.CreateDirectory(previewFolder);

        File.WriteAllText(thumbnailPath, "thumbnail data");
        File.WriteAllText(Path.Combine(previewFolder, "preview1.png"), "preview data 1");
        File.WriteAllText(Path.Combine(previewFolder, "preview2.png"), "preview data 2");

        // Act
        var result = await _service.ClearModCacheAsync(_testSha);

        // Assert
        result.Should().BeTrue();
        File.Exists(thumbnailPath).Should().BeFalse("Thumbnail should be deleted");
        Directory.Exists(previewFolder).Should().BeFalse("Preview folder should be deleted");
    }

    [Fact]
    public async Task ClearModCacheAsync_WhenNothingExists_ShouldReturnFalse()
    {
        // Act
        var result = await _service.ClearModCacheAsync(_testSha);

        // Assert
        result.Should().BeFalse("Nothing was cleared");
    }

    [Fact]
    public async Task ClearModCacheAsync_WhenOnlyThumbnailExists_ShouldDeleteThumbnail()
    {
        // Arrange
        var thumbnailPath = Path.Combine(_testDataPath, "thumbnails", $"{_testSha}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        File.WriteAllText(thumbnailPath, "thumbnail data");

        // Act
        var result = await _service.ClearModCacheAsync(_testSha);

        // Assert
        result.Should().BeTrue();
        File.Exists(thumbnailPath).Should().BeFalse();
    }

    [Fact]
    public async Task ClearModCacheAsync_WhenOnlyPreviewFolderExists_ShouldDeleteFolder()
    {
        // Arrange
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);
        File.WriteAllText(Path.Combine(previewFolder, "preview1.png"), "preview data");

        // Act
        var result = await _service.ClearModCacheAsync(_testSha);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(previewFolder).Should().BeFalse();
    }

    #endregion

    #region GetSupportedImageExtensions Tests

    [Fact]
    public void GetSupportedImageExtensions_ShouldReturnCommonFormats()
    {
        // Act
        var extensions = _service.GetSupportedImageExtensions();

        // Assert
        extensions.Should().NotBeNull();
        extensions.Should().NotBeEmpty();
        extensions.Should().Contain(".png");
        extensions.Should().Contain(".jpg");
        extensions.Should().Contain(".jpeg");
        extensions.Should().Contain(".gif");
        extensions.Should().Contain(".bmp");
    }

    [Fact]
    public void GetSupportedImageExtensions_ShouldReturnLowercaseExtensions()
    {
        // Act
        var extensions = _service.GetSupportedImageExtensions();

        // Assert
        extensions.Should().OnlyContain(ext => ext == ext.ToLowerInvariant());
    }

    #endregion

    #region GetThumbnailPathAsync Tests

    [Fact]
    public async Task GetThumbnailPathAsync_WhenThumbnailDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetThumbnailPathAsync(_testSha);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetThumbnailPathAsync_WhenThumbnailExists_ShouldReturnPath()
    {
        // Arrange
        var thumbnailPath = Path.Combine(_testDataPath, "thumbnails", $"{_testSha}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        File.WriteAllText(thumbnailPath, "thumbnail data");

        // Act
        var result = await _service.GetThumbnailPathAsync(_testSha);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(thumbnailPath);
    }

    #endregion

    #region Integration Tests - Real Scenario

    [Fact]
    public async Task RealScenario_UserAddsPreviewsManually_ShouldBeDiscovered()
    {
        // Scenario: User manually adds preview images to mod folder

        // Arrange - Create preview folder
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);
        Directory.CreateDirectory(previewFolder);

        // Initially no previews
        var initialPreviews = await _service.GetPreviewPathsAsync(_testSha);
        initialPreviews.Should().BeEmpty();

        // Act - User manually adds preview images
        File.WriteAllText(Path.Combine(previewFolder, "preview1.png"), "new preview 1");
        File.WriteAllText(Path.Combine(previewFolder, "preview2.jpg"), "new preview 2");

        // Scan again
        var updatedPreviews = await _service.GetPreviewPathsAsync(_testSha);

        // Assert - New previews should be discovered
        updatedPreviews.Should().HaveCount(2);
        updatedPreviews.Should().Contain(p => p.Contains("preview1.png"));
        updatedPreviews.Should().Contain(p => p.Contains("preview2.jpg"));
    }

    [Fact]
    public async Task RealScenario_ClearCache_ShouldRemoveAllImagesForMod()
    {
        // Scenario: Clear all cached images for a mod

        // Arrange - Create thumbnail and multiple previews
        var thumbnailPath = Path.Combine(_testDataPath, "thumbnails", $"{_testSha}.png");
        var previewFolder = Path.Combine(_testDataPath, "previews", _testSha);

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        Directory.CreateDirectory(previewFolder);

        File.WriteAllText(thumbnailPath, "thumbnail");
        File.WriteAllText(Path.Combine(previewFolder, "preview1.png"), "preview 1");
        File.WriteAllText(Path.Combine(previewFolder, "preview2.png"), "preview 2");

        // Verify files exist
        File.Exists(thumbnailPath).Should().BeTrue();
        Directory.Exists(previewFolder).Should().BeTrue();
        (await _service.GetPreviewPathsAsync(_testSha)).Should().HaveCount(2);

        // Act - Clear cache
        var result = await _service.ClearModCacheAsync(_testSha);

        // Assert - Everything should be deleted
        result.Should().BeTrue();
        File.Exists(thumbnailPath).Should().BeFalse();
        Directory.Exists(previewFolder).Should().BeFalse();
        (await _service.GetPreviewPathsAsync(_testSha)).Should().BeEmpty();
    }

    #endregion
}
