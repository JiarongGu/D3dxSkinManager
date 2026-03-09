using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Tests.Modules.Mod.Mappers;

/// <summary>
/// Unit tests for TagMapper
/// Tests entity-domain conversion without external dependencies
/// </summary>
public class TagMapperTests
{
    [Fact]
    public void ToDomain_WithValidEntity_ShouldConvertCorrectly()
    {
        // Arrange
        var entity = new TagEntity
        {
            Name = "action",
            Color = "#FF5733",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var domain = TagMapper.ToDomain(entity);

        // Assert
        domain.Should().NotBeNull();
        domain.Name.Should().Be("action");
        domain.Color.Should().Be("#FF5733");
        domain.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        domain.UpdatedAt.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToDomain_WithDefaultColor_ShouldUseDefaultColor()
    {
        // Arrange
        var entity = new TagEntity
        {
            Name = "test-tag",
            Color = "#808080" // Default gray
        };

        // Act
        var domain = TagMapper.ToDomain(entity);

        // Assert
        domain.Color.Should().Be("#808080");
    }

    [Fact]
    public void ToEntity_WithValidDomain_ShouldConvertCorrectly()
    {
        // Arrange
        var domain = new Tag
        {
            Name = "adventure",
            Color = "#3366FF",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var entity = TagMapper.ToEntity(domain);

        // Assert
        entity.Should().NotBeNull();
        entity.Name.Should().Be("adventure");
        entity.Color.Should().Be("#3366FF");
        entity.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        entity.UpdatedAt.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToEntity_WithDefaultColor_ShouldPreserveDefaultColor()
    {
        // Arrange
        var domain = new Tag
        {
            Name = "test-tag",
            Color = "#808080"
        };

        // Act
        var entity = TagMapper.ToEntity(domain);

        // Assert
        entity.Color.Should().Be("#808080");
    }

    [Fact]
    public void ToDomainList_WithMultipleTags_ShouldConvertAll()
    {
        // Arrange
        var entities = new List<TagEntity>
        {
            new TagEntity
            {
                Name = "action",
                Color = "#FF5733",
                CreatedAt = new DateTime(2024, 1, 1),
                UpdatedAt = new DateTime(2024, 1, 1)
            },
            new TagEntity
            {
                Name = "adventure",
                Color = "#3366FF",
                CreatedAt = new DateTime(2024, 1, 2),
                UpdatedAt = new DateTime(2024, 1, 2)
            },
            new TagEntity
            {
                Name = "rpg",
                Color = "#33CC33",
                CreatedAt = new DateTime(2024, 1, 3),
                UpdatedAt = new DateTime(2024, 1, 3)
            }
        };

        // Act
        var domainList = TagMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(3);
        domainList[0].Name.Should().Be("action");
        domainList[0].Color.Should().Be("#FF5733");
        domainList[1].Name.Should().Be("adventure");
        domainList[1].Color.Should().Be("#3366FF");
        domainList[2].Name.Should().Be("rpg");
        domainList[2].Color.Should().Be("#33CC33");
    }

    [Fact]
    public void ToDomainList_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var entities = new List<TagEntity>();

        // Act
        var domainList = TagMapper.ToDomainList(entities);

        // Assert
        domainList.Should().NotBeNull();
        domainList.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_EntityToDomainToEntity_ShouldPreserveData()
    {
        // Arrange
        var originalEntity = new TagEntity
        {
            Name = "strategy",
            Color = "#FF9900",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var domain = TagMapper.ToDomain(originalEntity);
        var roundTrippedEntity = TagMapper.ToEntity(domain);

        // Assert
        roundTrippedEntity.Name.Should().Be(originalEntity.Name);
        roundTrippedEntity.Color.Should().Be(originalEntity.Color);
        roundTrippedEntity.CreatedAt.Should().Be(originalEntity.CreatedAt);
        roundTrippedEntity.UpdatedAt.Should().Be(originalEntity.UpdatedAt);
    }

    [Fact]
    public void RoundTrip_DomainToEntityToDomain_ShouldPreserveData()
    {
        // Arrange
        var originalDomain = new Tag
        {
            Name = "puzzle",
            Color = "#9933CC",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var entity = TagMapper.ToEntity(originalDomain);
        var roundTrippedDomain = TagMapper.ToDomain(entity);

        // Assert
        roundTrippedDomain.Name.Should().Be(originalDomain.Name);
        roundTrippedDomain.Color.Should().Be(originalDomain.Color);
        roundTrippedDomain.CreatedAt.Should().Be(originalDomain.CreatedAt);
        roundTrippedDomain.UpdatedAt.Should().Be(originalDomain.UpdatedAt);
    }

    [Fact]
    public void ToDomain_WithVariousColorFormats_ShouldPreserveColorExactly()
    {
        // Arrange & Act & Assert - Test various hex color formats
        var testCases = new[]
        {
            "#000000", // Black
            "#FFFFFF", // White
            "#FF0000", // Red
            "#00FF00", // Green
            "#0000FF", // Blue
            "#808080", // Gray (default)
            "#ABCDEF", // Mixed case
            "#123456"  // Arbitrary
        };

        foreach (var color in testCases)
        {
            var entity = new TagEntity { Name = "test", Color = color };
            var domain = TagMapper.ToDomain(entity);
            domain.Color.Should().Be(color, $"Color {color} should be preserved exactly");
        }
    }

    [Fact]
    public void ToEntity_WithVariousColorFormats_ShouldPreserveColorExactly()
    {
        // Arrange & Act & Assert - Test various hex color formats
        var testCases = new[]
        {
            "#000000", // Black
            "#FFFFFF", // White
            "#FF0000", // Red
            "#00FF00", // Green
            "#0000FF", // Blue
            "#808080", // Gray (default)
            "#ABCDEF", // Mixed case
            "#123456"  // Arbitrary
        };

        foreach (var color in testCases)
        {
            var domain = new Tag { Name = "test", Color = color };
            var entity = TagMapper.ToEntity(domain);
            entity.Color.Should().Be(color, $"Color {color} should be preserved exactly");
        }
    }

    [Fact]
    public void ToDomain_WithMultipleTagsWithSameTimestamps_ShouldMapIndependently()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var entities = new List<TagEntity>
        {
            new TagEntity { Name = "tag1", Color = "#111111", CreatedAt = now, UpdatedAt = now },
            new TagEntity { Name = "tag2", Color = "#222222", CreatedAt = now, UpdatedAt = now },
            new TagEntity { Name = "tag3", Color = "#333333", CreatedAt = now, UpdatedAt = now }
        };

        // Act
        var domainList = TagMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(3);
        domainList.Should().OnlyHaveUniqueItems(tag => tag.Name);
        domainList.Should().OnlyHaveUniqueItems(tag => tag.Color);
        domainList.Should().AllSatisfy(tag =>
        {
            tag.CreatedAt.Should().Be(now);
            tag.UpdatedAt.Should().Be(now);
        });
    }
}
