using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Comprehensive tests for ModImportService
/// Focuses on import workflow, error handling, edge cases, and data validation
/// </summary>
public class ModImportServiceTests
{
    private readonly Mock<IFileHelper> _mockFileService;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IModRepository> _mockRepository;
    private readonly Mock<IModArchiveService> _mockArchiveService;
    private readonly Mock<IModMetadataService> _mockMetadataService;
    private readonly Mock<IPathValidator> _mockPathValidator;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly ModImportService _service;

    public ModImportServiceTests()
    {
        _mockFileService = new Mock<IFileHelper>();
        _mockHashHelper = new Mock<IHashHelper>();
        _mockImageService = new Mock<IImageService>();
        _mockRepository = new Mock<IModRepository>();
        _mockArchiveService = new Mock<IModArchiveService>();
        _mockMetadataService = new Mock<IModMetadataService>();
        _mockPathValidator = new Mock<IPathValidator>();
        _mockLogger = new Mock<ILogHelper>();
        _mockEventBus = new Mock<IProfileEventBus>();

        _service = new ModImportService(
            _mockFileService.Object,
            _mockHashHelper.Object,
            _mockImageService.Object,
            _mockRepository.Object,
            _mockArchiveService.Object,
            _mockMetadataService.Object,
            _mockPathValidator.Object,
            _mockLogger.Object,
            _mockEventBus.Object
        );
    }

    #region Helper Methods (as suggested by user)

