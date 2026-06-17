using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Comprehensive tests for ModMetadataService
/// Focuses on NULL handling, validation, partial updates, and edge cases
/// Tests the impedance mismatch between persistence (nullable) and domain (non-nullable) layers
/// </summary>
public class ModMetadataServiceTests
{
    private readonly Mock<IModRepository> _mockRepository;
    private readonly Mock<IModEnrichmentService> _mockEnrichmentService;
    private readonly Mock<IModLifecycleService> _mockLifecycleService;
    private readonly Mock<IModDeletionService> _mockDeletionService;
    private readonly Mock<IModQueryService> _mockQueryService;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly ModMetadataService _service;

    public ModMetadataServiceTests()
    {
        _mockRepository = new Mock<IModRepository>();
        _mockEnrichmentService = new Mock<IModEnrichmentService>();
        _mockLifecycleService = new Mock<IModLifecycleService>();
        _mockDeletionService = new Mock<IModDeletionService>();
        _mockQueryService = new Mock<IModQueryService>();
        _mockLogger = new Mock<ILogHelper>();
        _mockEventBus = new Mock<IProfileEventBus>();

        _service = new ModMetadataService(
            _mockRepository.Object,
            _mockEnrichmentService.Object,
            _mockLifecycleService.Object,
            _mockDeletionService.Object,
            _mockQueryService.Object,
            _mockLogger.Object,
            _mockEventBus.Object,
            Mock.Of<D3dxSkinManager.Modules.Core.Services.IProcessRegistry>()
        );
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithNullCategory_ShouldConvertToEmptyString()
    {
        // Arrange - NULL category (unclassified mod)
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = null,  // NULL means unclassified
            Name = "Test Mod",
            Author = "Test Author",
            Description = "Test Description"
        };

        _mockRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(x => x.InsertAsync(It.IsAny<ModEntity>())).ReturnsAsync((ModEntity e) => e);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert - Domain layer should have empty string, not null
        result.Category.Should().Be(string.Empty, "NULL category should become empty string in domain layer");
        result.Name.Should().Be("Test Mod");
        result.Author.Should().Be("Test Author");
        result.Description.Should().Be("Test Description");

        // Verify repository was called
        _mockRepository.Verify(x => x.InsertAsync(It.Is<ModEntity>(e =>
            e.Category == string.Empty
        )), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_ShouldConvertToEmptyStrings()
    {
        // Arrange - All optional fields are NULL
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = "test-category",
            Name = "Test Mod",
            Author = null,  // NULL optional field
            Description = null  // NULL optional field
        };

        _mockRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(x => x.InsertAsync(It.IsAny<ModEntity>())).ReturnsAsync((ModEntity e) => e);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert - Domain layer converts NULL to empty string
        result.Author.Should().Be(string.Empty, "NULL author should become empty string");
        result.Description.Should().Be(string.Empty, "NULL description should become empty string");
        result.Tags.Should().BeEmpty("NULL tags should become empty list");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyStringFields_ShouldPreserveEmptyStrings()
    {
        // Arrange - User explicitly provides empty strings
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = string.Empty,  // Explicitly empty
            Name = "Test Mod",
            Author = string.Empty,  // Explicitly empty
            Description = string.Empty  // Explicitly empty
        };

        _mockRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(x => x.InsertAsync(It.IsAny<ModEntity>())).ReturnsAsync((ModEntity e) => e);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert - Empty strings should be preserved
        result.Category.Should().Be(string.Empty);
        result.Author.Should().Be(string.Empty);
        result.Description.Should().Be(string.Empty);
    }

    [Fact]
    public async Task CreateAsync_WithNullSHA_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = null!,  // Invalid - id is required
            Category = "test-category",
            Name = "Test Mod"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithEmptySHA_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "   ",  // Whitespace-only is invalid
            Category = "test-category",
            Name = "Test Mod"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = "test-category",
            Name = null!  // Invalid - Name is required
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = "test-category",
            Name = "   "  // Whitespace-only is invalid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WhenModAlreadyExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "existing-id",
            Category = "test-category",
            Name = "Test Mod"
        };

