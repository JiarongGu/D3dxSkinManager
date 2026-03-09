using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Category.Entities;
using D3dxSkinManager.Modules.Category.Mappers;
using D3dxSkinManager.Modules.Category.Models;

namespace D3dxSkinManager.Tests.Modules.Category.Mappers;

/// <summary>
/// Unit tests for CategoryMapper
/// Tests entity-domain conversion without external dependencies
/// </summary>
public class CategoryMapperTests
{
    [Fact]
    public void ToDomain_WithValidEntity_ShouldConvertCorrectly()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat-123",
            Name = "Test Category",
            ParentId = "parent-456",
            ThumbnailPath = "/path/to/thumb.png",
            Priority = 10,
            Metadata = "{\"key1\":\"value1\",\"key2\":\"value2\"}",
            Description = "Test description",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Should().NotBeNull();
        domain.Id.Should().Be("cat-123");
        domain.Name.Should().Be("Test Category");
        domain.ParentId.Should().Be("parent-456");
        domain.Thumbnail.Should().Be("/path/to/thumb.png"); // Note: ThumbnailPath -> Thumbnail
        domain.Priority.Should().Be(10);
        domain.Description.Should().Be("Test description");
        domain.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        domain.UpdatedAt.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc));
        domain.Children.Should().NotBeNull();
        domain.Children.Should().BeEmpty();
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().ContainKey("key1");
        domain.Metadata["key1"].ToString().Should().Be("value1");
        domain.Metadata.Should().ContainKey("key2");
        domain.Metadata["key2"].ToString().Should().Be("value2");
    }

    [Fact]
    public void ToDomain_WithNullMetadata_ShouldInitializeEmptyDictionary()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat-123",
            Name = "Test Category",
            Metadata = null
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithEmptyMetadata_ShouldInitializeEmptyDictionary()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat-123",
            Name = "Test Category",
            Metadata = ""
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithNullThumbnailPath_ShouldMapToNullThumbnail()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat-123",
            Name = "Test Category",
            ThumbnailPath = null
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Thumbnail.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WithValidDomain_ShouldConvertCorrectly()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat-123",
            Name = "Test Category",
            ParentId = "parent-456",
            Thumbnail = "/path/to/thumb.png",
            Priority = 10,
            Description = "Test description",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Metadata = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 }
            }
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be("cat-123");
        entity.Name.Should().Be("Test Category");
        entity.ParentId.Should().Be("parent-456");
        entity.ThumbnailPath.Should().Be("/path/to/thumb.png"); // Note: Thumbnail -> ThumbnailPath
        entity.Priority.Should().Be(10);
        entity.Description.Should().Be("Test description");
        entity.CreatedAt.Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        entity.UpdatedAt.Should().Be(new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc));
        entity.Metadata.Should().NotBeNullOrEmpty();
        entity.Metadata.Should().Contain("\"key1\"");
        entity.Metadata.Should().Contain("\"key2\"");
    }

    [Fact]
    public void ToEntity_WithNullMetadata_ShouldSerializeAsNull()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat-123",
            Name = "Test Category",
            Metadata = null
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.Metadata.Should().BeNull();
    }

    [Fact]
    public void ToEntity_WithEmptyMetadata_ShouldSerializeAsEmptyObject()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat-123",
            Name = "Test Category",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.Metadata.Should().NotBeNullOrEmpty();
        entity.Metadata.Should().Be("{}");
    }

    [Fact]
    public void ToEntity_WithNullThumbnail_ShouldMapToNullThumbnailPath()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat-123",
            Name = "Test Category",
            Thumbnail = null
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.ThumbnailPath.Should().BeNull();
    }

    [Fact]
    public void ToDomainList_WithMultipleEntities_ShouldConvertAll()
    {
        // Arrange
        var entities = new List<CategoryEntity>
        {
            new CategoryEntity
            {
                Id = "cat-1",
                Name = "Category 1",
                Priority = 10
            },
            new CategoryEntity
            {
                Id = "cat-2",
                Name = "Category 2",
                Priority = 20,
                Metadata = "{\"type\":\"folder\"}"
            },
            new CategoryEntity
            {
                Id = "cat-3",
                Name = "Category 3",
                Priority = 30,
                ParentId = "cat-1"
            }
        };

        // Act
        var domainList = CategoryMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(3);
        domainList[0].Id.Should().Be("cat-1");
        domainList[0].Name.Should().Be("Category 1");
        domainList[1].Id.Should().Be("cat-2");
        domainList[1].Metadata.Should().ContainKey("type");
        domainList[2].Id.Should().Be("cat-3");
        domainList[2].ParentId.Should().Be("cat-1");
    }

    [Fact]
    public void ToDomainList_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var entities = new List<CategoryEntity>();

        // Act
        var domainList = CategoryMapper.ToDomainList(entities);

        // Assert
        domainList.Should().NotBeNull();
        domainList.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_EntityToDomainToEntity_ShouldPreserveData()
    {
        // Arrange
        var originalEntity = new CategoryEntity
        {
            Id = "cat-123",
            Name = "Test Category",
            ParentId = "parent-456",
            ThumbnailPath = "/path/to/thumb.png",
            Priority = 10,
            Metadata = "{\"key\":\"value\"}",
            Description = "Test description",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var domain = CategoryMapper.ToDomain(originalEntity);
        var roundTrippedEntity = CategoryMapper.ToEntity(domain);

        // Assert
        roundTrippedEntity.Id.Should().Be(originalEntity.Id);
        roundTrippedEntity.Name.Should().Be(originalEntity.Name);
        roundTrippedEntity.ParentId.Should().Be(originalEntity.ParentId);
        roundTrippedEntity.ThumbnailPath.Should().Be(originalEntity.ThumbnailPath);
        roundTrippedEntity.Priority.Should().Be(originalEntity.Priority);
        roundTrippedEntity.Description.Should().Be(originalEntity.Description);
        roundTrippedEntity.CreatedAt.Should().Be(originalEntity.CreatedAt);
        roundTrippedEntity.UpdatedAt.Should().Be(originalEntity.UpdatedAt);
        // Metadata JSON might have different formatting but same content
        roundTrippedEntity.Metadata.Should().Contain("key");
        roundTrippedEntity.Metadata.Should().Contain("value");
    }

    [Fact]
    public void RoundTrip_DomainToEntityToDomain_ShouldPreserveData()
    {
        // Arrange
        var originalDomain = new CategoryInfo
        {
            Id = "cat-123",
            Name = "Test Category",
            ParentId = "parent-456",
            Thumbnail = "/path/to/thumb.png",
            Priority = 10,
            Description = "Test description",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Metadata = new Dictionary<string, object>
            {
                { "key", "value" },
                { "count", 123 }
            }
        };

        // Act
        var entity = CategoryMapper.ToEntity(originalDomain);
        var roundTrippedDomain = CategoryMapper.ToDomain(entity);

        // Assert
        roundTrippedDomain.Id.Should().Be(originalDomain.Id);
        roundTrippedDomain.Name.Should().Be(originalDomain.Name);
        roundTrippedDomain.ParentId.Should().Be(originalDomain.ParentId);
        roundTrippedDomain.Thumbnail.Should().Be(originalDomain.Thumbnail);
        roundTrippedDomain.Priority.Should().Be(originalDomain.Priority);
        roundTrippedDomain.Description.Should().Be(originalDomain.Description);
        roundTrippedDomain.CreatedAt.Should().Be(originalDomain.CreatedAt);
        roundTrippedDomain.UpdatedAt.Should().Be(originalDomain.UpdatedAt);
        roundTrippedDomain.Metadata.Should().ContainKey("key");
        roundTrippedDomain.Metadata["key"].ToString().Should().Be("value");
        roundTrippedDomain.Metadata.Should().ContainKey("count");
        roundTrippedDomain.Metadata["count"].ToString().Should().Be("123"); // JSON deserializes as string
    }
}
