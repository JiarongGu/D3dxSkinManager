using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Mods;

/// <summary>
/// Unit tests for ClassificationService
/// Tests tree building, caching, and search functionality
/// </summary>
public class ClassificationServiceTests
{
    private readonly Mock<IClassificationRepository> _mockRepository;
    private readonly Mock<ImageServerService> _mockImageServer;
    private readonly ClassificationService _service;

    public ClassificationServiceTests()
    {
        _mockRepository = new Mock<IClassificationRepository>();
        _mockImageServer = new Mock<ImageServerService>("test_data_path", 5555);
        _service = new ClassificationService(_mockRepository.Object, _mockImageServer.Object);
    }

    [Fact]
    public async Task GetClassificationTreeAsync_FirstCall_ShouldFetchFromRepository()
    {
        // Arrange
        var nodes = CreateSampleNodes();
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().NotBeEmpty();
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetClassificationTreeAsync_SecondCallWithinCacheExpiry_ShouldReturnCached()
    {
        // Arrange
        var nodes = CreateSampleNodes();
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result1 = await _service.GetClassificationTreeAsync();
        var result2 = await _service.GetClassificationTreeAsync();

        // Assert
        result1.Should().BeSameAs(result2); // Same reference = cached
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetClassificationTreeAsync_ShouldBuildCorrectHierarchy()
    {
        // Arrange - Create a tree: Root -> Child1 -> Grandchild
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "root", Name = "Root", ParentId = null },
            new() { Id = "child1", Name = "Child1", ParentId = "root" },
            new() { Id = "child2", Name = "Child2", ParentId = "root" },
            new() { Id = "grandchild", Name = "Grandchild", ParentId = "child1" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().HaveCount(1); // Only 1 root node
        result[0].Name.Should().Be("Root");
        result[0].Children.Should().HaveCount(2); // Child1 and Child2
        result[0].Children[0].Children.Should().HaveCount(1); // Grandchild under Child1
    }

    [Fact]
    public async Task GetClassificationTreeAsync_WithOrphanNodes_ShouldOnlyIncludeConnectedNodes()
    {
        // Arrange - Create nodes with orphan (child with non-existent parent)
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "root", Name = "Root", ParentId = null },
            new() { Id = "child1", Name = "Child1", ParentId = "root" },
            new() { Id = "orphan", Name = "Orphan", ParentId = "nonexistent" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().HaveCount(1); // Only root
        result[0].Children.Should().HaveCount(1); // Only Child1, not orphan
    }

    [Fact]
    public async Task GetClassificationTreeAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshTreeAsync_ShouldForceReloadFromDatabase()
    {
        // Arrange
        var nodes = CreateSampleNodes();
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act - First load
        await _service.GetClassificationTreeAsync();

        // Act - Force refresh
        var refreshResult = await _service.RefreshTreeAsync();

        // Assert
        refreshResult.Should().BeTrue();
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshTreeAsync_WhenDatabaseFails_ShouldReturnFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.RefreshTreeAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FindClassificationForObjectAsync_WithExactMatch_ShouldReturnFromRepository()
    {
        // Arrange
        var expectedNode = new ClassificationNode { Id = "obj1", Name = "TestObject", ParentId = null };
        _mockRepository.Setup(r => r.GetByNameAsync("TestObject")).ReturnsAsync(expectedNode);

        // Act
        var result = await _service.FindClassificationForObjectAsync("TestObject");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("TestObject");
        _mockRepository.Verify(r => r.GetByNameAsync("TestObject"), Times.Once);
    }

    [Fact]
    public async Task FindClassificationForObjectAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByNameAsync("NonExistent")).ReturnsAsync((ClassificationNode?)null);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.FindClassificationForObjectAsync("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindClassificationForObjectAsync_ShouldSearchTreeRecursively()
    {
        // Arrange
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "root", Name = "Root", ParentId = null },
            new() { Id = "child", Name = "Child", ParentId = "root" },
            new() { Id = "grandchild", Name = "DeepNode", ParentId = "child" }
        };

        _mockRepository.Setup(r => r.GetByNameAsync("DeepNode")).ReturnsAsync((ClassificationNode?)null);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.FindClassificationForObjectAsync("DeepNode");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("DeepNode");
        result.Id.Should().Be("grandchild");
    }

    [Fact]
    public async Task FindClassificationForObjectAsync_WithMultipleMatches_ShouldReturnFirst()
    {
        // Arrange - Create tree with multiple nodes named "Duplicate"
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "root", Name = "Root", ParentId = null },
            new() { Id = "dup1", Name = "Duplicate", ParentId = "root" },
            new() { Id = "dup2", Name = "Duplicate", ParentId = "root" }
        };

        _mockRepository.Setup(r => r.GetByNameAsync("Duplicate")).ReturnsAsync((ClassificationNode?)null);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.FindClassificationForObjectAsync("Duplicate");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Duplicate");
        result.Id.Should().Be("dup1"); // First match
    }

    [Fact]
    public async Task GetClassificationTreeAsync_WithMultipleRoots_ShouldReturnAll()
    {
        // Arrange
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "root1", Name = "Root1", ParentId = null },
            new() { Id = "root2", Name = "Root2", ParentId = null },
            new() { Id = "child1", Name = "Child1", ParentId = "root1" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().HaveCount(2); // Two root nodes
        result[0].Name.Should().Be("Root1");
        result[1].Name.Should().Be("Root2");
        result[0].Children.Should().HaveCount(1);
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassificationTreeAsync_WithDeepHierarchy_ShouldBuildCompleteTree()
    {
        // Arrange - Create a 4-level deep tree
        var nodes = new List<ClassificationNode>
        {
            new() { Id = "l1", Name = "Level1", ParentId = null },
            new() { Id = "l2", Name = "Level2", ParentId = "l1" },
            new() { Id = "l3", Name = "Level3", ParentId = "l2" },
            new() { Id = "l4", Name = "Level4", ParentId = "l3" }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Level1");
        result[0].Children[0].Name.Should().Be("Level2");
        result[0].Children[0].Children[0].Name.Should().Be("Level3");
        result[0].Children[0].Children[0].Children[0].Name.Should().Be("Level4");
    }

    // Helper method to create sample test data
    private List<ClassificationNode> CreateSampleNodes()
    {
        return new List<ClassificationNode>
        {
            new() { Id = "char", Name = "Characters", ParentId = null },
            new() { Id = "weapon", Name = "Weapons", ParentId = null },
            new() { Id = "char1", Name = "Character1", ParentId = "char" },
            new() { Id = "char2", Name = "Character2", ParentId = "char" }
        };
    }
}
