using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Integration tests for ModRepository NULL handling
/// Verifies that nullable database fields are properly handled through the full stack:
/// Database (NULL) → Entity (string?) → Mapper → Domain (string)
/// </summary>
public class ModRepositoryNullHandlingTests : InMemoryDatabaseTestBase
{
    private readonly ModRepository _repository;

    public ModRepositoryNullHandlingTests()
    {
        MockProfilePathService.Setup(x => x.CacheModsDirectory).Returns("C:\\test_cache");
        _repository = new ModRepository(MockProfilePathService.Object, MockLogger.Object);
    }

    [Fact]
    public async Task InsertAndRetrieve_WithNullOptionalFields_ShouldRoundTripCorrectly()
    {
        // Arrange - Entity with NULL optional fields (as database allows)
        var entity = new ModEntity
        {
            SHA = "null-test",
            Category = "test-category",
            Name = "Test Mod",
            Author = null,  // NULL in database
            Description = null,  // NULL in database
            Tags = null,  // NULL in database
            Type = "7z",
            Grading = "G"
        };

        // Act - Insert and retrieve
        await _repository.InsertAsync(entity);
        var retrieved = await _repository.GetByIdAsync("null-test");

        // Assert - NULLs should be preserved in entity layer
        retrieved.Should().NotBeNull();
        retrieved!.Author.Should().BeNull("database allows NULL");
        retrieved.Description.Should().BeNull("database allows NULL");
        retrieved.Tags.Should().BeNull("database allows NULL");

        // When converted to domain model, NULLs become empty strings
        var domain = ModMapper.ToDomain(retrieved);
        domain.Author.Should().Be(string.Empty, "domain layer converts NULL to empty string");
        domain.Description.Should().Be(string.Empty, "domain layer converts NULL to empty string");
        domain.Tags.Should().BeEmpty("NULL Tags JSON becomes empty list");
    }

    [Fact]
    public async Task Update_WithEmptyStrings_ShouldPreserveEmptyStrings()
    {
        // Arrange - Create mod with values
        var original = new ModEntity
        {
            SHA = "empty-string-test",
            Category = "category1",
            Name = "Test Mod",
            Author = "Original Author",
            Description = "Original Description",
            Type = "7z",
            Grading = "G"
        };
        await _repository.InsertAsync(original);

        // Act - Update with empty strings (user clearing fields)
        var updated = new ModEntity
        {
            SHA = "empty-string-test",
            Category = "category1",
            Name = "Test Mod",
            Author = string.Empty,  // Clearing author
            Description = string.Empty,  // Clearing description
            Type = "7z",
            Grading = "G"
        };
        await _repository.UpdateAsync(updated);

        // Assert - Empty strings should be stored as-is
        var retrieved = await _repository.GetByIdAsync("empty-string-test");
        retrieved!.Author.Should().Be(string.Empty, "empty string should be preserved");
        retrieved.Description.Should().Be(string.Empty, "empty string should be preserved");
    }

    [Fact]
    public async Task GetDistinctAuthorsAsync_ShouldExcludeNullAndEmpty()
    {
        // Arrange - Insert mods with various author values
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "author-1",
            Category = "test",
            Name = "Mod 1",
            Author = "Author A",  // Valid author
            Type = "7z",
            Grading = "G"
        });

        await _repository.InsertAsync(new ModEntity
        {
            SHA = "author-2",
            Category = "test",
            Name = "Mod 2",
            Author = null,  // NULL author
            Type = "7z",
            Grading = "G"
        });

        await _repository.InsertAsync(new ModEntity
        {
            SHA = "author-3",
            Category = "test",
            Name = "Mod 3",
            Author = string.Empty,  // Empty author
            Type = "7z",
            Grading = "G"
        });

        await _repository.InsertAsync(new ModEntity
        {
            SHA = "author-4",
            Category = "test",
            Name = "Mod 4",
            Author = "Author B",  // Valid author
            Type = "7z",
            Grading = "G"
        });

        await _repository.InsertAsync(new ModEntity
        {
            SHA = "author-5",
            Category = "test",
            Name = "Mod 5",
            Author = "Author A",  // Duplicate (should be distinct)
            Type = "7z",
            Grading = "G"
        });

        // Act
        var distinctAuthors = await _repository.GetDistinctAuthorsAsync();

        // Assert - Should only include non-empty, non-null authors, distinct
        distinctAuthors.Should().HaveCount(2);
        distinctAuthors.Should().Contain("Author A");
        distinctAuthors.Should().Contain("Author B");
        // All returned authors should be non-null and non-empty
        distinctAuthors.Should().OnlyContain(a => !string.IsNullOrEmpty(a), "null and empty authors should be excluded");
    }

    [Fact]
    public async Task Mapper_RoundTrip_PreservesSemantics()
    {
        // Arrange - Start with entity containing NULLs
        var originalEntity = new ModEntity
        {
            SHA = "round-trip-test",
            Category = "test",
            Name = "Round Trip Test",
            Author = null,
            Description = null,
            Type = "7z",
            Grading = "G"
        };

        await _repository.InsertAsync(originalEntity);

        // Act - Full round trip: Database → Entity → Domain → Entity → Database
        var retrievedEntity1 = await _repository.GetByIdAsync("round-trip-test");
        var domain = ModMapper.ToDomain(retrievedEntity1!);
        var backToEntity = ModMapper.ToEntity(domain);
        await _repository.UpdateAsync(backToEntity);
        var retrievedEntity2 = await _repository.GetByIdAsync("round-trip-test");

        // Assert - Semantic equivalence maintained
        // NULLs and empty strings are semantically equivalent for optional fields
        retrievedEntity2!.Author.Should().BeOneOf(null, string.Empty,
            "NULL and empty string are semantically equivalent");
        retrievedEntity2.Description.Should().BeOneOf(null, string.Empty,
            "NULL and empty string are semantically equivalent");
    }
}
