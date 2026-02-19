using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Mods.Services;

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

    public ModRepository(IProfileContext profileContext)
    {
        _connectionString = $"Data Source={Path.Combine(profileContext.ProfilePath, "mods.db")}";
        InitializeDatabaseAsync().Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Mods (
                SHA TEXT PRIMARY KEY,
                Category TEXT NOT NULL,
                Name TEXT NOT NULL,
                Author TEXT,
                Description TEXT,
                Type TEXT DEFAULT '7z',
                Grading TEXT DEFAULT 'G',
                Tags TEXT,
                IsLoaded INTEGER DEFAULT 0,
                IsAvailable INTEGER DEFAULT 0,
                ThumbnailPath TEXT,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_category ON Mods(Category);
            CREATE INDEX IF NOT EXISTS idx_is_loaded ON Mods(IsLoaded);
            CREATE INDEX IF NOT EXISTS idx_author ON Mods(Author);
        ";
        await createTableCmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ModInfo>> GetAllAsync()
    {
        var mods = new List<ModInfo>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods ORDER BY Category, Name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            mods.Add(MapToModInfo(reader));
        }

        return mods;
    }

    public async Task<ModInfo?> GetByIdAsync(string sha)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToModInfo(reader);
        }

        return null;
    }

    public async Task<bool> ExistsAsync(string sha)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    public async Task<ModInfo> InsertAsync(ModInfo mod)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Mods (SHA, Category, Name, Author, Description, Type, Grading, Tags, IsLoaded, IsAvailable, ThumbnailPath)
            VALUES (@sha, @category, @name, @author, @description, @type, @grading, @tags, @isLoaded, @isAvailable, @thumbnailPath)
        ";

        command.Parameters.AddWithValue("@sha", mod.SHA);
        command.Parameters.AddWithValue("@category", mod.Category);
        command.Parameters.AddWithValue("@name", mod.Name);
        command.Parameters.AddWithValue("@author", mod.Author ?? string.Empty);
        command.Parameters.AddWithValue("@description", mod.Description ?? string.Empty);
        command.Parameters.AddWithValue("@type", mod.Type);
        command.Parameters.AddWithValue("@grading", mod.Grading);
        command.Parameters.AddWithValue("@tags", JsonConvert.SerializeObject(mod.Tags));
        command.Parameters.AddWithValue("@isLoaded", mod.IsLoaded ? 1 : 0);
        command.Parameters.AddWithValue("@isAvailable", mod.IsAvailable ? 1 : 0);
        command.Parameters.AddWithValue("@thumbnailPath", mod.ThumbnailPath ?? string.Empty);

        await command.ExecuteNonQueryAsync();
        return mod;
    }

    public async Task<bool> UpdateAsync(ModInfo mod)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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
                IsLoaded = @isLoaded,
                IsAvailable = @isAvailable,
                ThumbnailPath = @thumbnailPath,
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
        command.Parameters.AddWithValue("@tags", JsonConvert.SerializeObject(mod.Tags));
        command.Parameters.AddWithValue("@isLoaded", mod.IsLoaded ? 1 : 0);
        command.Parameters.AddWithValue("@isAvailable", mod.IsAvailable ? 1 : 0);
        command.Parameters.AddWithValue("@thumbnailPath", mod.ThumbnailPath ?? string.Empty);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string sha)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Mods WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<List<ModInfo>> GetByCategoryAsync(string category)
    {
        var mods = new List<ModInfo>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Mods WHERE Category = @category ORDER BY Name";
        command.Parameters.AddWithValue("@category", category);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            mods.Add(MapToModInfo(reader));
        }

        return mods;
    }

    public async Task<List<string>> GetLoadedIdsAsync()
    {
        var shas = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT SHA FROM Mods WHERE IsLoaded = 1";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            shas.Add(reader.GetString(0));
        }

        return shas;
    }

    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        var categories = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Category FROM Mods WHERE Category != '' ORDER BY Category";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    public async Task<List<string>> GetDistinctAuthorsAsync()
    {
        var authors = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Author FROM Mods WHERE Author != '' ORDER BY Author";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            authors.Add(reader.GetString(0));
        }

        return authors;
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        var allTags = new HashSet<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Tags FROM Mods WHERE Tags != ''";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tagsJson = reader.GetString(0);
            if (!string.IsNullOrEmpty(tagsJson))
            {
                var tags = JsonConvert.DeserializeObject<List<string>>(tagsJson);
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

    public async Task<bool> SetLoadedStateAsync(string sha, bool isLoaded)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Mods SET IsLoaded = @isLoaded, UpdatedAt = CURRENT_TIMESTAMP WHERE SHA = @sha";
        command.Parameters.AddWithValue("@sha", sha);
        command.Parameters.AddWithValue("@isLoaded", isLoaded ? 1 : 0);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    private ModInfo MapToModInfo(SqliteDataReader reader)
    {
        var tagsJson = reader.GetString(reader.GetOrdinal("Tags"));
        var tags = string.IsNullOrEmpty(tagsJson)
            ? new List<string>()
            : JsonConvert.DeserializeObject<List<string>>(tagsJson) ?? new List<string>();

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
            IsLoaded = reader.GetInt32(reader.GetOrdinal("IsLoaded")) == 1,
            IsAvailable = reader.GetInt32(reader.GetOrdinal("IsAvailable")) == 1,
            ThumbnailPath = reader.GetString(reader.GetOrdinal("ThumbnailPath"))
            // Note: Preview paths scanned dynamically from previews/{SHA}/ folder
        };
    }
}
