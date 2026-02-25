using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Mods.Services;

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
    private readonly Lazy<Task> _init;

    public TagRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _init = new Lazy<Task>(InitializeAsync, isThreadSafe: true);
    }

    private async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tags (
                Name TEXT PRIMARY KEY,
                Color TEXT NOT NULL,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_tags_name ON Tags(Name);
        ";

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    public async Task<List<Tag>> GetAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var tags = new List<Tag>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, Color, CreatedAt, UpdatedAt FROM Tags ORDER BY Name";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            tags.Add(new Tag
            {
                Name = reader.GetString(0),
                Color = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return tags;
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, Color, CreatedAt, UpdatedAt FROM Tags WHERE Name = @name";
        command.Parameters.AddWithValue("@name", name);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return new Tag
            {
                Name = reader.GetString(0),
                Color = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            };
        }

        return null;
    }

    public async Task<bool> UpsertAsync(Tag tag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Tags (Name, Color, CreatedAt, UpdatedAt)
            VALUES (@name, @color, @createdAt, @updatedAt)
            ON CONFLICT(Name) DO UPDATE SET
                Color = @color,
                UpdatedAt = @updatedAt
        ";

        command.Parameters.AddWithValue("@name", tag.Name);
        command.Parameters.AddWithValue("@color", tag.Color);
        command.Parameters.AddWithValue("@createdAt", tag.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("o"));

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tags WHERE Name = @name";
        command.Parameters.AddWithValue("@name", name);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<List<string>> GetUsedTagNamesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var allTags = new HashSet<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Tags FROM Mods WHERE Tags != ''";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var tagsJson = reader.GetString(0);
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
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Tags FROM Mods WHERE Tags != ''";

        int count = 0;

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var tagsJson = reader.GetString(0);
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

        await EnsureInitializedAsync().ConfigureAwait(false);

        var tags = new List<Tag>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Name, Color, CreatedAt, UpdatedAt
            FROM Tags
            WHERE Name LIKE @searchTerm
            ORDER BY Name
        ";
        command.Parameters.AddWithValue("@searchTerm", $"%{searchTerm}%");

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            tags.Add(new Tag
            {
                Name = reader.GetString(0),
                Color = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                UpdatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return tags;
    }
}
