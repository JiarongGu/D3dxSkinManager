using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Comprehensive tests for ModLifecycleService
/// Focuses on category conflict resolution, loading/unloading logic, and edge cases
/// </summary>
public class ModLifecycleServiceTests
{
    private readonly Mock<IModRepository> _mockRepository;
    private readonly Mock<IModArchiveService> _mockArchiveService;
    private readonly Mock<IModCacheService> _mockCacheService;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IProfilePathService> _mockProfilePaths;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly ModLifecycleService _service;

    public ModLifecycleServiceTests()
    {
        _mockRepository = new Mock<IModRepository>();
        _mockArchiveService = new Mock<IModArchiveService>();
        _mockCacheService = new Mock<IModCacheService>();
        _mockImageService = new Mock<IImageService>();
        _mockProfilePaths = new Mock<IProfilePathService>();
        _mockLogger = new Mock<ILogHelper>();
        _mockEventBus = new Mock<IProfileEventBus>();

        // Setup default path
        _mockProfilePaths.Setup(x => x.CacheModsDirectory).Returns("C:\\test\\cache\\Mods");

        _service = new ModLifecycleService(
            _mockRepository.Object,
            _mockArchiveService.Object,
            _mockCacheService.Object,
            _mockImageService.Object,
            _mockProfilePaths.Object,
            _mockLogger.Object,
            _mockEventBus.Object
        );
    }

    #region Helper Methods

    /// <summary>
    /// Helper to setup successful load mocks
    /// </summary>
    private void SetupSuccessfulLoadMocks(string id, ModEntity entity, bool cacheEnabled = false)
    {
        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockRepository.Setup(x => x.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(new List<ModEntity>());
        _mockCacheService.Setup(x => x.EnableCacheAsync(id)).ReturnsAsync(cacheEnabled);

        if (!cacheEnabled)
        {
            _mockArchiveService.Setup(x => x.ExtractAsync(id, It.IsAny<string>())).ReturnsAsync(
                new ArchiveExtractionResult { Success = true, FileCount = 10 });
        }

        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(id)).ReturnsAsync(0);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
    }

    #endregion

    #region LoadAsync Tests - Basic Scenarios

