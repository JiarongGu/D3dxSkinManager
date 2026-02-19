using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Tests.Modules.Mods;

/// <summary>
/// Tests for classification node move and reorder operations
/// </summary>
public class ClassificationMoveTests
{
    private readonly Mock<IClassificationRepository> _mockRepository;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly ClassificationService _service;

    public ClassificationMoveTests()
    {
        _mockRepository = new Mock<IClassificationRepository>();
        _mockModRepository = new Mock<IModRepository>();
        _service = new ClassificationService(_mockRepository.Object, _mockModRepository.Object);

        // Setup default mock behavior for mod repository
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModInfo>());
    }

    [Fact]
    public async Task MoveNodeAsync_ToNewParent_ShouldUpdateParentId()
    {
        // Arrange
        var nodeId = "child1";
        var newParentId = "parent2";
        _mockRepository.Setup(r => r.MoveNodeAsync(nodeId, newParentId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.MoveNodeAsync(nodeId, newParentId, null);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.MoveNodeAsync(nodeId, newParentId), Times.Once);
    }

    [Fact]
    public async Task MoveNodeAsync_ToRoot_ShouldSetParentIdToNull()
    {
        // Arrange
        var nodeId = "child1";
        _mockRepository.Setup(r => r.MoveNodeAsync(nodeId, null)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.MoveNodeAsync(nodeId, null, null);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.MoveNodeAsync(nodeId, null), Times.Once);
    }

    [Fact]
    public async Task MoveNodeAsync_WithDropPosition_ShouldReorderSiblings()
    {
        // Arrange
        var nodeId = "node1";
        var newParentId = "parent1";
        var dropPosition = 1;

        // Setup: 3 existing children in parent1
        var existingSiblings = new List<ClassificationNode>
        {
            new() { Id = "existing1", Name = "Existing 1", ParentId = newParentId, Priority = 300 },
            new() { Id = "existing2", Name = "Existing 2", ParentId = newParentId, Priority = 200 },
            new() { Id = "existing3", Name = "Existing 3", ParentId = newParentId, Priority = 100 }
        };

        _mockRepository.Setup(r => r.MoveNodeAsync(nodeId, newParentId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetChildrenAsync(newParentId)).ReturnsAsync(existingSiblings);
        _mockRepository.Setup(r => r.ReorderSiblingsAsync(It.IsAny<List<(string nodeId, int priority)>>()))
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.MoveNodeAsync(nodeId, newParentId, dropPosition);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.ReorderSiblingsAsync(It.Is<List<(string nodeId, int priority)>>(updates =>
            updates.Count == 4 && // 3 existing + 1 moved node
            updates.Any(u => u.nodeId == nodeId) // Moved node is included
        )), Times.Once);
    }

    [Fact]
    public async Task MoveNodeAsync_ToPosition0_ShouldMakeNodeFirst()
    {
        // Arrange
        var nodeId = "node1";
        var newParentId = "parent1";
        var dropPosition = 0;

        var existingSiblings = new List<ClassificationNode>
        {
            new() { Id = "existing1", Name = "Existing 1", ParentId = newParentId, Priority = 200 },
            new() { Id = "existing2", Name = "Existing 2", ParentId = newParentId, Priority = 100 }
        };

        _mockRepository.Setup(r => r.MoveNodeAsync(nodeId, newParentId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetChildrenAsync(newParentId)).ReturnsAsync(existingSiblings);

        List<(string nodeId, int priority)>? capturedUpdates = null;
        _mockRepository.Setup(r => r.ReorderSiblingsAsync(It.IsAny<List<(string nodeId, int priority)>>()))
            .Callback<List<(string nodeId, int priority)>>(updates => capturedUpdates = updates)
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.MoveNodeAsync(nodeId, newParentId, dropPosition);

        // Assert
        result.Should().BeTrue();
        capturedUpdates.Should().NotBeNull();
        capturedUpdates!.Count.Should().Be(3);

        // The moved node should have the highest priority
        var movedNodeUpdate = capturedUpdates.First(u => u.nodeId == nodeId);
        var maxPriority = capturedUpdates.Max(u => u.priority);
        movedNodeUpdate.priority.Should().Be(maxPriority);
    }

    [Fact]
    public async Task MoveNodeAsync_ToEndPosition_ShouldMakeNodeLast()
    {
        // Arrange
        var nodeId = "node1";
        var newParentId = "parent1";

        var existingSiblings = new List<ClassificationNode>
        {
            new() { Id = "existing1", Name = "Existing 1", ParentId = newParentId, Priority = 200 },
            new() { Id = "existing2", Name = "Existing 2", ParentId = newParentId, Priority = 100 }
        };

        var dropPosition = existingSiblings.Count; // Drop at end

        _mockRepository.Setup(r => r.MoveNodeAsync(nodeId, newParentId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetChildrenAsync(newParentId)).ReturnsAsync(existingSiblings);

        List<(string nodeId, int priority)>? capturedUpdates = null;
        _mockRepository.Setup(r => r.ReorderSiblingsAsync(It.IsAny<List<(string nodeId, int priority)>>()))
            .Callback<List<(string nodeId, int priority)>>(updates => capturedUpdates = updates)
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.MoveNodeAsync(nodeId, newParentId, dropPosition);

        // Assert
        result.Should().BeTrue();
        capturedUpdates.Should().NotBeNull();
        capturedUpdates!.Count.Should().Be(3);

        // The moved node should have the lowest priority
        var movedNodeUpdate = capturedUpdates.First(u => u.nodeId == nodeId);
        var minPriority = capturedUpdates.Min(u => u.priority);
        movedNodeUpdate.priority.Should().Be(minPriority);
    }

    [Fact]
    public async Task ReorderNodeAsync_ShouldMaintainSameParent()
    {
        // Arrange
        var nodeId = "node2";
        var parentId = "parent1";
        var newPosition = 0;

        var node = new ClassificationNode
        {
            Id = nodeId,
            Name = "Node 2",
            ParentId = parentId,
            Priority = 200
        };

        var siblings = new List<ClassificationNode>
        {
            new() { Id = "node1", Name = "Node 1", ParentId = parentId, Priority = 300 },
            new() { Id = nodeId, Name = "Node 2", ParentId = parentId, Priority = 200 },
            new() { Id = "node3", Name = "Node 3", ParentId = parentId, Priority = 100 }
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(node);
        _mockRepository.Setup(r => r.GetChildrenAsync(parentId)).ReturnsAsync(siblings);
        _mockRepository.Setup(r => r.ReorderSiblingsAsync(It.IsAny<List<(string nodeId, int priority)>>()))
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());

        // Act
        var result = await _service.ReorderNodeAsync(nodeId, newPosition);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.ReorderSiblingsAsync(It.Is<List<(string nodeId, int priority)>>(updates =>
            updates.Count == 3 && // All 3 siblings
            updates.Any(u => u.nodeId == nodeId) // Including the reordered node
        )), Times.Once);
    }
}
