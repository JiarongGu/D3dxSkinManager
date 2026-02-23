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
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Mods;

/// <summary>
/// Unit tests for ClassificationService GUID-based operations
/// Tests GUID generation, stable IDs, name uniqueness, and update operations
/// </summary>
public class ClassificationServiceGuidTests
{
    private readonly Mock<IClassificationRepository> _mockRepository = new();
    private readonly Mock<IModRepository> _mockModRepository = new();
    private readonly Mock<IPathHelper> _mockPathHelper = new();
    private readonly Mock<IFileTransferService> _mockFileTransferService = new();
    private readonly Mock<IProfilePathService> _mockProfilePathService = new();
    private readonly ClassificationService _service;

    public ClassificationServiceGuidTests()
    {
        _service = new ClassificationService(
            _mockRepository.Object,
            _mockModRepository.Object,
            _mockPathHelper.Object,
            _mockFileTransferService.Object,
            _mockProfilePathService.Object
        );

        // Setup default mock behavior
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModInfo>());
        _mockProfilePathService.Setup(p => p.ThumbnailsDirectory).Returns("thumbnails");
    }

    #region Node Creation with GUID Tests

    [Fact]
    public async Task CreateNodeAsync_ShouldGenerateGUID_NotUseProvidedNodeId()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(new ClassificationNode());

        // Act
        var result = await _service.CreateNodeAsync(
            "old-path-based-id", // This should be ignored
            "TestNode",
            null,
            100,
            "Test Description"
        );

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBe("old-path-based-id");

        // Check if it's a valid GUID
        Guid.TryParse(result.Id, out var guid).Should().BeTrue();
        result.Name.Should().Be("TestNode");
    }

    [Fact]
    public async Task CreateNodeAsync_ShouldEnsureNameUniquenessAtSameLevel()
    {
        // Arrange - existing nodes with same name at same level
        var existingNodes = new List<ClassificationNode>
        {
            new() { Id = Guid.NewGuid().ToString(), Name = "DuplicateName", ParentId = null }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(existingNodes);

        // Act - try to create another node with same name at root level
        var result = await _service.CreateNodeAsync(
            "ignored-id",
            "DuplicateName", // Same name
            null, // Same parent (root)
            100
        );

        // Assert
        result.Should().BeNull(); // Should fail due to duplicate name
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<ClassificationNode>()), Times.Never);
    }

    [Fact]
    public async Task CreateNodeAsync_ShouldAllowSameNameAtDifferentLevels()
    {
        // Arrange - existing node with name at root level
        var parentId = Guid.NewGuid().ToString();
        var existingNodes = new List<ClassificationNode>
        {
            new() { Id = parentId, Name = "Parent", ParentId = null },
            new() { Id = Guid.NewGuid().ToString(), Name = "TestName", ParentId = null }
        };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(existingNodes);
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(new ClassificationNode());

        // Act - create node with same name but under different parent
        var result = await _service.CreateNodeAsync(
            "ignored-id",
            "TestName", // Same name as root node
            parentId, // But different parent
            100
        );

        // Assert
        result.Should().NotBeNull(); // Should succeed - different level
        result!.Name.Should().Be("TestName");
        result.ParentId.Should().Be(parentId);
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<ClassificationNode>()), Times.Once);
    }

    [Fact]
    public async Task CreateNodeAsync_WithThumbnail_ShouldCopyToManagedDirectory()
    {
        // Arrange
        var sourceThumbnail = "C:\\source\\image.png";
        var copiedPath = "thumbnails\\abc123.png";

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(new ClassificationNode());

        _mockFileTransferService.Setup(f => f.CopyToManagedDirectoryAsync(
            sourceThumbnail,
            "thumbnails",
            true
        )).ReturnsAsync(copiedPath);

        // Act
        var result = await _service.CreateNodeAsync(
            "ignored-id",
            "NodeWithThumbnail",
            null,
            100,
            "Description",
            sourceThumbnail
        );

        // Assert
        result.Should().NotBeNull();
        result!.Thumbnail.Should().Be(copiedPath);
        _mockFileTransferService.Verify(f => f.CopyToManagedDirectoryAsync(
            sourceThumbnail,
            "thumbnails",
            true
        ), Times.Once);
    }

    #endregion

    #region Node Update with Stable IDs Tests

    [Fact]
    public async Task UpdateNodeAsync_ShouldKeepSameId_WhenNameChanges()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var existingNode = new ClassificationNode
        {
            Id = nodeId,
            Name = "OldName",
            ParentId = null
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(existingNode);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { existingNode });
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateNodeAsync(nodeId, "NewName");

        // Assert
        result.Should().BeTrue();
        existingNode.Id.Should().Be(nodeId); // ID should remain unchanged
        existingNode.Name.Should().Be("NewName"); // Only name should change
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<ClassificationNode>(n => n.Id == nodeId)), Times.Once);
    }

    [Fact]
    public async Task UpdateNodeAsync_ShouldNotUpdateModCategories_WhenNameChanges()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var existingNode = new ClassificationNode
        {
            Id = nodeId,
            Name = "OldName",
            ParentId = null
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(existingNode);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { existingNode });
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateNodeAsync(nodeId, "NewName");

        // Assert
        result.Should().BeTrue();
        // Should NOT call mod repository to update categories
        _mockModRepository.Verify(r => r.UpdateAsync(It.IsAny<ModInfo>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNodeAsync_ShouldPreventDuplicateNamesAtSameLevel()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var siblingId = Guid.NewGuid().ToString();
        var parentId = Guid.NewGuid().ToString();

        var targetNode = new ClassificationNode
        {
            Id = nodeId,
            Name = "CurrentName",
            ParentId = parentId
        };

        var siblingNode = new ClassificationNode
        {
            Id = siblingId,
            Name = "ExistingName",
            ParentId = parentId // Same parent
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(targetNode);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { targetNode, siblingNode });

        // Act - try to rename to existing sibling name
        var result = await _service.UpdateNodeAsync(nodeId, "ExistingName");

        // Assert
        result.Should().BeFalse(); // Should fail due to duplicate name
        targetNode.Name.Should().Be("CurrentName"); // Name should remain unchanged
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ClassificationNode>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNodeAsync_ShouldAllowSameNameAtDifferentLevels()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var otherNodeId = Guid.NewGuid().ToString();

        var targetNode = new ClassificationNode
        {
            Id = nodeId,
            Name = "CurrentName",
            ParentId = "parent1"
        };

        var otherNode = new ClassificationNode
        {
            Id = otherNodeId,
            Name = "NewName",
            ParentId = "parent2" // Different parent
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(targetNode);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { targetNode, otherNode });
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(true);

        // Act - rename to same name but at different level
        var result = await _service.UpdateNodeAsync(nodeId, "NewName");

        // Assert
        result.Should().BeTrue(); // Should succeed - different parents
        targetNode.Name.Should().Be("NewName");
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ClassificationNode>()), Times.Once);
    }

    #endregion

    #region Node Deletion Tests

    [Fact]
    public async Task DeleteNodeAsync_ShouldDeleteNodeAndChildren()
    {
        // Arrange
        var parentId = Guid.NewGuid().ToString();
        var childId = Guid.NewGuid().ToString();
        var grandchildId = Guid.NewGuid().ToString();

        var nodes = new List<ClassificationNode>
        {
            new() { Id = parentId, Name = "Parent", ParentId = null },
            new() { Id = childId, Name = "Child", ParentId = parentId },
            new() { Id = grandchildId, Name = "Grandchild", ParentId = childId }
        };

        _mockRepository.Setup(r => r.GetByIdAsync(parentId)).ReturnsAsync(nodes[0]);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);
        _mockRepository.Setup(r => r.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.DeleteNodeAsync(parentId);

        // Assert
        result.Should().BeTrue();
        // Should delete parent and all descendants
        _mockRepository.Verify(r => r.DeleteAsync(parentId), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(childId), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(grandchildId), Times.Once);
    }

    [Fact]
    public async Task DeleteNodeAsync_WithNonExistentNode_ShouldReturnFalse()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync((ClassificationNode?)null);

        // Act
        var result = await _service.DeleteNodeAsync(nodeId);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Node Movement Tests

    [Fact]
    public async Task MoveNodeAsync_ShouldUpdateParentId_ButKeepSameId()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var newParentId = Guid.NewGuid().ToString();

        var node = new ClassificationNode
        {
            Id = nodeId,
            Name = "MovingNode",
            ParentId = null // Start at root
        };

        var newParent = new ClassificationNode
        {
            Id = newParentId,
            Name = "NewParent",
            ParentId = null
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(node);
        _mockRepository.Setup(r => r.GetByIdAsync(newParentId)).ReturnsAsync(newParent);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { node, newParent });
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(true);

        // Act
        var result = await _service.MoveNodeAsync(nodeId, newParentId, 0);

        // Assert
        result.Should().BeTrue();
        node.Id.Should().Be(nodeId); // ID should remain the same
        node.ParentId.Should().Be(newParentId); // Parent should be updated
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<ClassificationNode>(n =>
            n.Id == nodeId && n.ParentId == newParentId)), Times.Once);
    }

    [Fact]
    public async Task MoveNodeAsync_ShouldPreventMovingNodeUnderItself()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        var childId = Guid.NewGuid().ToString();

        var parent = new ClassificationNode
        {
            Id = nodeId,
            Name = "Parent",
            ParentId = null
        };

        var child = new ClassificationNode
        {
            Id = childId,
            Name = "Child",
            ParentId = nodeId
        };

        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync(parent);
        _mockRepository.Setup(r => r.GetByIdAsync(childId)).ReturnsAsync(child);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode> { parent, child });

        // Act - try to move parent under its own child
        var result = await _service.MoveNodeAsync(nodeId, childId, 0);

        // Assert
        result.Should().BeFalse(); // Should prevent circular reference
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ClassificationNode>()), Times.Never);
    }

    #endregion

    #region Node Existence Tests

    [Fact]
    public async Task NodeExistsAsync_WithExistingNode_ShouldReturnTrue()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        _mockRepository.Setup(r => r.ExistsAsync(nodeId)).ReturnsAsync(true);

        // Act
        var result = await _service.NodeExistsAsync(nodeId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.ExistsAsync(nodeId), Times.Once);
    }

    [Fact]
    public async Task NodeExistsAsync_WithNonExistentNode_ShouldReturnFalse()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        _mockRepository.Setup(r => r.ExistsAsync(nodeId)).ReturnsAsync(false);

        // Act
        var result = await _service.NodeExistsAsync(nodeId);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.ExistsAsync(nodeId), Times.Once);
    }

    #endregion

    #region Mod Count Calculation Tests

    [Fact]
    public async Task GetClassificationTreeAsync_ShouldCalculateModCounts()
    {
        // Arrange
        var parentId = Guid.NewGuid().ToString();
        var childId = Guid.NewGuid().ToString();

        var nodes = new List<ClassificationNode>
        {
            new() { Id = parentId, Name = "Parent", ParentId = null },
            new() { Id = childId, Name = "Child", ParentId = parentId }
        };

        var mods = new List<ModInfo>
        {
            new() { SHA = "mod1", Category = parentId },
            new() { SHA = "mod2", Category = parentId },
            new() { SHA = "mod3", Category = childId }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(nodes);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(mods);

        // Act
        var result = await _service.GetClassificationTreeAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].ModCount.Should().Be(5); // 2 direct + 3 from child (recursive count)
        result[0].Children[0].ModCount.Should().Be(3); // 3 direct mods
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public async Task CreateNodeAsync_WhenRepositoryThrows_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.CreateNodeAsync("id", "Name", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNodeAsync_WhenNodeNotFound_ShouldReturnFalse()
    {
        // Arrange
        var nodeId = Guid.NewGuid().ToString();
        _mockRepository.Setup(r => r.GetByIdAsync(nodeId)).ReturnsAsync((ClassificationNode?)null);

        // Act
        var result = await _service.UpdateNodeAsync(nodeId, "NewName");

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ClassificationNode>()), Times.Never);
    }

    [Fact]
    public async Task CreateNodeAsync_ShouldHandleGuidCollision_ByGeneratingNewGuid()
    {
        // Arrange - simulate first GUID already exists (extremely rare)
        var callCount = 0;
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(() => {
                callCount++;
                return callCount == 1; // First call returns true (collision), second returns false
            });
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ClassificationNode>());
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>())).ReturnsAsync(new ClassificationNode());

        // Act
        var result = await _service.CreateNodeAsync("ignored", "TestNode", null);

        // Assert
        result.Should().NotBeNull();
        Guid.TryParse(result!.Id, out _).Should().BeTrue();
        _mockRepository.Verify(r => r.ExistsAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    #endregion
}