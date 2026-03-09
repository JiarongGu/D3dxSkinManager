using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Integration tests for ModRepository with real database operations
/// Tests error handling, concurrency, transactions, and edge cases using in-memory database with migrations
/// </summary>
public class ModRepositoryIntegrationTests : InMemoryDatabaseTestBase
{
    private readonly ModRepository _repository;

    public ModRepositoryIntegrationTests()
    {
        MockProfilePathService.Setup(x => x.CacheModsDirectory).Returns("C:\\test_cache");
        _repository = new ModRepository(MockProfilePathService.Object, MockLogger.Object);
    }

    #region Concurrent Operations

    [Fact]
    public async Task InsertAsync_ConcurrentInserts_ShouldHandleAllInserts()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _repository.InsertAsync(new ModEntity
            {
                SHA = $"concurrent{i}",
                Category = "test",
                Name = $"Mod {i}"
            }))
            .ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(10);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentUpdates_ShouldHandleAllUpdates()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "concurrent-update",
            Category = "test",
            Name = "Original"
        });

        var tasks = Enumerable.Range(0, 10)
            .Select(i => _repository.UpdateAsync(new ModEntity
            {
                SHA = "concurrent-update",
                Category = "test",
                Name = $"Updated {i}"
            }))
            .ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert - Last update should win (or one of them)
        var result = await _repository.GetByIdAsync("concurrent-update");
        result.Should().NotBeNull();
        result!.Name.Should().StartWith("Updated");
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task InsertAsync_WithDuplicateSHA_ShouldThrow()
    {
        // Arrange
        var entity = new ModEntity
        {
            SHA = "duplicate",
            Category = "test",
            Name = "First"
        };
        await _repository.InsertAsync(entity);

        var duplicate = new ModEntity
        {
            SHA = "duplicate",
            Category = "test2",
            Name = "Second"
        };

        // Act & Assert
        await Assert.ThrowsAsync<SqliteException>(() => _repository.InsertAsync(duplicate));
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentMod_ShouldNotThrow()
    {
        // Arrange
        var entity = new ModEntity
        {
            SHA = "non-existent",
            Category = "test",
            Name = "Test"
        };

        // Act
        await _repository.UpdateAsync(entity);

        // Assert - Should complete without error (update affects 0 rows)
        var result = await _repository.GetByIdAsync("non-existent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentMod_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        await _repository.DeleteAsync("non-existent");
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public async Task InsertAsync_WithMaxLengthStrings_ShouldSucceed()
    {
        // Arrange
        var entity = new ModEntity
        {
            SHA = new string('A', 40), // SHA-1 max length
            Category = new string('C', 500),
            Name = new string('N', 5000),
            Description = new string('D', 10000),
            Author = new string('A', 1000),
            Tags = "[" + string.Join(",", Enumerable.Range(0, 1000).Select(i => $"\"{i}\"")) + "]"
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync(entity.SHA);
        result.Should().NotBeNull();
        result!.Name.Should().HaveLength(5000);
    }

    [Fact]
    public async Task InsertAsync_WithMinimalFields_ShouldSucceed()
    {
        // Arrange - Only required fields
        var entity = new ModEntity
        {
            SHA = "minimal",
            Category = "test"
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync("minimal");
        result.Should().NotBeNull();
        result!.SHA.Should().Be("minimal");
    }

    [Fact]
    public async Task InsertAsync_WithUnicodeCharacters_ShouldPreserveUnicode()
    {
        // Arrange
        var entity = new ModEntity
        {
            SHA = "unicode-test",
            Category = "test",
            Name = "测试模组 日本語 한국어 العربية",
            Description = "Emoji: 🎮🔥💯",
            Author = "作者名前"
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync("unicode-test");
        result!.Name.Should().Be("测试模组 日本語 한국어 العربية");
        result.Description.Should().Be("Emoji: 🎮🔥💯");
        result.Author.Should().Be("作者名前");
    }

    #endregion

    #region Query Performance and Large Data

    [Fact]
    public async Task GetAllAsync_WithLargeDataset_ShouldReturnAll()
    {
        // Arrange - Insert 1000 mods
        var tasks = Enumerable.Range(0, 1000)
            .Select(i => _repository.InsertAsync(new ModEntity
            {
                SHA = $"perf{i:D4}",
                Category = $"cat{i % 10}",
                Name = $"Mod {i}"
            }));

        await Task.WhenAll(tasks);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(1000);
    }

    [Fact]
    public async Task GetByCategoryAsync_WithManyResults_ShouldReturnAllMatches()
    {
        // Arrange - 500 mods in same category
        var tasks = Enumerable.Range(0, 500)
            .Select(i => _repository.InsertAsync(new ModEntity
            {
                SHA = $"cat-test{i}",
                Category = "popular",
                Name = $"Mod {i}"
            }));

        await Task.WhenAll(tasks);

        // Act
        var result = await _repository.GetByCategoryAsync("popular");

        // Assert
        result.Should().HaveCount(500);
    }

    #endregion

    #region Tag Operations

    [Fact]
    public async Task GetAllTagsAsync_WithDuplicateTags_ShouldReturnUnique()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "tag1",
            Category = "test",
            Tags = "[\"action\",\"rpg\"]"
        });
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "tag2",
            Category = "test",
            Tags = "[\"rpg\",\"adventure\"]"
        });
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "tag3",
            Category = "test",
            Tags = "[\"action\",\"adventure\"]"
        });

        // Act
        var result = await _repository.GetAllTagsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("action");
        result.Should().Contain("rpg");
        result.Should().Contain("adventure");
    }

    [Fact]
    public async Task GetAllTagsAsync_WithEmptyTags_ShouldIgnoreEmptyArrays()
    {
        // Arrange
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "empty-tags",
            Category = "test",
            Tags = "[]"
        });
        await _repository.InsertAsync(new ModEntity
        {
            SHA = "with-tags",
            Category = "test",
            Tags = "[\"test\"]"
        });

        // Act
        var result = await _repository.GetAllTagsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("test");
    }

    #endregion

    #region DateTime Handling

    [Fact]
    public async Task InsertAsync_WithPreciseDateTime_ShouldPreserveWithinSeconds()
    {
        // Arrange
        var precise = DateTime.UtcNow;
        var entity = new ModEntity
        {
            SHA = "datetime-test",
            Category = "test",
            CreatedAt = precise
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert - SQLite datetime stores to second precision, not millisecond
        var result = await _repository.GetByIdAsync("datetime-test");
        result!.CreatedAt.Should().BeCloseTo(precise, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task InsertAsync_AutomaticTimestamp_ShouldSetCreatedAt()
    {
        // Arrange
        var beforeInsert = DateTime.UtcNow;
        var entity = new ModEntity
        {
            SHA = "timestamp-test",
            Category = "test"
        };

        // Act
        await _repository.InsertAsync(entity);
        var afterInsert = DateTime.UtcNow;

        // Assert - Database sets CreatedAt automatically via DEFAULT
        var result = await _repository.GetByIdAsync("timestamp-test");
        result!.CreatedAt.Should().BeAfter(beforeInsert.AddSeconds(-2));
        result.CreatedAt.Should().BeBefore(afterInsert.AddSeconds(2));
    }

    #endregion

    #region Null and Empty Value Handling

    [Fact]
    public async Task InsertAsync_WithNullOptionalFields_ShouldSucceed()
    {
        // Arrange
        // Name is NOT NULL in database, so we must provide a value
        // Optional fields can be null and will use the entity's defaults
        var entity = new ModEntity
        {
            SHA = "null-fields",
            Category = "test",
            Name = "Test Mod",  // Name is required (NOT NULL in DB)
            Description = null,  // Will use default string.Empty
            Author = null,       // Will use default string.Empty
            Metadata = null      // Will use default string.Empty
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync("null-fields");
        result.Should().NotBeNull();
        result!.SHA.Should().Be("null-fields");
        result.Name.Should().Be("Test Mod");
        // These fields are allowed to be null in the database
        // When explicitly set to null, they remain null (not converted to empty string)
        result.Description.Should().BeNull();
        result.Author.Should().BeNull();
        result.Metadata.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_WithEmptyStrings_ShouldPreserveEmptyStrings()
    {
        // Arrange
        var entity = new ModEntity
        {
            SHA = "empty-strings",
            Category = "test",
            Name = "",
            Description = "",
            Author = ""
        };

        // Act
        await _repository.InsertAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync("empty-strings");
        result!.Name.Should().Be("");
        result.Description.Should().Be("");
        result.Author.Should().Be("");
    }

    #endregion

    #region Transaction and Rollback Scenarios

    [Fact]
    public async Task MultipleOperations_InSequence_ShouldAllSucceed()
    {
        // Arrange & Act - Series of operations
        await _repository.InsertAsync(new ModEntity { SHA = "seq1", Category = "test", Name = "First" });
        await _repository.InsertAsync(new ModEntity { SHA = "seq2", Category = "test", Name = "Second" });
        await _repository.UpdateAsync(new ModEntity { SHA = "seq1", Category = "test", Name = "Updated" });
        await _repository.DeleteAsync("seq2");

        // Assert
        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].SHA.Should().Be("seq1");
        all[0].Name.Should().Be("Updated");
    }

    #endregion
}
