using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Integration tests for ModRepository
/// Tests SQLite database operations using in-memory database with migrations
/// No file system dependencies - each test gets a fresh database with schema from migrations
/// NOTE: Repository now works with ModEntity (database layer), not ModInfo (domain layer)
/// </summary>
public class ModRepositoryTests : InMemoryDatabaseTestBase
{
    private readonly ModRepository _repository;

    public ModRepositoryTests()
    {
        MockProfilePathService.Setup(p => p.CacheModsDirectory).Returns("C:\\cache\\mods");
        _repository = new ModRepository(MockProfilePathService.Object, MockLogger.Object);
    }

    [Fact]
    public async Task InsertAsync_WithValidEntity_ShouldInsert()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "abc123",
            Category = "category-1",
            Name = "Test Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "G",
            Tags = "[\"tag1\",\"tag2\"]"  // JSON string
        };

        // Act
        var result = await _repository.InsertAsync(entity);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("abc123");
        result.Name.Should().Be("Test Mod");

        // Verify it was actually inserted
        var retrieved = await _repository.GetByIdAsync("abc123");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Mod");
        retrieved.Tags.Should().Be("[\"tag1\",\"tag2\"]");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleEntities_ShouldReturnAll()
    {
        // Arrange
        var entity1 = new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Tags = "[]" };
        var entity2 = new ModEntity { Id = "id2", Category = "cat2", Name = "Mod 2", Tags = "[]" };

        await _repository.InsertAsync(entity1);
        await _repository.InsertAsync(entity2);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(m => m.Name == "Mod 1");
        results.Should().Contain(m => m.Name == "Mod 2");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnEntity()
    {
        // Arrange
        var entity = new ModEntity { Id = "test123", Category = "cat1", Name = "Test Mod", Tags = "[]" };
        await _repository.InsertAsync(entity);

        // Act
        var result = await _repository.GetByIdAsync("test123");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test123");
        result.Name.Should().Be("Test Mod");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var entity = new ModEntity { Id = "exists123", Category = "cat1", Name = "Test", Tags = "[]" };
        await _repository.InsertAsync(entity);

        // Act
        var result = await _repository.ExistsAsync("exists123");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingId_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsAsync("non-existing");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdate()
    {
        // Arrange
        var entity = new ModEntity { Id = "update123", Category = "cat1", Name = "Original Name", Tags = "[]" };
        await _repository.InsertAsync(entity);

        entity.Name = "Updated Name";
        entity.Description = "New Description";

        // Act
        var result = await _repository.UpdateAsync(entity);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync("update123");
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Description.Should().Be("New Description");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_ShouldDelete()
    {
        // Arrange
        var entity = new ModEntity { Id = "delete123", Category = "cat1", Name = "To Delete", Tags = "[]" };
        await _repository.InsertAsync(entity);

        // Act
        var result = await _repository.DeleteAsync("delete123");

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync("delete123");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetByCategoryAsync_ShouldReturnEntitiesInCategory()
    {
        // Arrange
        var entity1 = new ModEntity { Id = "id1", Category = "category-1", Name = "Mod 1", Tags = "[]" };
        var entity2 = new ModEntity { Id = "id2", Category = "category-1", Name = "Mod 2", Tags = "[]" };
        var entity3 = new ModEntity { Id = "id3", Category = "category-2", Name = "Mod 3", Tags = "[]" };

        await _repository.InsertAsync(entity1);
        await _repository.InsertAsync(entity2);
        await _repository.InsertAsync(entity3);

        // Act
        var results = await _repository.GetByCategoryAsync("category-1");

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(m => m.Name == "Mod 1");
        results.Should().Contain(m => m.Name == "Mod 2");
        results.Should().NotContain(m => m.Name == "Mod 3");
    }

    [Fact]
    public async Task GetDistinctCategoriesAsync_ShouldReturnUniqueCategories()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Tags = "[]" });
        await _repository.InsertAsync(new ModEntity { Id = "id2", Category = "cat2", Name = "Mod 2", Tags = "[]" });
        await _repository.InsertAsync(new ModEntity { Id = "id3", Category = "cat1", Name = "Mod 3", Tags = "[]" });

        // Act
        var categories = await _repository.GetDistinctCategoriesAsync();

        // Assert
        categories.Should().HaveCount(2);
        categories.Should().Contain("cat1");
        categories.Should().Contain("cat2");
    }

    [Fact]
    public async Task GetDistinctAuthorsAsync_ShouldReturnUniqueAuthors()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity { Id = "id1", Category = "cat1", Name = "Mod 1", Author = "Author A", Tags = "[]" });
        await _repository.InsertAsync(new ModEntity { Id = "id2", Category = "cat1", Name = "Mod 2", Author = "Author B", Tags = "[]" });
        await _repository.InsertAsync(new ModEntity { Id = "id3", Category = "cat1", Name = "Mod 3", Author = "Author A", Tags = "[]" });

        // Act
        var authors = await _repository.GetDistinctAuthorsAsync();

        // Assert
        authors.Should().HaveCount(2);
        authors.Should().Contain("Author A");
        authors.Should().Contain("Author B");
    }


    [Fact]
    public async Task GetAllTagsAsync_ShouldReturnAllUniqueTags()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity
        {
            Id = "id1",
            Category = "cat1",
            Name = "Mod 1",
            Tags = "[\"action\",\"adventure\"]"
        });
        await _repository.InsertAsync(new ModEntity
        {
            Id = "id2",
            Category = "cat1",
            Name = "Mod 2",
            Tags = "[\"adventure\",\"rpg\"]"
        });

        // Act
        var tags = await _repository.GetAllTagsAsync();

        // Assert
        tags.Should().HaveCount(3);
        tags.Should().Contain("action");
        tags.Should().Contain("adventure");
        tags.Should().Contain("rpg");
    }
}