    /// <summary>
    /// Helper to setup successful import workflow mocks
    /// </summary>
    private void SetupSuccessfulImportMocks(string filePath, string sha, ModInfo expectedMod)
    {
        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath));
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(filePath)).ReturnsAsync(sha);
        _mockRepository.Setup(x => x.ExistsAsync(sha)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.CopyArchiveAsync(filePath, sha)).ReturnsAsync("mock-path");
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(sha)).ReturnsAsync(0);
        _mockMetadataService.Setup(x => x.CreateAsync(It.IsAny<CreateModRequest>())).ReturnsAsync(expectedMod);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Helper to setup mocks for existing mod scenario
    /// </summary>
    private void SetupExistingModMocks(string filePath, string sha, ModEntity existingEntity)
    {
        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath));
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(filePath)).ReturnsAsync(sha);
        _mockRepository.Setup(x => x.ExistsAsync(sha)).ReturnsAsync(true);
        _mockRepository.Setup(x => x.GetByIdAsync(sha)).ReturnsAsync(existingEntity);
    }

    #endregion

    #region ImportAsync Tests

    [Fact]
    public async Task ImportAsync_WithValidFile_ShouldImportSuccessfully()
    {
        // Arrange
        var filePath = "C:\\test\\my-mod.7z";
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "my-mod",
            Author = string.Empty,
            Description = string.Empty,
            Type = "7z",
            Grading = "G",
            Tags = new List<string>()
        };

        SetupSuccessfulImportMocks(filePath, sha, expectedMod);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.SHA.Should().Be(sha);
        result.Name.Should().Be("my-mod");
        result.Type.Should().Be("7z");
        result.Category.Should().Be(string.Empty, "new imports should be uncategorized");

        // Verify workflow steps executed
        _mockPathValidator.Verify(x => x.ValidateFileExists(filePath), Times.Once);
        _mockHashHelper.Verify(x => x.CalculateFileSHA256Async(filePath), Times.Once);
        _mockRepository.Verify(x => x.ExistsAsync(sha), Times.Once);
        _mockArchiveService.Verify(x => x.CopyArchiveAsync(filePath, sha), Times.Once);
        _mockMetadataService.Verify(x => x.CreateAsync(It.IsAny<CreateModRequest>()), Times.Once);
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "IMPORTED", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WithFileWithoutExtension_ShouldHandleGracefully()
    {
        // Arrange
        var filePath = "C:\\test\\my-mod";  // No extension
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "my-mod",
            Author = string.Empty,
            Description = string.Empty,
            Type = string.Empty,  // No extension
            Grading = "G",
            Tags = new List<string>()
        };

        SetupSuccessfulImportMocks(filePath, sha, expectedMod);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("my-mod");
        result.Type.Should().Be(string.Empty, "files without extension should have empty type");

        // Verify CreateAsync was called with correct Type
        _mockMetadataService.Verify(x => x.CreateAsync(It.Is<CreateModRequest>(r =>
            r.Type == string.Empty
        )), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenModAlreadyExists_ShouldReturnExistingMod()
    {
        // Arrange
        var filePath = "C:\\test\\my-mod.7z";
        var sha = "abc123";
        var existingEntity = new ModEntity
        {
            SHA = sha,
            Category = "test-category",
            Name = "Existing Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "PG13"
        };

        SetupExistingModMocks(filePath, sha, existingEntity);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.SHA.Should().Be(sha);
        result.Name.Should().Be("Existing Mod", "should return existing mod");
        result.Category.Should().Be("test-category", "should preserve existing category");

        // Verify no import operations were performed
        _mockArchiveService.Verify(x => x.CopyArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never, "should not copy archive for existing mod");
        _mockMetadataService.Verify(x => x.CreateAsync(It.IsAny<CreateModRequest>()), Times.Never, "should not create new mod");
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "IMPORTED", It.IsAny<object>()), Times.Never, "should not emit IMPORTED event");
    }

    [Fact]
    public async Task ImportAsync_WhenFileDoesNotExist_ShouldThrowException()
    {
        // Arrange
        var filePath = "C:\\test\\nonexistent.7z";
        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath))
            .Throws(new FileNotFoundException($"File not found: {filePath}"));

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ImportAsync(filePath));

        // Verify no operations were performed
        _mockHashHelper.Verify(x => x.CalculateFileSHA256Async(It.IsAny<string>()), Times.Never);
        _mockArchiveService.Verify(x => x.CopyArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WhenPreviewAutoImportFails_ShouldContinueImport()
    {
        // Arrange - Auto-import previews throws exception
        var filePath = "C:\\test\\my-mod.7z";
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "my-mod",
            Author = string.Empty,
            Description = string.Empty,
            Type = "7z",
            Grading = "G",
            Tags = new List<string>()
        };

        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath));
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(filePath)).ReturnsAsync(sha);
        _mockRepository.Setup(x => x.ExistsAsync(sha)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.CopyArchiveAsync(filePath, sha)).ReturnsAsync("mock-path");
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(sha))
            .ThrowsAsync(new InvalidOperationException("Cache not found"));  // Fails
        _mockMetadataService.Setup(x => x.CreateAsync(It.IsAny<CreateModRequest>())).ReturnsAsync(expectedMod);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert - Import should succeed despite preview import failure
        result.Should().NotBeNull();
        result!.SHA.Should().Be(sha);

        // Verify import completed
        _mockMetadataService.Verify(x => x.CreateAsync(It.IsAny<CreateModRequest>()), Times.Once, "import should complete despite preview failure");
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "IMPORTED", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenArchiveCopyFails_ShouldThrowAndNotCreateMod()
    {
        // Arrange - Archive copy fails
        var filePath = "C:\\test\\my-mod.7z";
        var sha = "abc123";

        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath));
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(filePath)).ReturnsAsync(sha);
        _mockRepository.Setup(x => x.ExistsAsync(sha)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.CopyArchiveAsync(filePath, sha))
            .ThrowsAsync(new IOException("Disk full"));

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => _service.ImportAsync(filePath));

        // Verify mod was not created
        _mockMetadataService.Verify(x => x.CreateAsync(It.IsAny<CreateModRequest>()), Times.Never, "mod should not be created if archive copy fails");
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "IMPORTED", It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_WithSpecialCharactersInFilename_ShouldSanitizeForName()
    {
        // Arrange
        var filePath = "C:\\test\\[NSFW] My-Mod (v2.0).7z";
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "[NSFW] My-Mod (v2.0)",  // Should preserve filename
            Author = string.Empty,
            Description = string.Empty,
            Type = "7z",
            Grading = "G",
            Tags = new List<string>()
        };

        SetupSuccessfulImportMocks(filePath, sha, expectedMod);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("[NSFW] My-Mod (v2.0)");

        // Verify CreateAsync was called with filename (without extension) as Name
        _mockMetadataService.Verify(x => x.CreateAsync(It.Is<CreateModRequest>(r =>
            r.Name == "[NSFW] My-Mod (v2.0)"
        )), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_ShouldCreateModWithDefaultValues()
    {
        // Arrange
        var filePath = "C:\\test\\my-mod.zip";
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "my-mod",
            Author = string.Empty,
            Description = string.Empty,
            Type = "zip",
            Grading = "G",
            Tags = new List<string>()
        };

        SetupSuccessfulImportMocks(filePath, sha, expectedMod);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();

        // Verify CreateAsync was called with correct default values
        _mockMetadataService.Verify(x => x.CreateAsync(It.Is<CreateModRequest>(r =>
            r.SHA == sha &&
            r.Category == null &&  // Should be null (unclassified)
            r.Name == "my-mod" &&
            r.Author == null &&  // Should be null (empty)
            r.Description == null &&  // Should be null (empty)
            r.Type == "zip" &&
            r.Grading == "G" &&  // Default grading
            r.Tags.Count == 0  // Empty tags
        )), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_WhenPreviewAutoImportFindsImages_ShouldLogCount()
    {
        // Arrange
        var filePath = "C:\\test\\my-mod.7z";
        var sha = "abc123";
        var expectedMod = new ModInfo
        {
            SHA = sha,
            Category = string.Empty,
            Name = "my-mod",
            Author = string.Empty,
            Description = string.Empty,
            Type = "7z",
            Grading = "G",
            Tags = new List<string>()
        };

        _mockPathValidator.Setup(x => x.ValidateFileExists(filePath));
        _mockHashHelper.Setup(x => x.CalculateFileSHA256Async(filePath)).ReturnsAsync(sha);
        _mockRepository.Setup(x => x.ExistsAsync(sha)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.CopyArchiveAsync(filePath, sha)).ReturnsAsync("mock-path");
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(sha)).ReturnsAsync(3);  // Found 3 previews
        _mockMetadataService.Setup(x => x.CreateAsync(It.IsAny<CreateModRequest>())).ReturnsAsync(expectedMod);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.ImportAsync(filePath);

        // Assert
        result.Should().NotBeNull();

        // Verify auto-import was attempted
        _mockImageService.Verify(x => x.TryAutoImportPreviewsFromCacheAsync(sha), Times.Once);
    }

    #endregion

    #region ScanAndImportPreviewsFromFolderAsync Tests

    [Fact]
    public async Task ScanAndImportPreviewsFromFolderAsync_WithValidFolder_ShouldImportPreviews()
    {
        // Arrange
        var sha = "abc123";
        var folderPath = "C:\\test\\previews";

        _mockFileService.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockImageService.Setup(x => x.ScanAndImportFromCacheAsync(sha, folderPath)).ReturnsAsync(5);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var count = await _service.ScanAndImportPreviewsFromFolderAsync(sha, folderPath);

        // Assert
        count.Should().Be(5, "should return number of imported previews");

        _mockImageService.Verify(x => x.ScanAndImportFromCacheAsync(sha, folderPath), Times.Once);
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "PREVIEW_IMPORTED", It.Is<object>(o =>
            o.GetType().GetProperty("sha") != null &&
            o.GetType().GetProperty("source") != null
        )), Times.Once);
    }

    [Fact]
    public async Task ScanAndImportPreviewsFromFolderAsync_WhenFolderDoesNotExist_ShouldReturnZero()
    {
        // Arrange
        var sha = "abc123";
        var folderPath = "C:\\test\\nonexistent";

        _mockFileService.Setup(x => x.DirectoryExists(folderPath)).Returns(false);

        // Act
        var count = await _service.ScanAndImportPreviewsFromFolderAsync(sha, folderPath);

        // Assert
        count.Should().Be(0, "should return 0 when folder doesn't exist");

        // Verify no import was attempted
        _mockImageService.Verify(x => x.ScanAndImportFromCacheAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockEventBus.Verify(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ScanAndImportPreviewsFromFolderAsync_WhenNoImagesFound_ShouldReturnZeroWithoutEvent()
    {
        // Arrange
        var sha = "abc123";
        var folderPath = "C:\\test\\empty";

        _mockFileService.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockImageService.Setup(x => x.ScanAndImportFromCacheAsync(sha, folderPath)).ReturnsAsync(0);

        // Act
        var count = await _service.ScanAndImportPreviewsFromFolderAsync(sha, folderPath);

        // Assert
        count.Should().Be(0, "should return 0 when no images found");

        // Verify event was not emitted when count is 0
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "PREVIEW_IMPORTED", It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ScanAndImportPreviewsFromFolderAsync_WhenScanFails_ShouldReturnZero()
    {
        // Arrange
        var sha = "abc123";
        var folderPath = "C:\\test\\previews";

        _mockFileService.Setup(x => x.DirectoryExists(folderPath)).Returns(true);
        _mockImageService.Setup(x => x.ScanAndImportFromCacheAsync(sha, folderPath))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var count = await _service.ScanAndImportPreviewsFromFolderAsync(sha, folderPath);

        // Assert
        count.Should().Be(0, "should return 0 on error and not propagate exception");

        // Verify exception was caught and logged (not propagated)
        _mockLogger.Verify(x => x.Error(
            It.Is<string>(s => s.Contains("Failed to scan and import")),
            "ModImportService",
            It.IsAny<Exception>()
        ), Times.Once);
    }

    #endregion
}
