using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Category.Models;
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
    private readonly Lazy<Task> _init;

    public CategoryRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _init = new Lazy<Task>(InitializeDatabaseAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // Create Categories table
        var createCategoriesCmd = connection.CreateCommand();
        createCategoriesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                ParentId TEXT NULL,
                ThumbnailPath TEXT NULL,
                Priority INTEGER DEFAULT 0,
                Description TEXT NULL,
                Metadata TEXT NULL,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_Categories_parent ON Categories(ParentId);
            CREATE INDEX IF NOT EXISTS idx_Categories_priority ON Categories(Priority DESC);
        ";
        await createCategoriesCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<List<CategoryInfo>> GetAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var categories = new List<CategoryInfo>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Categories ORDER BY Priority DESC, Name";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            categories.Add(MapToCategory(reader));
        }

        return categories;
    }

    public async Task<CategoryInfo?> GetByIdAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Categories WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapToCategory(reader);
        }

        return null;
    }

    public async Task<List<CategoryInfo>> GetChildrenAsync(string? parentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var categories = new List<CategoryInfo>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        if (parentId == null)
        {
            command.CommandText = "SELECT * FROM Categories WHERE ParentId IS NULL ORDER BY Priority DESC, Name";
        }
        else
        {
            command.CommandText = "SELECT * FROM Categories WHERE ParentId = @parentId ORDER BY Priority DESC, Name";
            command.Parameters.AddWithValue("@parentId", parentId);
        }

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            categories.Add(MapToCategory(reader));
        }

        return categories;
    }

    /// <summary>
    /// Get all descendant category IDs recursively (children, grandchildren, etc.)
    /// Used for querying mods by parent category (includes all subcategories)
    /// </summary>
    public async Task<List<string>> GetAllDescendantIdsAsync(string parentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var descendantIds = new List<string>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(parentId);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // BFS to collect all descendants
        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            descendantIds.Add(currentId);

            // Get direct children
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id FROM Categories WHERE ParentId = @parentId";
            command.Parameters.AddWithValue("@parentId", currentId);

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var childId = reader["Id"].ToString();
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

        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Categories WHERE Name = @name LIMIT 1";
        command.Parameters.AddWithValue("@name", name);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapToCategory(reader);
        }

        return null;
    }

    public async Task<CategoryInfo> InsertAsync(CategoryInfo category)
    {

        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Categories (Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata)
            VALUES (@id, @name, @parentId, @thumbnailPath, @priority, @description, @metadata)
        ";

        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@parentId", category.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", category.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", category.Priority);
        command.Parameters.AddWithValue("@description", category.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", category.Metadata != null ? JsonHelper.Serialize(category.Metadata) : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return category;
    }

    public async Task<bool> UpdateAsync(CategoryInfo category)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Categories
            SET Name = @name,
                ParentId = @parentId,
                ThumbnailPath = @thumbnailPath,
                Priority = @priority,
                Description = @description,
                Metadata = @metadata,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", category.Id);
        command.Parameters.AddWithValue("@name", category.Name);
        command.Parameters.AddWithValue("@parentId", category.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", category.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", category.Priority);
        command.Parameters.AddWithValue("@description", category.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", category.Metadata != null ? JsonHelper.Serialize(category.Metadata) : (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Categories WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var count = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        return count > 0;
    }

    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<bool> MoveCategoryAsync(string categoryId, string? newParentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Categories
            SET ParentId = @newParentId
            WHERE Id = @categoryId
        ";

        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@newParentId", newParentId ?? (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> UpdatePriorityAsync(string categoryId, int priority)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Categories
            SET Priority = @priority
            WHERE Id = @categoryId
        ";

        command.Parameters.AddWithValue("@categoryId", categoryId);
        command.Parameters.AddWithValue("@priority", priority);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Categories
                    SET Priority = @priority
                    WHERE Id = @categoryId
                ";

                command.Parameters.AddWithValue("@categoryId", categoryId);
                command.Parameters.AddWithValue("@priority", priority);

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    private CategoryInfo MapToCategory(SqliteDataReader reader)
    {
        var metadataJson = reader["Metadata"] as string;
        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrEmpty(metadataJson))
        {
            metadata = JsonHelper.Deserialize<Dictionary<string, object>>(metadataJson);
        }

        return new CategoryInfo
        {
            Id = reader["Id"].ToString() ?? string.Empty,
            Name = reader["Name"].ToString() ?? string.Empty,
            ParentId = reader["ParentId"] as string,
            Thumbnail = reader["ThumbnailPath"] as string,
            Priority = Convert.ToInt32(reader["Priority"]),
            Description = reader["Description"] as string,
            Metadata = metadata,
            Children = new List<CategoryInfo>(),
            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString() ?? DateTime.UtcNow.ToString()),
            UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString() ?? DateTime.UtcNow.ToString())
        };
    }
}

