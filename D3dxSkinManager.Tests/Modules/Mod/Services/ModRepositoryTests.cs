using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Integration tests for ModRepository
/// Tests SQLite database operations using in-memory database
/// No file system dependencies - each test gets a fresh in-memory database
/// </summary>
public class ModRepositoryTests
{
    private readonly ModRepository _repository;
    private readonly Mock<IProfilePathService> _mockProfilePathService;

    public ModRepositoryTests()
    {
        // Use shared in-memory SQLite database - no file system access!
        // Using URI filename with cache=shared allows multiple connections to share the same in-memory database
        // This is required because ModRepository opens/closes connections for each operation
        var dbName = $"testdb_{Guid.NewGuid():N}";
        _mockProfilePathService = new Mock<IProfilePathService>();
        _mockProfilePathService.Setup(p => p.ProfileDatabasePath).Returns($"file:{dbName}?mode=memory&cache=shared");

        _repository = new ModRepository(_mockProfilePathService.Object);
    }

    [Fact]
    public async Task InsertAsync_WithValidMod_ShouldInsert()
    {
        // Arrange
        var mod = new ModInfo
        {
            SHA = "abc123",
            Category = "category-1",
            Name = "Test Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "G",
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var result = await _repository.InsertAsync(mod);

        // Assert
        result.Should().NotBeNull();
        result.SHA.Should().Be("abc123");
        result.Name.Should().Be("Test Mod");

        // Verify it was actually inserted
        var retrieved = await _repository.GetByIdAsync("abc123");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Mod");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleMods_ShouldReturnAll()
    {
        // Arrange
        var mod1 = new ModInfo { SHA = "sha1", Category = "cat1", Name = "Mod 1" };
        var mod2 = new ModInfo { SHA = "sha2", Category = "cat2", Name = "Mod 2" };

        await _repository.InsertAsync(mod1);
        await _repository.InsertAsync(mod2);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(m => m.Name == "Mod 1");
        results.Should().Contain(m => m.Name == "Mod 2");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnMod()
    {
        // Arrange
        var mod = new ModInfo { SHA = "test123", Category = "cat1", Name = "Test Mod" };
        await _repository.InsertAsync(mod);

        // Act
        var result = await _repository.GetByIdAsync("test123");

        // Assert
        result.Should().NotBeNull();
        result!.SHA.Should().Be("test123");
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
        var mod = new ModInfo { SHA = "exists123", Category = "cat1", Name = "Test" };
        await _repository.InsertAsync(mod);

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
        var mod = new ModInfo { SHA = "update123", Category = "cat1", Name = "Original Name" };
        await _repository.InsertAsync(mod);

        mod.Name = "Updated Name";
        mod.Description = "New Description";

        // Act
        var result = await _repository.UpdateAsync(mod);

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
        var mod = new ModInfo { SHA = "delete123", Category = "cat1", Name = "To Delete" };
        await _repository.InsertAsync(mod);

        // Act
        var result = await _repository.DeleteAsync("delete123");

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync("delete123");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetByCategoryAsync_ShouldReturnModsInCategory()
    {
        // Arrange
        var mod1 = new ModInfo { SHA = "sha1", Category = "category-1", Name = "Mod 1" };
        var mod2 = new ModInfo { SHA = "sha2", Category = "category-1", Name = "Mod 2" };
        var mod3 = new ModInfo { SHA = "sha3", Category = "category-2", Name = "Mod 3" };

        await _repository.InsertAsync(mod1);
        await _repository.InsertAsync(mod2);
        await _repository.InsertAsync(mod3);

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
        await _repository.InsertAsync(new ModInfo { SHA = "sha1", Category = "cat1", Name = "Mod 1" });
        await _repository.InsertAsync(new ModInfo { SHA = "sha2", Category = "cat2", Name = "Mod 2" });
        await _repository.InsertAsync(new ModInfo { SHA = "sha3", Category = "cat1", Name = "Mod 3" });

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
        await _repository.InsertAsync(new ModInfo { SHA = "sha1", Category = "cat1", Name = "Mod 1", Author = "Author A" });
        await _repository.InsertAsync(new ModInfo { SHA = "sha2", Category = "cat1", Name = "Mod 2", Author = "Author B" });
        await _repository.InsertAsync(new ModInfo { SHA = "sha3", Category = "cat1", Name = "Mod 3", Author = "Author A" });

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
        await _repository.InsertAsync(new ModInfo
        {
            SHA = "sha1",
            Category = "cat1",
            Name = "Mod 1",
            Tags = new List<string> { "action", "adventure" }
        });
        await _repository.InsertAsync(new ModInfo
        {
            SHA = "sha2",
            Category = "cat1",
            Name = "Mod 2",
            Tags = new List<string> { "adventure", "rpg" }
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