        _mockRepository.Setup(x => x.ExistsAsync("existing-id")).ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
        exception.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCallRepositoryAndReturnMod()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = "test-category",
            Name = "Test Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "PG13",
            Tags = new List<string> { "tag1", "tag2" }
        };

        _mockRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(x => x.InsertAsync(It.IsAny<ModEntity>())).ReturnsAsync((ModEntity e) => e);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Id.Should().Be("test-id");
        result.Category.Should().Be("test-category");
        result.Name.Should().Be("Test Mod");
        result.Author.Should().Be("Test Author");
        result.Description.Should().Be("Test Description");
        result.Type.Should().Be("7z");
        result.Grading.Should().Be("PG13");
        result.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });

        _mockRepository.Verify(x => x.InsertAsync(It.IsAny<ModEntity>()), Times.Once);
    }

    #endregion

    #region GetOrCreateAsync Tests

    [Fact]
    public async Task GetOrCreateAsync_WhenModExists_ShouldReturnExistingMod()
    {
        // Arrange
        var existingEntity = new ModEntity
        {
            Id = "existing-id",
            Category = "test-category",
            Name = "Existing Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync("existing-id")).ReturnsAsync(existingEntity);

        var request = new CreateModRequest
        {
            Id = "existing-id",
            Category = "different-category",  // Different values
            Name = "Different Name"
        };

        // Act
        var result = await _service.GetOrCreateAsync("existing-id", request);

        // Assert - Should return existing mod with original values, not request values
        result.Should().NotBeNull();
        result!.Name.Should().Be("Existing Mod", "should return existing mod, not create new");
        result.Category.Should().Be("test-category", "should return existing mod's category");

        _mockRepository.Verify(x => x.InsertAsync(It.IsAny<ModEntity>()), Times.Never, "should not create new mod");
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenModDoesNotExist_ShouldCreateNewMod()
    {
        // Arrange
        _mockRepository.Setup(x => x.GetByIdAsync("new-id")).ReturnsAsync((ModEntity?)null);
        _mockRepository.Setup(x => x.ExistsAsync("new-id")).ReturnsAsync(false);
        _mockRepository.Setup(x => x.InsertAsync(It.IsAny<ModEntity>())).ReturnsAsync((ModEntity e) => e);

        var request = new CreateModRequest
        {
            Id = "new-id",
            Category = "test-category",
            Name = "New Mod"
        };

        // Act
        var result = await _service.GetOrCreateAsync("new-id", request);

        // Assert - Should create new mod
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Mod");
        result.Category.Should().Be("test-category");

        _mockRepository.Verify(x => x.InsertAsync(It.IsAny<ModEntity>()), Times.Once, "should create new mod");
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNullSHA_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new CreateModRequest
        {
            Id = "test-id",
            Category = "test-category",
            Name = "Test Mod"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetOrCreateAsync(null!, request));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithExistingModHavingNullFields_ShouldConvertToEmptyStrings()
    {
        // Arrange - Existing entity with NULL fields (from database)
        var existingEntity = new ModEntity
        {
            Id = "existing-id",
            Category = "test-category",
            Name = "Existing Mod",
            Author = null,  // NULL in database
            Description = null,  // NULL in database
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync("existing-id")).ReturnsAsync(existingEntity);

        var request = new CreateModRequest
        {
            Id = "existing-id",
            Category = "test-category",
            Name = "Test Mod"
        };

        // Act
        var result = await _service.GetOrCreateAsync("existing-id", request);

        // Assert - Mapper should convert NULL to empty string when converting entity to domain
        result.Should().NotBeNull();
        result!.Author.Should().Be(string.Empty, "NULL from database should become empty string in domain");
        result.Description.Should().Be(string.Empty, "NULL from database should become empty string in domain");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithPartialUpdate_ShouldOnlyUpdateSpecifiedFields()
    {
        // Arrange - Existing mod with all fields populated
        var existingEntity = new ModEntity
        {
            Id = "test-id",
            Category = "original-category",
            Name = "Original Name",
            Author = "Original Author",
            Description = "Original Description",
            Tags = "[\"original-tag\"]",
            Grading = "G",
            Type = "7z",
            DisablePreview = false
        };

        _mockRepository.Setup(x => x.GetByIdAsync("test-id")).ReturnsAsync(existingEntity);
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<ModEntity>())).ReturnsAsync(true);

        // Update only Name and Author, leave others unchanged
        var request = new UpdateModMetadataRequest
        {
            Name = "Updated Name",
            Author = "Updated Author",
            // Description, Tags, Grading, DisablePreview are null - should not be updated
            Description = null,
            Tags = null,
            Grading = null,
            DisablePreview = null
        };

        // Act
        var result = await _service.UpdateAsync("test-id", request);

        // Assert - Only specified fields should be updated
        result.Name.Should().Be("Updated Name", "Name was specified in update");
        result.Author.Should().Be("Updated Author", "Author was specified in update");
        result.Description.Should().Be("Original Description", "Description was not specified, should remain unchanged");
        result.Grading.Should().Be("G", "Grading was not specified, should remain unchanged");
        result.DisablePreview.Should().BeFalse("DisablePreview was not specified, should remain unchanged");

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<ModEntity>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyStringAuthor_ShouldUpdateToEmptyString()
    {
        // Arrange - Mod with author
        var existingEntity = new ModEntity
        {
            Id = "test-id",
            Category = "test-category",
            Name = "Test Mod",
            Author = "Original Author",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync("test-id")).ReturnsAsync(existingEntity);
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<ModEntity>())).ReturnsAsync(true);

        // Update author to empty string (clearing the field)
        var request = new UpdateModMetadataRequest
        {
            Author = string.Empty  // Explicitly clearing author
        };

        // Act
        var result = await _service.UpdateAsync("test-id", request);

        // Assert - Author should be empty string
        result.Author.Should().Be(string.Empty, "user explicitly cleared author field");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentMod_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockRepository.Setup(x => x.GetByIdAsync("nonexistent-id")).ReturnsAsync((ModEntity?)null);

        var request = new UpdateModMetadataRequest
        {
            Name = "Updated Name"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync("nonexistent-id", request));
        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateAsync_WithNullSHA_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new UpdateModMetadataRequest
        {
            Name = "Updated Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(null!, request));
    }

    [Fact]
    public async Task UpdateAsync_ShouldEmitMetadataUpdatedEvent()
    {
        // Arrange
        var existingEntity = new ModEntity
        {
            Id = "test-id",
            Category = "test-category",
            Name = "Original Name",
            Type = "7z",
            Grading = "G"
        };

        _mockRepository.Setup(x => x.GetByIdAsync("test-id")).ReturnsAsync(existingEntity);
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<ModEntity>())).ReturnsAsync(true);

        var request = new UpdateModMetadataRequest
        {
            Name = "Updated Name"
        };

        // Act
        await _service.UpdateAsync("test-id", request);

        // Assert - Event should be emitted
        _mockEventBus.Verify(x => x.EmitAsync(
            "MOD",
            "METADATA_UPDATED",
            It.Is<object>(o => o.GetType().GetProperty("id") != null)
        ), Times.Once);
    }

    #endregion

    #region BatchUpdateAsync Tests

    [Fact]
    public async Task BatchUpdateAsync_WithMultipleMods_ShouldUpdateAllSuccessfully()
    {
        // Arrange
        var mod1 = new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Type = "7z", Grading = "G" };
        var mod2 = new ModEntity { Id = "id2", Category = "cat2", Name = "Mod 2", Type = "7z", Grading = "G" };

        _mockRepository.Setup(x => x.GetByIdAsync("id1")).ReturnsAsync(mod1);
        _mockRepository.Setup(x => x.GetByIdAsync("id2")).ReturnsAsync(mod2);
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<ModEntity>())).ReturnsAsync(true);

        var updates = new Dictionary<string, UpdateModMetadataRequest>
        {
            { "id1", new UpdateModMetadataRequest { Name = "Updated Mod 1" } },
            { "id2", new UpdateModMetadataRequest { Name = "Updated Mod 2" } }
        };

        // Act
        var count = await _service.BatchUpdateAsync(updates);

        // Assert
        count.Should().Be(2, "both mods should be updated");
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<ModEntity>()), Times.Exactly(2));
        _mockEventBus.Verify(x => x.EmitAsync("MOD", "METADATA_UPDATED", It.IsAny<object>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BatchUpdateAsync_WithSomeNonExistentMods_ShouldOnlyUpdateExisting()
    {
        // Arrange
        var mod1 = new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Type = "7z", Grading = "G" };

        _mockRepository.Setup(x => x.GetByIdAsync("id1")).ReturnsAsync(mod1);
        _mockRepository.Setup(x => x.GetByIdAsync("id2")).ReturnsAsync((ModEntity?)null);  // Doesn't exist
        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<ModEntity>())).ReturnsAsync(true);

        var updates = new Dictionary<string, UpdateModMetadataRequest>
        {
            { "id1", new UpdateModMetadataRequest { Name = "Updated Mod 1" } },
            { "id2", new UpdateModMetadataRequest { Name = "Updated Mod 2" } }  // Won't be updated
        };

        // Act
        var count = await _service.BatchUpdateAsync(updates);

        // Assert
        count.Should().Be(1, "only existing mod should be updated");
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<ModEntity>()), Times.Once);
    }

    [Fact]
    public async Task BatchUpdateAsync_WithUpdateFailure_ShouldContinueWithRemainingMods()
    {
        // Arrange
        var mod1 = new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Type = "7z", Grading = "G" };
        var mod2 = new ModEntity { Id = "id2", Category = "cat2", Name = "Mod 2", Type = "7z", Grading = "G" };

        _mockRepository.Setup(x => x.GetByIdAsync("id1")).ReturnsAsync(mod1);
        _mockRepository.Setup(x => x.GetByIdAsync("id2")).ReturnsAsync(mod2);

        // First update fails, second succeeds
        _mockRepository.SetupSequence(x => x.UpdateAsync(It.IsAny<ModEntity>()))
            .ThrowsAsync(new InvalidOperationException("Database error"))
            .ReturnsAsync(true);

        var updates = new Dictionary<string, UpdateModMetadataRequest>
        {
            { "id1", new UpdateModMetadataRequest { Name = "Updated Mod 1" } },
            { "id2", new UpdateModMetadataRequest { Name = "Updated Mod 2" } }
        };

        // Act
        var count = await _service.BatchUpdateAsync(updates);

        // Assert - Should continue despite first failure
        count.Should().Be(1, "second mod should be updated despite first failure");
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<ModEntity>()), Times.Exactly(2));
    }

    #endregion
}
