using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Entities;
using D3dxSkinManager.Modules.Category.Mappers;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Category.Services;

/// <summary>
/// Interface for Category repository
/// </summary>
public interface ICategoryRepository
{
    Task<List<CategoryInfo>> GetAllAsync();
    Task<CategoryInfo?> GetByIdAsync(string id);
    Task<List<CategoryInfo>> GetChildrenAsync(string? parentId);
    Task<List<string>> GetAllDescendantIdsAsync(string parentId);
    Task<CategoryInfo?> GetByNameAsync(string name);
    Task<CategoryInfo> InsertAsync(CategoryInfo category);
    Task<bool> UpdateAsync(CategoryInfo category);
    Task<bool> DeleteAsync(string id);
    Task<bool> ExistsAsync(string id);
    Task ClearAllAsync();
    Task<bool> MoveCategoryAsync(string categoryId, string? newParentId);
    Task<bool> UpdatePriorityAsync(string categoryId, int priority);
    Task<bool> ReorderSiblingsAsync(List<(string categoryId, int priority)> updates);
}

/// <summary>
/// Repository for Category database operations
/// Manages the Category tree structure in SQLite
/// </summary>
public class CategoryRepository : ICategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(IProfilePathService profilePaths)
    {
        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
        // Table creation now handled by Fluent migrations (Migration_202603080002_CreateCategoriesTable)
    }

    public async Task<List<CategoryInfo>> GetAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<CategoryEntity>(
            "SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories ORDER BY Priority DESC, Name"
        );
        return CategoryMapper.ToDomainList(entities);
    }

    public async Task<CategoryInfo?> GetByIdAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = await connection.QuerySingleOrDefaultAsync<CategoryEntity>(
            "SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories WHERE Id = @id",
            new { id }
        );

        return entity != null ? CategoryMapper.ToDomain(entity) : null;
    }

    public async Task<List<CategoryInfo>> GetChildrenAsync(string? parentId)
    {
        await using var connection = new SqliteConnection(_connectionString);

        IEnumerable<CategoryEntity> entities;
        if (parentId == null)
        {
            entities = await connection.QueryAsync<CategoryEntity>(
                "SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories WHERE ParentId IS NULL ORDER BY Priority DESC, Name"
            );
        }
        else
        {
            entities = await connection.QueryAsync<CategoryEntity>(
                "SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories WHERE ParentId = @parentId ORDER BY Priority DESC, Name",
                new { parentId }
            );
        }

        return CategoryMapper.ToDomainList(entities);
    }

    /// <summary>
    /// Get all descendant category IDs recursively (children, grandchildren, etc.)
    /// Used for querying mods by parent category (includes all subcategories)
    /// </summary>
    public async Task<List<string>> GetAllDescendantIdsAsync(string parentId)
    {
        var descendantIds = new List<string>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(parentId);

        await using var connection = new SqliteConnection(_connectionString);

        // BFS to collect all descendants
        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            descendantIds.Add(currentId);

            // Get direct children using Dapper
            var childIds = await connection.QueryAsync<string>(
                "SELECT Id FROM Categories WHERE ParentId = @parentId",
                new { parentId = currentId }
            );

            foreach (var childId in childIds)
            {
                if (!string.IsNullOrEmpty(childId))
                {
                    toProcess.Enqueue(childId);
                }
            }
        }

        return descendantIds;
    }

    public async Task<CategoryInfo?> GetByNameAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = await connection.QuerySingleOrDefaultAsync<CategoryEntity>(
            "SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories WHERE Name = @name LIMIT 1",
            new { name }
        );

        return entity != null ? CategoryMapper.ToDomain(entity) : null;
    }

    public async Task<CategoryInfo> InsertAsync(CategoryInfo category)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = CategoryMapper.ToEntity(category);

        // Set timestamps if not already set (use default values from entity)
        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }
        if (entity.UpdatedAt == default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await connection.ExecuteAsync(
            @"INSERT INTO Categories (Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt)
              VALUES (@Id, @Name, @ParentId, @ThumbnailPath, @Priority, @Description, @Metadata, @CreatedAt, @UpdatedAt)",
            entity
        );
        return category;
    }

    public async Task<bool> UpdateAsync(CategoryInfo category)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = CategoryMapper.ToEntity(category);

        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Categories
              SET Name = @Name,
                  ParentId = @ParentId,
                  ThumbnailPath = @ThumbnailPath,
                  Priority = @Priority,
                  Description = @Description,
                  Metadata = @Metadata,
                  UpdatedAt = CURRENT_TIMESTAMP
              WHERE Id = @Id",
            entity
        );
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM Categories WHERE Id = @id",
            new { id }
        );
        return rowsAffected > 0;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Categories WHERE Id = @id",
            new { id }
        );
        return count > 0;
    }

    public async Task ClearAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync("DELETE FROM Categories");
    }

    public async Task<bool> MoveCategoryAsync(string categoryId, string? newParentId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Categories
              SET ParentId = @newParentId
              WHERE Id = @categoryId",
            new { categoryId, newParentId }
        );
        return rowsAffected > 0;
    }

    public async Task<bool> UpdatePriorityAsync(string categoryId, int priority)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Categories
              SET Priority = @priority
              WHERE Id = @categoryId",
            new { categoryId, priority }
        );
        return rowsAffected > 0;
    }

    public async Task<bool> ReorderSiblingsAsync(List<(string categoryId, int priority)> updates)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (categoryId, priority) in updates)
            {
                await connection.ExecuteAsync(
                    @"UPDATE Categories
                      SET Priority = @priority
                      WHERE Id = @categoryId",
                    new { categoryId, priority },
                    transaction
                );
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            return false;
        }
    }
}

