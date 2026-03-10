using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Mappers;

namespace D3dxSkinManager.Tests.Modules.Mod.Mappers;

/// <summary>
/// Unit tests for ModMapper
/// Tests entity-domain conversion between ModEntity (database) and ModInfo (business logic)
/// </summary>
public class ModMapperTests
{
    [Fact]
    public void ToDomain_WithValidEntity_ShouldConvertCorrectly()
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
            Tags = "[\"action\",\"rpg\"]",  // JSON string
            DisablePreview = true,
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 2),
            Metadata = "{\"version\":\"1.0\"}"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Should().NotBeNull();
        domain.Id.Should().Be("abc123");
        domain.Category.Should().Be("category-1");
        domain.Name.Should().Be("Test Mod");
        domain.Author.Should().Be("Test Author");
        domain.Description.Should().Be("Test Description");
        domain.Type.Should().Be("7z");
        domain.Grading.Should().Be("G");
        domain.Tags.Should().BeEquivalentTo(new List<string> { "action", "rpg" });
        domain.DisablePreview.Should().BeTrue();
        domain.CreatedAt.Should().Be(new DateTime(2024, 1, 1));
        domain.UpdatedAt.Should().Be(new DateTime(2024, 1, 2));

        // Computed properties should be initialized to defaults
        domain.IsLoaded.Should().BeFalse();
        domain.IsAvailable.Should().BeFalse();
        domain.CategoryName.Should().BeEmpty();
        domain.CachePath.Should().BeNull();
    }

    [Fact]
    public void ToDomain_WithEmptyTags_ShouldReturnEmptyList()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "Test",
            Tags = "[]"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithNullTags_ShouldReturnEmptyList()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "Test",
            Tags = ""
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_WithValidDomain_ShouldConvertCorrectly()
    {
        // Arrange
        var domain = new ModInfo
        {
            Id = "abc123",
            Category = "category-1",
            Name = "Test Mod",
            Author = "Test Author",
            Description = "Test Description",
            Type = "7z",
            Grading = "G",
            Tags = new List<string> { "action", "rpg" },
            DisablePreview = true,
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 2),
            // Computed properties (should not be mapped to entity)
            IsLoaded = true,
            CategoryName = "My Category",
            CachePath = "C:\\cache\\test"
        };

        // Act
        var entity = ModMapper.ToEntity(domain);

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be("abc123");
        entity.Category.Should().Be("category-1");
        entity.Name.Should().Be("Test Mod");
        entity.Author.Should().Be("Test Author");
        entity.Description.Should().Be("Test Description");
        entity.Type.Should().Be("7z");
        entity.Grading.Should().Be("G");
        // Tags JSON may be formatted, so deserialize to compare
        entity.Tags.Should().NotBeNull();
        var deserializedTags = JsonHelper.Deserialize<List<string>>(entity.Tags!);
        deserializedTags.Should().BeEquivalentTo(new List<string> { "action", "rpg" });
        entity.DisablePreview.Should().BeTrue();
        entity.CreatedAt.Should().Be(new DateTime(2024, 1, 1));
        entity.UpdatedAt.Should().Be(new DateTime(2024, 1, 2));
    }

    [Fact]
    public void ToEntity_WithEmptyTags_ShouldSerializeAsEmptyArray()
    {
        // Arrange
        var domain = new ModInfo
        {
            Id = "test",
            Name = "Test",
            Tags = new List<string>()
        };

        // Act
        var entity = ModMapper.ToEntity(domain);

        // Assert
        entity.Tags.Should().Be("[]");
    }

    [Fact]
    public void UpdateEntity_ShouldUpdateAllProperties()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "abc123",
            Name = "Old Name",
            Author = "Old Author",
            Tags = "[]"
        };

        var domain = new ModInfo
        {
            Id = "abc123",
            Category = "new-category",
            Name = "New Name",
            Author = "New Author",
            Description = "New Description",
            Tags = new List<string> { "new-tag" },
            DisablePreview = true
        };

        // Act
        ModMapper.UpdateEntity(entity, domain);

        // Assert
        entity.Id.Should().Be("abc123");  // SHA should not change
        entity.Category.Should().Be("new-category");
        entity.Name.Should().Be("New Name");
        entity.Author.Should().Be("New Author");
        entity.Description.Should().Be("New Description");
        // Tags JSON may be formatted, so deserialize to compare
        var updatedTags = JsonHelper.Deserialize<List<string>>(entity.Tags);
        updatedTags.Should().BeEquivalentTo(new List<string> { "new-tag" });
        entity.DisablePreview.Should().BeTrue();
    }

    [Fact]
    public void ToDomainList_WithMultipleEntities_ShouldConvertAll()
    {
        // Arrange
        var entities = new List<ModEntity>
        {
            new ModEntity { Id = "sha1", Name = "Mod 1", Tags = "[]" },
            new ModEntity { Id = "sha2", Name = "Mod 2", Tags = "[\"tag1\"]" },
            new ModEntity { Id = "sha3", Name = "Mod 3", Tags = "[\"tag2\",\"tag3\"]" }
        };

        // Act
        var domains = ModMapper.ToDomainList(entities);

        // Assert
        domains.Should().HaveCount(3);
        domains[0].Id.Should().Be("sha1");
        domains[0].Tags.Should().BeEmpty();
        domains[1].Id.Should().Be("sha2");
        domains[1].Tags.Should().BeEquivalentTo(new List<string> { "tag1" });
        domains[2].Id.Should().Be("sha3");
        domains[2].Tags.Should().BeEquivalentTo(new List<string> { "tag2", "tag3" });
    }

    [Fact]
    public void ToDomainList_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var entities = new List<ModEntity>();

        // Act
        var domains = ModMapper.ToDomainList(entities);

        // Assert
        domains.Should().NotBeNull();
        domains.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_EntityToDomainToEntity_ShouldPreserveData()
    {
        // Arrange
        var originalEntity = new ModEntity
        {
            Id = "test123",
            Category = "category-1",
            Name = "Test Mod",
            Author = "Author",
            Description = "Description",
            Type = "7z",
            Grading = "R",
            Tags = "[\"tag1\",\"tag2\"]",
            DisablePreview = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var domain = ModMapper.ToDomain(originalEntity);
        var roundTripEntity = ModMapper.ToEntity(domain);

        // Assert
        roundTripEntity.Id.Should().Be(originalEntity.Id);
        roundTripEntity.Category.Should().Be(originalEntity.Category);
        roundTripEntity.Name.Should().Be(originalEntity.Name);
        roundTripEntity.Author.Should().Be(originalEntity.Author);
        roundTripEntity.Description.Should().Be(originalEntity.Description);
        roundTripEntity.Type.Should().Be(originalEntity.Type);
        roundTripEntity.Grading.Should().Be(originalEntity.Grading);
        // Compare tags by deserializing (JSON formatting may differ)
        originalEntity.Tags.Should().NotBeNull();
        roundTripEntity.Tags.Should().NotBeNull();
        var originalTags = JsonHelper.Deserialize<List<string>>(originalEntity.Tags!);
        var roundTripTags = JsonHelper.Deserialize<List<string>>(roundTripEntity.Tags!);
        roundTripTags.Should().BeEquivalentTo(originalTags);
        roundTripEntity.DisablePreview.Should().Be(originalEntity.DisablePreview);
    }
}