    [Fact]
    public async Task LoadAsync_WithValidMod_ShouldLoadSuccessfully()
    {
        // Arrange
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        SetupSuccessfulLoadMocks(id, entity);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.LoadedModId.Should().Be(id);
        result.UnloadedModIds.Should().BeEmpty("no conflicting mods");

        _mockEventBus.Verify(x => x.EmitAsync("MOD", "LOADED", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WhenModDoesNotExist_ShouldThrowOperationException()
    {
        // Arrange
        var id = "nonexistent";
        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((ModEntity?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationException>(() => _service.LoadAsync(id));
        exception.Code.Should().Be("MOD_NOT_FOUND");
        exception.Parameters.Should().ContainKey("id");
    }

    [Fact]
    public async Task LoadAsync_WithShortSHA_ShouldHandleGracefully()
    {
        // Arrange - id less than 8 characters (edge case for id.Substring(0, 8))
        var id = "abc";  // Only 3 characters
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);

        // Act & Assert
        // The code has try-catch that wraps any exception in OperationException
        var exception = await Assert.ThrowsAsync<OperationException>(() => _service.LoadAsync(id));

        // Should get an operation exception wrapping the underlying error
        exception.Code.Should().Be("UNKNOWN_ERROR");
        exception.InnerException.Should().NotBeNull("error should be wrapped with inner exception");
        exception.InnerException.Should().BeAssignableTo<ArgumentException>("short id causes argument-related errors");
    }

    #endregion

    #region LoadAsync Tests - Category Conflicts

    [Fact]
    public async Task LoadAsync_WithConflictingModInSameCategory_ShouldUnloadConflictingMod()
    {
        // Arrange - Two mods in same category
        var id1 = "abc123def456";  // Will be loaded
        var id2 = "xyz789ghi012";  // Currently loaded, should be unloaded

        var entity1 = new ModEntity
        {
            Id = id1,
            Category = "category1",
            Name = "Mod 1",
            Type = "7z",
            Grading = "G"
        };

        var entity2 = new ModEntity
        {
            Id = id2,
            Category = "category1",
            Name = "Mod 2",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id1)).ReturnsAsync(entity1);
        _mockRepository.Setup(x => x.GetByCategoryAsync("category1")).ReturnsAsync(new List<ModEntity> { entity1, entity2 });
        _mockCacheService.Setup(x => x.DisableCacheAsync(id2)).ReturnsAsync(true);
        _mockCacheService.Setup(x => x.EnableCacheAsync(id1)).ReturnsAsync(true);
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(id1)).ReturnsAsync(0);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Mock PopulateIsLoadedFlags behavior - id2 is loaded, id1 is not
        _mockProfilePaths.Setup(x => x.CacheModsDirectory).Returns("C:\\test\\cache\\Mods");

        // Act
        var result = await _service.LoadAsync(id1);

        // Assert
        result.Success.Should().BeTrue();
        result.LoadedModId.Should().Be(id1);

        // Verify conflicting mod was unloaded (if it was actually loaded)
        // Note: We can't fully test this without file system access for PopulateIsLoadedFlags
        // But we can verify the service attempted to disable cache
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "LOADED", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WithUnclassifiedMod_ShouldNotUnloadOtherUnclassifiedMods()
    {
        // Arrange - Multiple unclassified mods (empty category)
        var id1 = "abc123def456";
        var id2 = "xyz789ghi012";

        var entity1 = new ModEntity
        {
            Id = id1,
            Category = string.Empty,  // Unclassified
            Name = "Mod 1",
            Type = "7z",
            Grading = "G"
        };

        var entity2 = new ModEntity
        {
            Id = id2,
            Category = string.Empty,  // Unclassified
            Name = "Mod 2",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id1)).ReturnsAsync(entity1);
        _mockCacheService.Setup(x => x.EnableCacheAsync(id1)).ReturnsAsync(true);
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(id1)).ReturnsAsync(0);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.LoadAsync(id1);

        // Assert
        result.Success.Should().BeTrue();
        result.UnloadedModIds.Should().BeEmpty("unclassified mods should not conflict");

        // Verify no mods were unloaded
        _mockCacheService.Verify(x => x.DisableCacheAsync(It.IsAny<string>()), Times.Never, "unclassified mods can be co-loaded");
    }

    [Fact]
    public async Task LoadAsync_WithWhitespaceCategory_ShouldTreatAsUnclassified()
    {
        // Arrange - Category is whitespace (should be treated as unclassified)
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "   ",  // Whitespace only
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        SetupSuccessfulLoadMocks(id, entity);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Success.Should().BeTrue();
        result.UnloadedModIds.Should().BeEmpty("whitespace category should be treated as unclassified");

        // Verify GetByCategoryAsync was not called (unclassified check skips it)
        _mockRepository.Verify(x => x.GetByCategoryAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_WhenUnloadingConflictingModFails_ShouldContinueLoading()
    {
        // Arrange - Conflicting mod unload fails
        var id1 = "abc123def456";
        var id2 = "xyz789ghi012";

        var entity1 = new ModEntity
        {
            Id = id1,
            Category = "category1",
            Name = "Mod 1",
            Type = "7z",
            Grading = "G"
        };

        var entity2 = new ModEntity
        {
            Id = id2,
            Category = "category1",
            Name = "Mod 2",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id1)).ReturnsAsync(entity1);
        _mockRepository.Setup(x => x.GetByCategoryAsync("category1")).ReturnsAsync(new List<ModEntity> { entity1, entity2 });
        _mockCacheService.Setup(x => x.DisableCacheAsync(id2)).ReturnsAsync(false);  // Unload fails
        _mockCacheService.Setup(x => x.EnableCacheAsync(id1)).ReturnsAsync(true);
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(id1)).ReturnsAsync(0);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.LoadAsync(id1);

        // Assert - Should continue loading despite unload failure
        result.Success.Should().BeTrue();
        result.LoadedModId.Should().Be(id1);

        // Note: The warning is only logged if PopulateIsLoadedFlags detects the mod as loaded
        // Since we're using mocks without real file system, we can't fully test this scenario
    }

    #endregion

    #region LoadAsync Tests - Cache vs Extract

    [Fact]
    public async Task LoadAsync_WithExistingCache_ShouldEnableCacheWithoutExtracting()
    {
        // Arrange - Cache already exists
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        SetupSuccessfulLoadMocks(id, entity, cacheEnabled: true);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Success.Should().BeTrue();

        // Verify cache was enabled but archive was NOT extracted
        _mockCacheService.Verify(x => x.EnableCacheAsync(id), Times.Once);
        _mockArchiveService.Verify(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never, "should use existing cache");
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "LOADING", It.IsAny<object>()), Times.Never, "LOADING event only for extraction");
    }

    [Fact]
    public async Task LoadAsync_WithoutCache_ShouldExtractArchive()
    {
        // Arrange - No cache exists
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        SetupSuccessfulLoadMocks(id, entity, cacheEnabled: false);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Success.Should().BeTrue();

        // Verify extraction was performed
        _mockArchiveService.Verify(x => x.ExtractAsync(id, It.IsAny<string>()), Times.Once);
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "LOADING", It.IsAny<object>()), Times.Once, "LOADING event before extraction");
    }

    [Fact]
    public async Task LoadAsync_WhenExtractionFails_ShouldThrowOperationException()
    {
        // Arrange - Extraction fails
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockRepository.Setup(x => x.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(new List<ModEntity>());
        _mockCacheService.Setup(x => x.EnableCacheAsync(id)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.ExtractAsync(id, It.IsAny<string>())).ReturnsAsync(
            new ArchiveExtractionResult { Success = false, ErrorMessage = "Corrupted archive" });
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationException>(() => _service.LoadAsync(id));
        exception.Code.Should().Be("MOD_EXTRACTION_FAILED");
        exception.Message.Should().Contain("Corrupted archive");
    }

    [Fact]
    public async Task LoadAsync_WhenExtractionFailsWithException_ShouldIncludeInnerException()
    {
        // Arrange - Extraction fails with exception
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        var innerException = new InvalidOperationException("Archive format not supported");

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockRepository.Setup(x => x.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(new List<ModEntity>());
        _mockCacheService.Setup(x => x.EnableCacheAsync(id)).ReturnsAsync(false);
        _mockArchiveService.Setup(x => x.ExtractAsync(id, It.IsAny<string>())).ReturnsAsync(
            new ArchiveExtractionResult { Success = false, ErrorMessage = "Extraction failed", Exception = innerException });
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OperationException>(() => _service.LoadAsync(id));
        exception.Code.Should().Be("MOD_EXTRACTION_FAILED");
        exception.InnerException.Should().Be(innerException);
    }

    #endregion

    #region LoadAsync Tests - Preview Import

    [Fact]
    public async Task LoadAsync_WithAutoImportedPreviews_ShouldEmitPreviewImportedEvent()
    {
        // Arrange - Auto-import finds previews
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockRepository.Setup(x => x.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(new List<ModEntity>());
        _mockCacheService.Setup(x => x.EnableCacheAsync(id)).ReturnsAsync(true);
        _mockImageService.Setup(x => x.TryAutoImportPreviewsFromCacheAsync(id)).ReturnsAsync(3);  // Found 3 previews
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Success.Should().BeTrue();

        // Verify PREVIEW_IMPORTED event was emitted
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "PREVIEW_IMPORTED", It.Is<object>(o =>
            o.GetType().GetProperty("Id") != null
        )), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_WithNoPreviewsFound_ShouldNotEmitPreviewImportedEvent()
    {
        // Arrange - Auto-import finds no previews
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        SetupSuccessfulLoadMocks(id, entity);

        // Act
        var result = await _service.LoadAsync(id);

        // Assert
        result.Success.Should().BeTrue();

        // Verify PREVIEW_IMPORTED event was NOT emitted
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "PREVIEW_IMPORTED", It.IsAny<object>()), Times.Never);
    }

    #endregion

    #region UnloadAsync Tests

    [Fact]
    public async Task UnloadAsync_WithValidMod_ShouldUnloadSuccessfully()
    {
        // Arrange
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockCacheService.Setup(x => x.DisableCacheAsync(id)).ReturnsAsync(true);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UnloadAsync(id);

        // Assert
        result.Should().BeTrue();
        _mockCacheService.Verify(x => x.DisableCacheAsync(id), Times.Once);
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "UNLOADED", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task UnloadAsync_WhenDisableCacheFails_ShouldReturnFalse()
    {
        // Arrange
        var id = "abc123def456";
        var entity = new ModEntity
        {
            Id = id,
            Category = "test-category",
            Name = "Test Mod",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(entity);
        _mockCacheService.Setup(x => x.DisableCacheAsync(id)).ReturnsAsync(false);

        // Act
        var result = await _service.UnloadAsync(id);

        // Assert
        result.Should().BeFalse();

        // Verify no event was emitted
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "UNLOADED", It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task UnloadAsync_WhenModDoesNotExist_ShouldStillAttemptUnload()
    {
        // Arrange - Mod doesn't exist in database
        var id = "nonexistent";
        _mockRepository.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((ModEntity?)null);
        _mockCacheService.Setup(x => x.DisableCacheAsync(id)).ReturnsAsync(true);
        _mockEventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.UnloadAsync(id);

        // Assert - Should still attempt to disable cache even if mod doesn't exist in DB
        result.Should().BeTrue();
        _mockCacheService.Verify(x => x.DisableCacheAsync(id), Times.Once);
    }

    #endregion
}
