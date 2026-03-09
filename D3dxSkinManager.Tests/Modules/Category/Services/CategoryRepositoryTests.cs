using FluentAssertions;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Category.Services;

/// <summary>
/// Integration tests for CategoryRepository
/// Tests SQLite database operations using in-memory database with migrations
/// No file system dependencies - each test gets a fresh database with schema from migrations
/// </summary>
public class CategoryRepositoryTests : InMemoryDatabaseTestBase
{
    private readonly CategoryRepository _repository;

    public CategoryRepositoryTests()
    {
        _repository = new CategoryRepository(MockProfilePathService.Object);
    }

    [Fact]
    public async Task InsertAsync_WithValidCategory_ShouldInsert()
    {
        // Arrange
        var category = new CategoryInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Category",
            Priority = 100,
            Description = "Test Description"
        };

        // Act
        var result = await _repository.InsertAsync(category);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(category.Id);
        result.Name.Should().Be(category.Name);

        // Verify it was actually inserted
        var retrieved = await _repository.GetByIdAsync(category.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Category");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleCategories_ShouldReturnAll()
    {
        // Arrange
        var category1 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Category 1", Priority = 100 };
        var category2 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Category 2", Priority = 50 };

        await _repository.InsertAsync(category1);
        await _repository.InsertAsync(category2);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.Name == "Category 1");
        results.Should().Contain(c => c.Name == "Category 2");
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnCategory()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Test", Priority = 100 };
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.GetByIdAsync(category.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(category.Id);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existing-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ShouldReturnCategory()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Unique Name", Priority = 100 };
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.GetByNameAsync("Unique Name");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Unique Name");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdate()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Original", Priority = 100 };
        await _repository.InsertAsync(category);

        category.Name = "Updated";
        category.Description = "New Description";

        // Act
        var result = await _repository.UpdateAsync(category);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(category.Id);
        retrieved!.Name.Should().Be("Updated");
        retrieved.Description.Should().Be("New Description");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_ShouldDelete()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "To Delete", Priority = 100 };
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.DeleteAsync(category.Id);

        // Assert
        result.Should().BeTrue();

        var retrieved = await _repository.GetByIdAsync(category.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetChildrenAsync_WithParentId_ShouldReturnOnlyChildren()
    {
        // Arrange
        var parentId = Guid.NewGuid().ToString();
        var parent = new CategoryInfo { Id = parentId, Name = "Parent", Priority = 100 };
        var child1 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child1", ParentId = parentId, Priority = 50 };
        var child2 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child2", ParentId = parentId, Priority = 30 };
        var otherCategory = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Other", Priority = 100 };

        await _repository.InsertAsync(parent);
        await _repository.InsertAsync(child1);
        await _repository.InsertAsync(child2);
        await _repository.InsertAsync(otherCategory);

        // Act
        var children = await _repository.GetChildrenAsync(parentId);

        // Assert
        children.Should().HaveCount(2);
        children.Should().Contain(c => c.Name == "Child1");
        children.Should().Contain(c => c.Name == "Child2");
        children.Should().NotContain(c => c.Name == "Other");
    }

    [Fact]
    public async Task GetChildrenAsync_WithNullParentId_ShouldReturnRootCategories()
    {
        // Arrange
        var root1 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Root1", Priority = 100 };
        var root2 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Root2", Priority = 50 };
        var child = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child", ParentId = root1.Id, Priority = 30 };

        await _repository.InsertAsync(root1);
        await _repository.InsertAsync(root2);
        await _repository.InsertAsync(child);

        // Act
        var roots = await _repository.GetChildrenAsync(null);

        // Assert
        roots.Should().HaveCount(2);
        roots.Should().Contain(c => c.Name == "Root1");
        roots.Should().Contain(c => c.Name == "Root2");
        roots.Should().NotContain(c => c.Name == "Child");
    }

    [Fact]
    public async Task MoveCategoryAsync_ShouldUpdateParentId()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Test", Priority = 100 };
        var newParentId = Guid.NewGuid().ToString();
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.MoveCategoryAsync(category.Id, newParentId);

        // Assert
        result.Should().BeTrue();

        var updated = await _repository.GetByIdAsync(category.Id);
        updated!.ParentId.Should().Be(newParentId);
    }

    [Fact]
    public async Task UpdatePriorityAsync_ShouldUpdatePriority()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Test", Priority = 100 };
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.UpdatePriorityAsync(category.Id, 200);

        // Assert
        result.Should().BeTrue();

        var updated = await _repository.GetByIdAsync(category.Id);
        updated!.Priority.Should().Be(200);
    }

    [Fact]
    public async Task ExistsAsync_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var category = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Test", Priority = 100 };
        await _repository.InsertAsync(category);

        // Act
        var result = await _repository.ExistsAsync(category.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingId_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsAsync("non-existing-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderSiblingsAsync_ShouldUpdateAllPriorities()
    {
        // Arrange
        var parent = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Parent", Priority = 100 };
        var child1 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child1", ParentId = parent.Id, Priority = 100 };
        var child2 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child2", ParentId = parent.Id, Priority = 50 };
        var child3 = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = "Child3", ParentId = parent.Id, Priority = 25 };

        await _repository.InsertAsync(parent);
        await _repository.InsertAsync(child1);
        await _repository.InsertAsync(child2);
        await _repository.InsertAsync(child3);

        var updates = new List<(string categoryId, int priority)>
        {
            (child1.Id, 300),
            (child2.Id, 200),
            (child3.Id, 100)
        };

        // Act
        var result = await _repository.ReorderSiblingsAsync(updates);

        // Assert
        result.Should().BeTrue();

        var updated1 = await _repository.GetByIdAsync(child1.Id);
        var updated2 = await _repository.GetByIdAsync(child2.Id);
        var updated3 = await _repository.GetByIdAsync(child3.Id);

        updated1!.Priority.Should().Be(300);
        updated2!.Priority.Should().Be(200);
        updated3!.Priority.Should().Be(100);
    }
}
