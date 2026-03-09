using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Category.Entities;
using D3dxSkinManager.Modules.Category.Mappers;
using D3dxSkinManager.Modules.Category.Models;

namespace D3dxSkinManager.Tests.Modules.Category.Mappers;

/// <summary>
/// Edge case and robustness tests for CategoryMapper
/// Tests metadata handling, hierarchy edge cases, and error conditions
/// </summary>
public class CategoryMapperEdgeCaseTests
{
    #region Metadata JSON Edge Cases

    [Fact]
    public void ToDomain_WithInvalidMetadataJson_ShouldReturnEmptyMetadataAndNotThrow()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Metadata = "{ invalid json }"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithNullMetadata_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Metadata = null
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithEmptyJsonObject_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Metadata = "{}"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().NotBeNull();
        domain.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_WithComplexNestedMetadata_ShouldDeserializeCorrectly()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Metadata = @"{
                ""level1"": {
                    ""level2"": {
                        ""level3"": ""deep value"",
                        ""array"": [1, 2, 3]
                    }
                },
                ""topLevel"": ""simple value""
            }"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().ContainKey("level1");
        domain.Metadata.Should().ContainKey("topLevel");
        domain.Metadata["topLevel"].ToString().Should().Be("simple value");
    }

    [Fact]
    public void ToDomain_WithMetadataContainingSpecialCharacters_ShouldPreserveValues()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Metadata = @"{
                ""unicode"": ""测试 日本語 한국어"",
                ""emoji"": ""🎮🔥💯"",
                ""escaped"": ""Line1\nLine2\tTabbed""
            }"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Metadata.Should().ContainKey("unicode");
        domain.Metadata.Should().ContainKey("emoji");
        domain.Metadata.Should().ContainKey("escaped");
    }

    [Fact]
    public void ToEntity_WithNullMetadata_ShouldSerializeAsNull()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat1",
            Name = "Test",
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
            Id = "cat1",
            Name = "Test",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.Metadata.Should().Be("{}");
    }

    [Fact]
    public void ToEntity_WithComplexMetadataTypes_ShouldSerializeAllTypes()
    {
        // Arrange
        var domain = new CategoryInfo
        {
            Id = "cat1",
            Name = "Test",
            Metadata = new Dictionary<string, object>
            {
                { "string", "value" },
                { "int", 42 },
                { "double", 3.14 },
                { "bool", true },
                { "null", null! },  // Intentionally testing null handling
                { "array", new[] { 1, 2, 3 } },
                { "nested", new Dictionary<string, object> { { "key", "value" } } }
            }
        };

        // Act
        var entity = CategoryMapper.ToEntity(domain);

        // Assert
        entity.Metadata.Should().Contain("string");
        entity.Metadata.Should().Contain("int");
        entity.Metadata.Should().Contain("42");
        entity.Metadata.Should().Contain("3.14");
        entity.Metadata.Should().Contain("true");
    }

    #endregion

    #region Thumbnail Path Mapping

    [Fact]
    public void ToDomain_WithNullThumbnailPath_ShouldMapToNullThumbnail()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ThumbnailPath = null
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Thumbnail.Should().BeNull();
    }

    [Fact]
    public void ToDomain_WithEmptyThumbnailPath_ShouldMapToEmptyString()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ThumbnailPath = ""
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Thumbnail.Should().Be("");
    }

    [Fact]
    public void ToDomain_WithLongThumbnailPath_ShouldPreservePath()
    {
        // Arrange
        var longPath = "C:\\" + new string('A', 500) + "\\thumbnail.png";
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ThumbnailPath = longPath
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Thumbnail.Should().Be(longPath);
    }

    [Fact]
    public void ToDomain_WithPathContainingSpecialChars_ShouldPreserveExactly()
    {
        // Arrange
        var specialPath = "C:\\Users\\测试\\Mod Folder (Special) [2024]\\thumbnail #1.png";
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ThumbnailPath = specialPath
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Thumbnail.Should().Be(specialPath);
    }

    #endregion

    #region Hierarchy and ParentId Edge Cases

    [Fact]
    public void ToDomain_WithNullParentId_ShouldMapToNull()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Root Category",
            ParentId = null
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.ParentId.Should().BeNull();
    }

    [Fact]
    public void ToDomain_WithEmptyParentId_ShouldMapToEmptyString()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ParentId = ""
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.ParentId.Should().Be("");
    }

    [Fact]
    public void ToDomain_WithSelfReferentialParentId_ShouldMapWithoutValidation()
    {
        // Arrange - Edge case: business logic should prevent this, but mapper doesn't validate
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ParentId = "cat1" // Self-reference
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Id.Should().Be("cat1");
        domain.ParentId.Should().Be("cat1");
    }

    [Fact]
    public void ToDomain_WithNonExistentParentId_ShouldMapWithoutValidation()
    {
        // Arrange - Edge case: referential integrity handled elsewhere
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            ParentId = "non-existent-parent"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.ParentId.Should().Be("non-existent-parent");
    }

    [Fact]
    public void ToDomain_ChildrenProperty_ShouldAlwaysBeInitializedEmptyList()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Children.Should().NotBeNull();
        domain.Children.Should().BeEmpty();
        domain.Children.Should().BeOfType<List<CategoryInfo>>();
    }

    #endregion

    #region Priority Boundary Values

    [Fact]
    public void ToDomain_WithNegativePriority_ShouldPreserveValue()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Priority = -100
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Priority.Should().Be(-100);
    }

    [Fact]
    public void ToDomain_WithMaxIntPriority_ShouldPreserveValue()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Priority = int.MaxValue
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Priority.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ToDomain_WithMinIntPriority_ShouldPreserveValue()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Priority = int.MinValue
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Priority.Should().Be(int.MinValue);
    }

    #endregion

    #region DateTime Edge Cases

    [Fact]
    public void ToDomain_WithMinDateTime_ShouldPreserveValue()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            CreatedAt = DateTime.MinValue,
            UpdatedAt = DateTime.MinValue
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.CreatedAt.Should().Be(DateTime.MinValue);
        domain.UpdatedAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ToDomain_WithMaxDateTime_ShouldPreserveValue()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            CreatedAt = DateTime.MaxValue,
            UpdatedAt = DateTime.MaxValue
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.CreatedAt.Should().Be(DateTime.MaxValue);
        domain.UpdatedAt.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void RoundTrip_WithPreciseDateTime_ShouldPreserveMilliseconds()
    {
        // Arrange
        var precise = new DateTime(2024, 3, 9, 14, 35, 22, 456, DateTimeKind.Utc);
        var originalDomain = new CategoryInfo
        {
            Id = "cat1",
            Name = "Test",
            CreatedAt = precise,
            UpdatedAt = precise
        };

        // Act
        var entity = CategoryMapper.ToEntity(originalDomain);
        var roundTripped = CategoryMapper.ToDomain(entity);

        // Assert
        roundTripped.CreatedAt.Should().Be(precise);
        roundTripped.UpdatedAt.Should().Be(precise);
    }

    #endregion

    #region String Field Edge Cases

    [Fact]
    public void ToDomain_WithVeryLongName_ShouldPreserve()
    {
        // Arrange
        var longName = new string('A', 10000);
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = longName
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Name.Should().HaveLength(10000);
        domain.Name.Should().Be(longName);
    }

    [Fact]
    public void ToDomain_WithVeryLongDescription_ShouldPreserve()
    {
        // Arrange
        var longDesc = new string('D', 50000);
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "Test",
            Description = longDesc
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Description.Should().HaveLength(50000);
    }

    [Fact]
    public void ToDomain_WithUnicodeInAllFields_ShouldPreserveUnicode()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "测试ID",
            Name = "カテゴリー名",
            Description = "설명 Description",
            ParentId = "родитель"
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Id.Should().Be("测试ID");
        domain.Name.Should().Be("カテゴリー名");
        domain.Description.Should().Be("설명 Description");
        domain.ParentId.Should().Be("родитель");
    }

    [Fact]
    public void ToDomain_WithWhitespaceOnlyFields_ShouldPreserve()
    {
        // Arrange
        var entity = new CategoryEntity
        {
            Id = "cat1",
            Name = "   ",
            Description = "\t\t\t",
            ParentId = "  "
        };

        // Act
        var domain = CategoryMapper.ToDomain(entity);

        // Assert
        domain.Name.Should().Be("   ");
        domain.Description.Should().Be("\t\t\t");
        domain.ParentId.Should().Be("  ");
    }

    #endregion

    #region List Operations Edge Cases

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
    public void ToDomainList_WithDuplicateIds_ShouldPreserveDuplicates()
    {
        // Arrange
        var entities = new List<CategoryEntity>
        {
            new CategoryEntity { Id = "dup", Name = "First" },
            new CategoryEntity { Id = "dup", Name = "Second" }
        };

        // Act
        var domainList = CategoryMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(2);
        domainList[0].Id.Should().Be("dup");
        domainList[1].Id.Should().Be("dup");
    }

    [Fact]
    public void ToDomainList_WithVeryLargeList_ShouldHandleEfficiently()
    {
        // Arrange - 5000 categories
        var entities = Enumerable.Range(0, 5000)
            .Select(i => new CategoryEntity
            {
                Id = $"cat{i}",
                Name = $"Category {i}",
                Priority = i
            })
            .ToList();

        // Act
        var domainList = CategoryMapper.ToDomainList(entities);

        // Assert
        domainList.Should().HaveCount(5000);
        domainList[0].Id.Should().Be("cat0");
        domainList[4999].Id.Should().Be("cat4999");
    }

    #endregion

    #region Round-Trip Stress Tests

    [Fact]
    public void RoundTrip_WithAllFieldsPopulated_ShouldPreserveEverything()
    {
        // Arrange
        var originalDomain = new CategoryInfo
        {
            Id = "cat-complex",
            Name = "Complex Category 测试",
            ParentId = "parent-123",
            Thumbnail = "C:\\path\\to\\thumbnail (copy).png",
            Priority = 42,
            Description = "Very long description with\nmultiple lines\tand tabs",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddHours(1),
            Metadata = new Dictionary<string, object>
            {
                { "str", "value" },
                { "num", 123 },
                { "bool", true },
                { "nested", new Dictionary<string, object> { { "key", "val" } } }
            }
        };

        // Act
        var entity = CategoryMapper.ToEntity(originalDomain);
        var roundTripped = CategoryMapper.ToDomain(entity);

        // Assert
        roundTripped.Id.Should().Be(originalDomain.Id);
        roundTripped.Name.Should().Be(originalDomain.Name);
        roundTripped.ParentId.Should().Be(originalDomain.ParentId);
        roundTripped.Thumbnail.Should().Be(originalDomain.Thumbnail);
        roundTripped.Priority.Should().Be(originalDomain.Priority);
        roundTripped.Description.Should().Be(originalDomain.Description);
        roundTripped.Metadata.Should().ContainKey("str");
        roundTripped.Metadata.Should().ContainKey("num");
    }

    [Fact]
    public void MultipleRoundTrips_ShouldProduceIdenticalResults()
    {
        // Arrange
        var original = new CategoryInfo
        {
            Id = "cat1",
            Name = "Test",
            Priority = 10,
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };

        // Act - Multiple round trips
        var entity1 = CategoryMapper.ToEntity(original);
        var domain1 = CategoryMapper.ToDomain(entity1);
        var entity2 = CategoryMapper.ToEntity(domain1);
        var domain2 = CategoryMapper.ToDomain(entity2);

        // Assert - Second round trip should match first
        domain2.Id.Should().Be(domain1.Id);
        domain2.Name.Should().Be(domain1.Name);
        domain2.Priority.Should().Be(domain1.Priority);
    }

    #endregion
}
