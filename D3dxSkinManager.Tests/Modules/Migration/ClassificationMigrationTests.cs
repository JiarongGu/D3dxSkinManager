using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Migration.Steps;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Tests.Modules.Migration;

/// <summary>
/// Unit tests for classification migration to GUID-based system
/// Tests migration from legacy path-based IDs to new GUID system
/// </summary>
public class ClassificationMigrationTests
{
    private readonly Mock<IProfileContext> _mockProfileContext = new();
    private readonly Mock<ILogHelper> _mockLogHelper = new();
    private readonly Mock<IClassificationService> _mockClassificationService = new();
    private readonly Mock<IModAutoDetectionService> _mockAutoDetectionService = new();
    private readonly MigrationStep3MigrateClassifications _migrationStep;

    public ClassificationMigrationTests()
    {
        _mockProfileContext.Setup(p => p.ProfileDataPath).Returns(@"C:\TestProfile\data");

        _migrationStep = new MigrationStep3MigrateClassifications(
            _mockProfileContext.Object,
            _mockLogHelper.Object,
            _mockClassificationService.Object,
            _mockAutoDetectionService.Object
        );
    }

    [Fact]
    public async Task MigrateClassifications_ShouldCreateNodesWithGUIDs()
    {
        // Arrange
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>
            {
                { "Characters", new List<string> { "Character1", "Character2" } },
                { "Weapons", new List<string> { "Sword", "Bow" } }
            }
        };

        var createdNodes = new List<ClassificationNode>();

        // Capture created nodes to verify GUIDs
        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((string nodeId, string name, string parentId, int priority, string desc, string thumb) =>
            {
                var node = new ClassificationNode
                {
                    Id = Guid.NewGuid().ToString(), // Service should generate GUID
                    Name = name,
                    ParentId = parentId,
                    Priority = priority,
                    Description = desc
                };
                createdNodes.Add(node);
                return node;
            });

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue();
        createdNodes.Should().NotBeEmpty();

        // All created nodes should have valid GUIDs
        foreach (var node in createdNodes)
        {
            Guid.TryParse(node.Id, out _).Should().BeTrue($"Node {node.Name} should have a valid GUID");
        }

        // Verify hierarchy is maintained with GUIDs
        var parentNodes = createdNodes.Where(n => n.ParentId == null).ToList();
        var childNodes = createdNodes.Where(n => n.ParentId != null).ToList();

        parentNodes.Should().HaveCount(2); // Characters and Weapons
        childNodes.Should().HaveCount(4); // Character1, Character2, Sword, Bow

        // All child nodes should reference parent GUIDs
        foreach (var child in childNodes)
        {
            parentNodes.Any(p => p.Id == child.ParentId).Should().BeTrue(
                $"Child {child.Name} should reference a valid parent GUID");
        }
    }

    [Fact]
    public async Task MigrateClassifications_ShouldHandleExistingPathBasedIds()
    {
        // Arrange - simulate migration with legacy path-based IDs
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>
            {
                { "Game.Characters", new List<string> { "Game.Characters.Hero" } }
            }
        };

        var createdNodes = new List<ClassificationNode>();

        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(
                It.IsAny<string>(), // Old path-based ID will be passed but ignored
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((string oldId, string name, string parentId, int priority, string desc, string thumb) =>
            {
                // Service ignores oldId and generates GUID
                var node = new ClassificationNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    ParentId = parentId,
                    Priority = priority,
                    Description = desc
                };
                createdNodes.Add(node);
                return node;
            });

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue();

        // Even though old path-based IDs were provided, new GUIDs should be generated
        foreach (var node in createdNodes)
        {
            node.Id.Should().NotContain(".");  // GUIDs don't contain dots like path-based IDs
            node.Id.Should().NotBe("Game.Characters"); // Should not use old ID
            Guid.TryParse(node.Id, out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task MigrateClassifications_ShouldPreventDuplicateNames()
    {
        // Arrange - attempt to create nodes with duplicate names at same level
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>
            {
                { "Category", new List<string> { "Item", "Item" } } // Duplicate names
            }
        };

        var callCount = 0;
        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(
                It.IsAny<string>(),
                "Item",
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call succeeds
                    return new ClassificationNode
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Item",
                        ParentId = null
                    };
                }
                else
                {
                    // Second call fails due to duplicate name
                    return null;
                }
            });

        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(
                It.IsAny<string>(),
                "Category",
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new ClassificationNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Category",
                ParentId = null
            });

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue(); // Migration continues despite duplicate
        _mockClassificationService.Verify(
            s => s.CreateNodeAsync(It.IsAny<string>(), "Item", It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2)); // Both attempts should be made
    }

    [Fact]
    public async Task MigrateClassifications_ShouldCreateAutoDetectionRules()
    {
        // Arrange
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>
            {
                { "Characters", new List<string> { "Hero", "Villain" } }
            }
        };

        var capturedRules = new List<ModAutoDetectionRule>();

        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string nodeId, string name, string parentId, int priority, string desc, string thumb) =>
                new ClassificationNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    ParentId = parentId
                });

        _mockAutoDetectionService
            .Setup(s => s.AddRuleAsync(It.IsAny<ModAutoDetectionRule>()))
            .Callback<ModAutoDetectionRule>(rule => capturedRules.Add(rule))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue();
        capturedRules.Should().HaveCount(2); // One for each leaf node

        // Rules should use the node names for pattern matching
        capturedRules.Should().Contain(r => r.Pattern == "*Hero*" && r.Category == "Hero");
        capturedRules.Should().Contain(r => r.Pattern == "*Villain*" && r.Category == "Villain");
    }

    [Fact]
    public async Task MigrateClassifications_WithEmptyData_ShouldReturnSuccess()
    {
        // Arrange
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>() // Empty
        };

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("No classification");
        _mockClassificationService.Verify(
            s => s.CreateNodeAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MigrateClassifications_WhenServiceThrows_ShouldContinueAndLogError()
    {
        // Arrange
        var analysis = new MigrationAnalysis
        {
            HasData = true,
            ModGroups = new Dictionary<string, List<string>>
            {
                { "Category", new List<string> { "Item1", "Item2" } }
            }
        };

        var callCount = 0;
        _mockClassificationService
            .Setup(s => s.CreateNodeAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2) // Fail on second call
                {
                    throw new Exception("Database error");
                }
                return new ClassificationNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Node{callCount}",
                    ParentId = null
                };
            });

        // Act
        var result = await _migrationStep.ExecuteAsync(analysis, new MigrationOptions(), null);

        // Assert
        result.Success.Should().BeTrue(); // Migration should continue despite errors
        _mockLogHelper.Verify(
            l => l.Error(It.IsAny<string>(), It.IsAny<string>()),
            Times.AtLeastOnce); // Error should be logged
    }
}