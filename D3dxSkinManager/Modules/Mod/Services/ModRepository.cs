using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod repository
/// Works with ModEntity (database model) - use ModMapper to convert to ModInfo (domain model)
/// </summary>
public interface IModRepository
{
    Task<List<ModEntity>> GetAllAsync();
    Task<ModEntity?> GetByIdAsync(string id);
    Task<bool> ExistsAsync(string id);
    Task<ModEntity> InsertAsync(ModEntity entity);
    Task<bool> UpdateAsync(ModEntity entity);
    /// <summary>Update ONLY the Metadata column (single-column write). Avoids the whole-row clobber
    /// of <see cref="UpdateAsync"/> when a caller touches only Metadata (e.g. the fix-time stamp),
    /// so it can't wipe a concurrent category/tag edit.</summary>
    Task<bool> UpdateMetadataAsync(string id, string? metadata);
    Task<bool> DeleteAsync(string id);
    Task<List<ModEntity>> GetByCategoryAsync(string category);
    Task<List<ModEntity>> GetByMultipleCategoriesAsync(IEnumerable<string> categoryIds);
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<List<string>> GetDistinctAuthorsAsync();
    Task<List<string>> GetAllTagsAsync();
    Task<List<string>> GetLoadedIdsAsync(); // File system-based: scans cache directory for active mods
}

/// <summary>
/// Repository for mod database operations (CRUD)
/// Uses Dapper for clean, efficient data access
/// </summary>
public class ModRepository : IModRepository
{
    private readonly string _connectionString;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;

    public ModRepository(IProfilePathService profilePaths, ILogHelper logger)
    {
        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";

        _profilePaths = profilePaths;
        _logger = logger;
        // Table creation now handled by Fluent migrations (Migration_202603080001_CreateModsTable)
    }

    public async Task<List<ModEntity>> GetAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<ModEntity>("SELECT * FROM Mods ORDER BY Id");
        return entities.ToList();
    }

    public async Task<ModEntity?> GetByIdAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<ModEntity>(
            "SELECT * FROM Mods WHERE Id = @id",
            new { id }
        );
    }

    public async Task<bool> ExistsAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Mods WHERE Id = @id",
            new { id }
        );
        return count > 0;
    }

    public async Task<ModEntity> InsertAsync(ModEntity entity)
    {
        // Set timestamps if not already set (use default values from entity)
        if (entity.CreatedAt == default)
        {
            entity.CreatedAt = DateTime.UtcNow;
        }
        if (entity.UpdatedAt == default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        var sql = @"
            INSERT INTO Mods (Id, Category, Name, Author, Description, Type, Grading, Tags, DisablePreview, CreatedAt, UpdatedAt, Metadata)
            VALUES (@Id, @Category, @Name, @Author, @Description, @Type, @Grading, @Tags, @DisablePreview, @CreatedAt, @UpdatedAt, @Metadata)";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Category,
            entity.Name,
            entity.Author,
            entity.Description,
            entity.Type,
            entity.Grading,
            entity.Tags,
            DisablePreview = entity.DisablePreview ? 1 : 0,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Metadata
        });

        return entity;
    }

    public async Task<bool> UpdateAsync(ModEntity entity)
    {
        var sql = @"
            UPDATE Mods SET
                Category = @Category,
                Name = @Name,
                Author = @Author,
                Description = @Description,
                Type = @Type,
                Grading = @Grading,
                Tags = @Tags,
                DisablePreview = @DisablePreview,
                Metadata = @Metadata,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = @Id";

        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Category,
            entity.Name,
            entity.Author,
            entity.Description,
            entity.Type,
            entity.Grading,
            entity.Tags,
            DisablePreview = entity.DisablePreview ? 1 : 0,
            entity.Metadata
        });

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateMetadataAsync(string id, string? metadata)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.ExecuteAsync(
            "UPDATE Mods SET Metadata = @metadata, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @id",
            new { id, metadata });
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM Mods WHERE Id = @id",
            new { id }
        );

        return rowsAffected > 0;
    }

    public async Task<List<ModEntity>> GetByCategoryAsync(string category)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<ModEntity>(
            "SELECT * FROM Mods WHERE Category = @category ORDER BY Name COLLATE NOCASE",
            new { category }
        );
        return entities.ToList();
    }

    public async Task<List<ModEntity>> GetByMultipleCategoriesAsync(IEnumerable<string> categoryIds)
    {
        var categoryList = categoryIds.ToList();
        if (categoryList.Count == 0)
        {
            return new List<ModEntity>();
        }

        await using var connection = new SqliteConnection(_connectionString);

        // Dapper supports IN clauses with collections
        var entities = await connection.QueryAsync<ModEntity>(
            "SELECT * FROM Mods WHERE Category IN @categoryIds",
            new { categoryIds = categoryList }
        );

        return entities.ToList();
    }

    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var categories = await connection.QueryAsync<string>(
            "SELECT DISTINCT Category FROM Mods WHERE Category != '' ORDER BY Category"
        );
        return categories.ToList();
    }

    public async Task<List<string>> GetDistinctAuthorsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var authors = await connection.QueryAsync<string>(
            "SELECT DISTINCT Author FROM Mods WHERE Author != '' ORDER BY Author"
        );
        return authors.ToList();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);

        var tagsJsonList = await connection.QueryAsync<string>(
            "SELECT Tags FROM Mods WHERE Tags != ''"
        );

        var allTags = new HashSet<string>();
        foreach (var tagsJson in tagsJsonList)
        {
            if (!string.IsNullOrEmpty(tagsJson))
            {
                var tags = JsonHelper.Deserialize<List<string>>(tagsJson);
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        allTags.Add(tag);
                    }
                }
            }
        }

        return allTags.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Get list of loaded mod IDs (file system-based check)
    /// Returns IDs of mods that have cache directories without DISABLED- prefix
    /// NOTE: This is not a database query - it scans the file system
    /// </summary>
    public Task<List<string>> GetLoadedIdsAsync()
    {
        var loadedIds = new List<string>();

        var cacheDir = _profilePaths.CacheModsDirectory;
        if (!Directory.Exists(cacheDir))
        {
            return Task.FromResult(loadedIds);
        }

        var directories = Directory.GetDirectories(cacheDir);
        foreach (var dir in directories)
        {
            var dirName = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(dirName) && !ModConventions.IsDisabledCacheName(dirName))
            {
                loadedIds.Add(dirName);
            }
        }

        return Task.FromResult(loadedIds);
    }
}
