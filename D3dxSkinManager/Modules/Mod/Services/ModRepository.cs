using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Utilities;

using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod repository
/// </summary>
public interface IModRepository
{
    Task<List<ModInfo>> GetAllAsync();
    Task<ModInfo?> GetByIdAsync(string sha);
    Task<bool> ExistsAsync(string sha);
    Task<ModInfo> InsertAsync(ModInfo mod);
    Task<bool> UpdateAsync(ModInfo mod);
    Task<bool> DeleteAsync(string sha);
    Task<List<ModInfo>> GetByCategoryAsync(string category);
    Task<List<string>> GetLoadedIdsAsync();
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<List<string>> GetDistinctAuthorsAsync();
    Task<List<string>> GetAllTagsAsync();
    Task<bool> SetLoadedStateAsync(string sha, bool isLoaded);
}

/// <summary>
/// Repository for mod database operations (CRUD)
/// Responsibility: All direct database interactions
/// </summary>
public class ModRepository : IModRepository
{
    private readonly string _connectionString;
    private readonly Lazy<Task> _init;

    public ModRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _init = new Lazy<Task>(InitializeDatabaseAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // Create Mods table
        var createModsCmd = connection.CreateCommand();
        createModsCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Mods (
                SHA TEXT PRIMARY KEY,
                Category TEXT NOT NULL,
                Name TEXT NOT NULL,
                Author TEXT,
                Description TEXT,
                Type TEXT DEFAULT '7z',
                Grading TEXT DEFAULT 'G',
                Tags TEXT,
                DisablePreview INTEGER DEFAULT 0,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                Metadata TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_mods_category ON Mods(Category);
            CREATE INDEX IF NOT EXISTS idx_mods_author ON Mods(Author);
        ";
        await createModsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<List<ModInfo>> GetAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mods = new List<ModInfo>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods ORDER BY SHA";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            mods.Add(MapToModInfo(reader));
        }

        return mods;
    }

    public async Task<ModInfo?> GetByIdAsync(string sha)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapToModInfo(reader);
        }

        return null;
    }

    public async Task<bool> ExistsAsync(string sha)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        var count = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        return count > 0;
    }

    public async Task<ModInfo> InsertAsync(ModInfo mod)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Mods (SHA, Category, Name, Author, Description, Type, Grading, Tags, DisablePreview, Metadata)
            VALUES (@sha, @category, @name, @author, @description, @type, @grading, @tags, @disablePreview, @metadata)
        ";

        command.Parameters.AddWithValue("@sha", mod.SHA);
        command.Parameters.AddWithValue("@category", mod.Category);
        command.Parameters.AddWithValue("@name", mod.Name);
        command.Parameters.AddWithValue("@author", mod.Author ?? string.Empty);
        command.Parameters.AddWithValue("@description", mod.Description ?? string.Empty);
        command.Parameters.AddWithValue("@type", mod.Type);
        command.Parameters.AddWithValue("@grading", mod.Grading);
        command.Parameters.AddWithValue("@tags", JsonHelper.Serialize(mod.Tags));
        command.Parameters.AddWithValue("@disablePreview", mod.DisablePreview ? 1 : 0);
        command.Parameters.AddWithValue("@metadata", (object?)mod.Metadata ?? DBNull.Value);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return mod;
    }

    public async Task<bool> UpdateAsync(ModInfo mod)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Mods SET
                Category = @category,
                Name = @name,
                Author = @author,
                Description = @description,
                Type = @type,
                Grading = @grading,
                Tags = @tags,
                DisablePreview = @disablePreview,
                Metadata = @metadata,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SHA = @sha
        ";

        command.Parameters.AddWithValue("@sha", mod.SHA);
        command.Parameters.AddWithValue("@category", mod.Category);
        command.Parameters.AddWithValue("@name", mod.Name);
        command.Parameters.AddWithValue("@author", mod.Author ?? string.Empty);
        command.Parameters.AddWithValue("@description", mod.Description ?? string.Empty);
        command.Parameters.AddWithValue("@type", mod.Type);
        command.Parameters.AddWithValue("@grading", mod.Grading);
        command.Parameters.AddWithValue("@tags", JsonHelper.Serialize(mod.Tags));
        command.Parameters.AddWithValue("@disablePreview", mod.DisablePreview ? 1 : 0);
        command.Parameters.AddWithValue("@metadata", (object?)mod.Metadata ?? DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string sha)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<List<ModInfo>> GetByCategoryAsync(string category)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mods = new List<ModInfo>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods WHERE Category = @category ORDER BY SHA";
        command.Parameters.AddWithValue("@category", category);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            mods.Add(MapToModInfo(reader));
        }

        return mods;
    }

    public async Task<List<string>> GetLoadedIdsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var shas = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT SHA FROM Mods WHERE IsLoaded = 1";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            shas.Add(reader.GetString(0));
        }

        return shas;
    }

    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var categories = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Category FROM Mods WHERE Category != '' ORDER BY Category";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    public async Task<List<string>> GetDistinctAuthorsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var authors = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Author FROM Mods WHERE Author != '' ORDER BY Author";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            authors.Add(reader.GetString(0));
        }

        return authors;
    }

    public async Task<List<string>> GetAllTagsAsync()
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

    /// <summary>
    /// This method is a no-op placeholder kept for backward compatibility.
    /// IsLoaded is determined dynamically from file system (work directory existence),
    /// not stored in the database. See ModInfo.IsLoaded comment.
    /// </summary>
    public async Task<bool> SetLoadedStateAsync(string sha, bool isLoaded)
    {
        // IsLoaded is not stored in database - it's determined dynamically by checking
        // if work directory exists (see PopulateStatusFlagsBulk in ModFacade)
        // This method is kept for interface compatibility but does nothing
        return await Task.FromResult(true).ConfigureAwait(false);
    }

    private ModInfo MapToModInfo(SqliteDataReader reader)
    {
        var tagsJson = reader.GetString(reader.GetOrdinal("Tags"));
        var tags = string.IsNullOrEmpty(tagsJson)
            ? new List<string>()
            : JsonHelper.Deserialize<List<string>>(tagsJson) ?? new List<string>();

        var disablePreviewOrdinal = reader.GetOrdinal("DisablePreview");
        var disablePreview = !reader.IsDBNull(disablePreviewOrdinal) && reader.GetInt32(disablePreviewOrdinal) == 1;

        var metadataOrdinal = reader.GetOrdinal("Metadata");
        var metadata = reader.IsDBNull(metadataOrdinal) ? null : reader.GetString(metadataOrdinal);

        return new ModInfo
        {
            SHA = reader.GetString(reader.GetOrdinal("SHA")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Author = reader.GetString(reader.GetOrdinal("Author")),
            Description = reader.GetString(reader.GetOrdinal("Description")),
            Type = reader.GetString(reader.GetOrdinal("Type")),
            Grading = reader.GetString(reader.GetOrdinal("Grading")),
            Tags = tags,
            DisablePreview = disablePreview,
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            Metadata = metadata
            // Note: IsLoaded, IsAvailable, preview paths, and thumbnails are populated dynamically from file system
        };
    }
}
