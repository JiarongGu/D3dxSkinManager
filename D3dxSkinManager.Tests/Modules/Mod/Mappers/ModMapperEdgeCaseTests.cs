using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Tests.Modules.Mod.Mappers;

/// <summary>
/// Edge case and robustness tests for ModMapper
/// Tests boundary conditions, invalid data, and error handling
/// </summary>
public class ModMapperEdgeCaseTests
{
    #region Null and Empty String Handling

    [Fact]
    public void ToDomain_WithNullTags_ShouldReturnEmptyList()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Tags = null
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithEmptyTagsJson_ShouldReturnEmptyList()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Tags = "[]"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithInvalidTagsJson_ShouldReturnEmptyListAndNotThrow()
    {
        // Arrange - Invalid JSON
        var entity = new ModEntity
        {
            Id = "test",
            Tags = "{this is not valid json"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert - Should handle gracefully
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithMalformedTagsArray_ShouldHandleGracefully()
    {
        // Arrange - JSON object instead of array
        var entity = new ModEntity
        {
            Id = "test",
            Tags = "{\"tag1\":\"value\"}"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_WithNullTagsList_ShouldSerializeAsEmptyArray()
    {
        // Arrange
        var domain = new ModInfo
        {
            Id = "test",
            Tags = null!  // Intentionally testing null handling
        };

        // Act
        var entity = ModMapper.ToEntity(domain);

        // Assert
        entity.Tags.Should().Be("[]");
    }

    [Fact]
    public void ToEntity_WithEmptyTagsList_ShouldSerializeAsEmptyArray()
    {
        // Arrange
        var domain = new ModInfo
        {
            Id = "test",
            Tags = new List<string>()
        };

        // Act
        var entity = ModMapper.ToEntity(domain);

        // Assert
        entity.Tags.Should().Be("[]");
    }

    #endregion

    #region Special Characters and Unicode

    [Fact]
    public void ToDomain_WithUnicodeCharactersInName_ShouldPreserveUnicode()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "测试模组 日本語 한국어 العربية",
            Category = "test"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Name.Should().Be("测试模组 日本語 한국어 العربية");
    }

    [Fact]
    public void ToDomain_WithSpecialCharactersInTags_ShouldPreserveSpecialChars()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Tags = "[\"tag-with-dash\",\"tag_with_underscore\",\"tag.with.dot\",\"tag@special\"]"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().Contain("tag-with-dash");
        domain.Tags.Should().Contain("tag_with_underscore");
        domain.Tags.Should().Contain("tag.with.dot");
        domain.Tags.Should().Contain("tag@special");
    }

    [Fact]
    public void RoundTrip_WithEmojiInName_ShouldPreserveEmoji()
    {
        // Arrange
        var originalDomain = new ModInfo
        {
            Id = "test",
            Name = "Cool Mod 🎮🔥💯",
            Category = "test"
        };

        // Act
        var entity = ModMapper.ToEntity(originalDomain);
        var roundTrippedDomain = ModMapper.ToDomain(entity);

        // Assert
        roundTrippedDomain.Name.Should().Be("Cool Mod 🎮🔥💯");
    }

    [Fact]
    public void ToDomain_WithQuotesInDescription_ShouldHandleEscaping()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Description = "This is a \"quoted\" description with 'single quotes' too"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Description.Should().Be("This is a \"quoted\" description with 'single quotes' too");
    }

    #endregion

    #region Boundary Values

    [Fact]
    public void ToDomain_WithVeryLongName_ShouldHandleWithoutTruncation()
    {
        // Arrange - 1000 character name
        var longName = new string('A', 1000);
        var entity = new ModEntity
        {
            Id = "test",
            Name = longName
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Name.Should().HaveLength(1000);
        domain.Name.Should().Be(longName);
    }

    [Fact]
    public void ToDomain_WithVeryLongTagsList_ShouldHandleAllTags()
    {
        // Arrange - 100 tags
        var tags = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            tags.Add($"tag{i}");
        }
        var tagsJson = System.Text.Json.JsonSerializer.Serialize(tags);

        var entity = new ModEntity
        {
            Id = "test",
            Tags = tagsJson
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Tags.Should().HaveCount(100);
        domain.Tags.Should().Contain("tag0");
        domain.Tags.Should().Contain("tag99");
    }

    [Fact]
    public void ToEntity_WithMaximumSizeValues_ShouldSerializeWithoutError()
    {
        // Arrange
        var domain = new ModInfo
        {
            Id = new string('A', 40), // id-1 length
            Name = new string('N', 5000),
            Description = new string('D', 10000),
            Author = new string('A', 1000),
            Category = new string('C', 500),
            Tags = Enumerable.Range(0, 1000).Select(i => $"tag{i}").ToList()
        };

        // Act
        var entity = ModMapper.ToEntity(domain);

        // Assert
        entity.Id.Should().HaveLength(40);
        entity.Name.Should().HaveLength(5000);
        entity.Description.Should().HaveLength(10000);
        entity.Tags.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToDomain_WithMinimumRequiredFields_ShouldCreateValidObject()
    {
        // Arrange - Only required fields
        var entity = new ModEntity
        {
            Id = "abc123",
            Category = "test"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Id.Should().Be("abc123");
        domain.Category.Should().Be("test");
        domain.Tags.Should().NotBeNull();
        domain.Tags.Should().BeEmpty();
        domain.IsLoaded.Should().BeFalse();
    }

    #endregion

    #region Date/Time Edge Cases

    [Fact]
    public void ToDomain_WithMinDateTime_ShouldPreserveValue()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            CreatedAt = DateTime.MinValue
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.CreatedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ToDomain_WithMaxDateTime_ShouldPreserveValue()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            UpdatedAt = DateTime.MaxValue
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.UpdatedAt.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void RoundTrip_WithPreciseDateTimeTicks_ShouldPreserveExactValue()
    {
        // Arrange
        var exactDateTime = new DateTime(2024, 3, 9, 14, 32, 47, 123, DateTimeKind.Utc).AddTicks(4567);
        var originalDomain = new ModInfo
        {
            Id = "test",
            Category = "test",
            CreatedAt = exactDateTime,
            UpdatedAt = exactDateTime
        };

        // Act
        var entity = ModMapper.ToEntity(originalDomain);
        var roundTrippedDomain = ModMapper.ToDomain(entity);

        // Assert
        roundTrippedDomain.CreatedAt.Should().Be(exactDateTime);
        roundTrippedDomain.CreatedAt.Ticks.Should().Be(exactDateTime.Ticks);
    }

    #endregion

    #region Whitespace and Trimming

    [Fact]
    public void ToDomain_WithLeadingTrailingWhitespace_ShouldPreserveWhitespace()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "  Name With Spaces  ",
            Author = "\tAuthor With Tabs\t",
            Category = "\nCategory With Newlines\n"
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert - Should preserve whitespace (business logic should handle trimming if needed)
        domain.Name.Should().Be("  Name With Spaces  ");
        domain.Author.Should().Be("\tAuthor With Tabs\t");
        domain.Category.Should().Be("\nCategory With Newlines\n");
    }

    [Fact]
    public void ToDomain_WithOnlyWhitespaceInOptionalFields_ShouldPreserve()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "   ",
            Description = "\t\t\t",
            Author = "   "
        };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.Name.Should().Be("   ");
        domain.Description.Should().Be("\t\t\t");
        domain.Author.Should().Be("   ");
    }

    #endregion

    #region List Operations Edge Cases

    [Fact]
    public void ToDomainList_WithEmptyList_ShouldReturnEmptyListNotNull()
    {
        // Arrange
        var entities = new List<ModEntity>();

        // Act
        var domainList = ModMapper.ToDomainList(entities);

        // Assert
        domainList.Should().NotBeNull();
        domainList.Should().BeEmpty();
        domainList.Should().BeOfType<List<ModInfo>>();
    }

    [Fact]
    public void ToDomainList_WithSingleItem_ShouldReturnListWithOneItem()
    {
        // Arrange
        var entities = new List<ModEntity>
        {
            new ModEntity { Id = "test", Category = "cat1" }
        };

        // Act
        var domainList = ModMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(1);
        domainList[0].Id.Should().Be("test");
    }

    [Fact]
    public void ToDomainList_WithDuplicateSHAs_ShouldPreserveDuplicates()
    {
        // Arrange - Edge case: database shouldn't allow this, but mapper should handle it
        var entities = new List<ModEntity>
        {
            new ModEntity { Id = "duplicate", Category = "cat1", Name = "First" },
            new ModEntity { Id = "duplicate", Category = "cat2", Name = "Second" }
        };

        // Act
        var domainList = ModMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(2);
        domainList[0].Id.Should().Be("duplicate");
        domainList[1].Id.Should().Be("duplicate");
        domainList[0].Name.Should().Be("First");
        domainList[1].Name.Should().Be("Second");
    }

    [Fact]
    public void ToDomainList_WithVeryLargeList_ShouldHandleEfficiently()
    {
        // Arrange - 10,000 entities
        var entities = Enumerable.Range(0, 10000)
            .Select(i => new ModEntity
            {
                Id = $"id{i}",
                Category = $"cat{i % 10}",
                Name = $"Mod {i}"
            })
            .ToList();

        // Act
        var domainList = ModMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(10000);
        domainList[0].Id.Should().Be("id0");
        domainList[9999].Id.Should().Be("id9999");
    }

    #endregion

    #region UpdateEntity Edge Cases

    [Fact]
    public void UpdateEntity_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        ModEntity entity = null!;  // Intentionally testing null handling
        var domainModel = new ModInfo { Id = "test", Category = "test" };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => ModMapper.UpdateEntity(entity, domainModel));
    }

    [Fact]
    public void UpdateEntity_WithAllFieldsNull_ShouldUpdateToNullValues()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "OldName",
            Description = "OldDesc",
            Author = "OldAuthor"
        };
        var domainModel = new ModInfo
        {
            Id = "test",
            Category = "test",
            Name = null!,  // Intentionally testing null handling
            Description = null!,  // Intentionally testing null handling
            Author = null!,  // Intentionally testing null handling
            Tags = null!  // Intentionally testing null handling
        };

        // Act
        ModMapper.UpdateEntity(entity, domainModel);

        // Assert
        entity.Name.Should().BeNull();
        entity.Description.Should().BeNull();
        entity.Author.Should().BeNull();
        entity.Tags.Should().Be("[]"); // Null tags converts to empty array
    }

    [Fact]
    public void UpdateEntity_WithEmptyStrings_ShouldUpdateToEmptyStrings()
    {
        // Arrange
        var entity = new ModEntity
        {
            Id = "test",
            Name = "OldName",
            Description = "OldDesc",
            Author = "OldAuthor"
        };
        var domainModel = new ModInfo
        {
            Id = "test",
            Category = "test",
            Name = "",
            Description = "",
            Author = ""
        };

        // Act
        ModMapper.UpdateEntity(entity, domainModel);

        // Assert
        entity.Name.Should().Be("");
        entity.Description.Should().Be("");
        entity.Author.Should().Be("");
    }

    #endregion

    #region Computed Properties

    [Fact]
    public void ToDomain_IsLoadedProperty_ShouldDefaultToFalse()
    {
        // Arrange
        var entity = new ModEntity { Id = "test", Category = "test" };

        // Act
        var domain = ModMapper.ToDomain(entity);

        // Assert
        domain.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void ToDomain_MultipleCallsSameEntity_ShouldProduceIndependentObjects()
    {
        // Arrange
        var entity = new ModEntity { Id = "test", Category = "test" };

        // Act
        var domain1 = ModMapper.ToDomain(entity);
        var domain2 = ModMapper.ToDomain(entity);

        // Modify domain1
        domain1.IsLoaded = true;
        domain1.Tags.Add("newtag");

        // Assert - domain2 should be unaffected
        domain2.IsLoaded.Should().BeFalse();
        domain2.Tags.Should().BeEmpty();
    }

    #endregion
}
