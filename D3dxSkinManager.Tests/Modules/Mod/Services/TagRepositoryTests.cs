using System;
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
/// Integration tests for TagRepository
/// Tests SQLite database operations using in-memory database
/// No file system dependencies - each test gets a fresh in-memory database
/// </summary>
public class TagRepositoryTests
{
    private readonly TagRepository _repository;
    private readonly Mock<IProfilePathService> _mockProfilePathService;

    public TagRepositoryTests()
    {
        // Use shared in-memory SQLite database - no file system access!
        var dbName = $"testdb_{Guid.NewGuid():N}";
        _mockProfilePathService = new Mock<IProfilePathService>();
        _mockProfilePathService.Setup(p => p.ProfileDatabasePath).Returns($"file:{dbName}?mode=memory&cache=shared");

        _repository = new TagRepository(_mockProfilePathService.Object);
    }

    [Fact]
    public async Task UpsertAsync_WithNewTag_ShouldInsert()
    {
        // Arrange
        var tag = new Tag
        {
            Name = "action",
            Color = "#1890ff"
        };

        // Act
        var result = await _repository.UpsertAsync(tag);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByNameAsync("action");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("action");
        retrieved.Color.Should().Be("#1890ff");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingTag_ShouldUpdate()
    {
        // Arrange
        var tag = new Tag { Name = "rpg", Color = "#ff0000" };
        await _repository.UpsertAsync(tag);

        // Act - Update the color
        tag.Color = "#00ff00";
        var result = await _repository.UpsertAsync(tag);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByNameAsync("rpg");
        retrieved!.Color.Should().Be("#00ff00");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleTags_ShouldReturnAll()
    {
        // Arrange
        await _repository.UpsertAsync(new Tag { Name = "action", Color = "#ff0000" });
        await _repository.UpsertAsync(new Tag { Name = "adventure", Color = "#00ff00" });
        await _repository.UpsertAsync(new Tag { Name = "rpg", Color = "#0000ff" });

        // Act
        var tags = await _repository.GetAllAsync();

        // Assert
        tags.Should().HaveCount(3);
        tags.Should().Contain(t => t.Name == "action");
        tags.Should().Contain(t => t.Name == "adventure");
        tags.Should().Contain(t => t.Name == "rpg");
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingTag_ShouldReturnTag()
    {
        // Arrange
        var tag = new Tag { Name = "strategy", Color = "#1890ff" };
        await _repository.UpsertAsync(tag);

        // Act
        var result = await _repository.GetByNameAsync("strategy");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("strategy");
        result.Color.Should().Be("#1890ff");
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistingTag_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingTag_ShouldDelete()
    {
        // Arrange
        var tag = new Tag { Name = "delete-me", Color = "#ff0000" };
        await _repository.UpsertAsync(tag);

        // Act
        var result = await _repository.DeleteAsync("delete-me");

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByNameAsync("delete-me");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingTag_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.DeleteAsync("non-existing");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingTags()
    {
        // Arrange
        await _repository.UpsertAsync(new Tag { Name = "action", Color = "#ff0000" });
        await _repository.UpsertAsync(new Tag { Name = "action-rpg", Color = "#00ff00" });
        await _repository.UpsertAsync(new Tag { Name = "adventure", Color = "#0000ff" });
        await _repository.UpsertAsync(new Tag { Name = "strategy", Color = "#ffff00" });

        // Act - Search for "act" should match "action" and "action-rpg"
        var results = await _repository.SearchAsync("act");

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(t => t.Name == "action");
        results.Should().Contain(t => t.Name == "action-rpg");
        results.Should().NotContain(t => t.Name == "adventure");
        results.Should().NotContain(t => t.Name == "strategy");
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ShouldReturnMatches()
    {
        // Arrange
        await _repository.UpsertAsync(new Tag { Name = "Action", Color = "#ff0000" });
        await _repository.UpsertAsync(new Tag { Name = "ADVENTURE", Color = "#00ff00" });

        // Act - Search with different case
        var results = await _repository.SearchAsync("action");

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Action");
    }

    [Fact]
    public async Task SearchAsync_WithEmptyString_ShouldReturnAllTags()
    {
        // Arrange
        await _repository.UpsertAsync(new Tag { Name = "tag1", Color = "#ff0000" });
        await _repository.UpsertAsync(new Tag { Name = "tag2", Color = "#00ff00" });
        await _repository.UpsertAsync(new Tag { Name = "tag3", Color = "#0000ff" });

        // Act
        var results = await _repository.SearchAsync("");

        // Assert
        results.Should().HaveCount(3);
    }
}
