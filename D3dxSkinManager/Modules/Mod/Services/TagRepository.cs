using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for tag repository
/// Manages the master Tags table (authoritative source for tag definitions)
/// Note: Mods.Tags column stores which tags each mod uses (managed by ModRepository)
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Get all tags from the Tags table
    /// </summary>
    Task<List<Tag>> GetAllAsync();

    /// <summary>
    /// Get a specific tag by name
    /// </summary>
    Task<Tag?> GetByNameAsync(string name);

    /// <summary>
    /// Create or update a tag
    /// </summary>
    Task<bool> UpsertAsync(Tag tag);

    /// <summary>
    /// Delete a tag from the Tags table
    /// Note: This only removes the tag definition, not tag references in Mods.Tags
    /// Mods will keep their tags, but the tag won't appear in autocomplete/dialogs
    /// </summary>
    Task<bool> DeleteAsync(string name);

    /// <summary>
    /// Get all unique tag names that are actually used in mods (from Mods.Tags)
    /// This is different from GetAllAsync which returns tags from Tags table
    /// </summary>
    Task<List<string>> GetUsedTagNamesAsync();

    /// <summary>
    /// Get count of mods using a specific tag (searches Mods.Tags)
    /// </summary>
    Task<int> GetUsageCountAsync(string name);

    /// <summary>
    /// Search tags by name (case-insensitive substring match)
    /// </summary>
    Task<List<Tag>> SearchAsync(string searchTerm);
}


/// <summary>
/// Repository for tag management
/// Manages the Tags table (master list of tag definitions with colors)
/// Responsibility: Tag CRUD operations on Tags table
/// Note: Mods.Tags column is managed by ModRepository
/// </summary>
public class TagRepository : ITagRepository
{
    private readonly string _connectionString;

    public TagRepository(IProfilePathService profilePaths)
    {
        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
        // Table creation now handled by Fluent migrations (Migration_202603080003_CreateTagsTable)
    }

    public async Task<List<Tag>> GetAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<TagEntity>(
            "SELECT Name, Color, CreatedAt, UpdatedAt FROM Tags ORDER BY Name"
        );
        return TagMapper.ToDomainList(entities);
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = await connection.QuerySingleOrDefaultAsync<TagEntity>(
            "SELECT Name, Color, CreatedAt, UpdatedAt FROM Tags WHERE Name = @name",
            new { name }
        );
        return entity != null ? TagMapper.ToDomain(entity) : null;
    }

    public async Task<bool> UpsertAsync(Tag tag)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = TagMapper.ToEntity(tag);

        var rowsAffected = await connection.ExecuteAsync(
            @"INSERT INTO Tags (Name, Color, CreatedAt, UpdatedAt)
              VALUES (@Name, @Color, @CreatedAt, @UpdatedAt)
              ON CONFLICT(Name) DO UPDATE SET
                  Color = @Color,
                  UpdatedAt = @UpdatedAt",
            new
            {
                entity.Name,
                entity.Color,
                CreatedAt = entity.CreatedAt.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            }
        );
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM Tags WHERE Name = @name",
            new { name }
        );
        return rowsAffected > 0;
    }

    public async Task<List<string>> GetUsedTagNamesAsync()
    {
        var allTags = new HashSet<string>();

        await using var connection = new SqliteConnection(_connectionString);
        var tagsJsonList = await connection.QueryAsync<string>(
            "SELECT Tags FROM Mods WHERE Tags != ''"
        );

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

    public async Task<int> GetUsageCountAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var tagsJsonList = await connection.QueryAsync<string>(
            "SELECT Tags FROM Mods WHERE Tags != ''"
        );

        int count = 0;
        foreach (var tagsJson in tagsJsonList)
        {
            if (!string.IsNullOrEmpty(tagsJson))
            {
                var tags = JsonHelper.Deserialize<List<string>>(tagsJson);
                if (tags != null && tags.Contains(name))
                {
                    count++;
                }
            }
        }

        return count;
    }

    public async Task<List<Tag>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync().ConfigureAwait(false);
        }

        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<TagEntity>(
            @"SELECT Name, Color, CreatedAt, UpdatedAt
              FROM Tags
              WHERE Name LIKE @searchTerm
              ORDER BY Name",
            new { searchTerm = $"%{searchTerm}%" }
        );

        return TagMapper.ToDomainList(entities);
    }
}
