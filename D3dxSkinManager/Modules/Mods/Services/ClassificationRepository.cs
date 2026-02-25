using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for classification repository
/// </summary>
public interface IClassificationRepository
{
    Task<List<ClassificationNode>> GetAllAsync();
    Task<ClassificationNode?> GetByIdAsync(string id);
    Task<List<ClassificationNode>> GetChildrenAsync(string? parentId);
    Task<List<string>> GetAllDescendantIdsAsync(string parentId);
    Task<ClassificationNode?> GetByNameAsync(string name);
    Task<ClassificationNode> InsertAsync(ClassificationNode node);
    Task<bool> UpdateAsync(ClassificationNode node);
    Task<bool> DeleteAsync(string id);
    Task<bool> ExistsAsync(string id);
    Task ClearAllAsync();
    Task<bool> MoveNodeAsync(string nodeId, string? newParentId);
    Task<bool> UpdatePriorityAsync(string nodeId, int priority);
    Task<bool> ReorderSiblingsAsync(List<(string nodeId, int priority)> updates);
}

/// <summary>
/// Repository for classification database operations
/// Manages the classification tree structure in SQLite
/// </summary>
public class ClassificationRepository : IClassificationRepository
{
    private readonly string _connectionString;
    private readonly Lazy<Task> _init;

    public ClassificationRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _init = new Lazy<Task>(InitializeDatabaseAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // Create Classifications table
        var createClassificationsCmd = connection.CreateCommand();
        createClassificationsCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Classifications (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                ParentId TEXT NULL,
                ThumbnailPath TEXT NULL,
                Priority INTEGER DEFAULT 0,
                MatchMode TEXT NULL,
                MatchPattern TEXT NULL,
                Description TEXT NULL,
                Metadata TEXT NULL,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_classifications_parent ON Classifications(ParentId);
            CREATE INDEX IF NOT EXISTS idx_classifications_priority ON Classifications(Priority DESC);
        ";
        await createClassificationsCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Migration: Add MatchMode and MatchPattern columns if they don't exist
        var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = @"
            PRAGMA table_info(Classifications);
        ";
        var columns = new HashSet<string>();
        await using var reader = await alterCmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            columns.Add(reader["name"].ToString()!);
        }

        await using (reader)
        {
            await reader.CloseAsync().ConfigureAwait(false);
        }

        if (!columns.Contains("MatchMode"))
        {
            var addMatchModeCmd = connection.CreateCommand();
            addMatchModeCmd.CommandText = "ALTER TABLE Classifications ADD COLUMN MatchMode TEXT NULL";
            await addMatchModeCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        if (!columns.Contains("MatchPattern"))
        {
            var addMatchPatternCmd = connection.CreateCommand();
            addMatchPatternCmd.CommandText = "ALTER TABLE Classifications ADD COLUMN MatchPattern TEXT NULL";
            await addMatchPatternCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<ClassificationNode>> GetAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var nodes = new List<ClassificationNode>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications ORDER BY Priority DESC, Name";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            nodes.Add(MapToNode(reader));
        }

        return nodes;
    }

    public async Task<ClassificationNode?> GetByIdAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapToNode(reader);
        }

        return null;
    }

    public async Task<List<ClassificationNode>> GetChildrenAsync(string? parentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var nodes = new List<ClassificationNode>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        if (parentId == null)
        {
            command.CommandText = "SELECT * FROM Classifications WHERE ParentId IS NULL ORDER BY Priority DESC, Name";
        }
        else
        {
            command.CommandText = "SELECT * FROM Classifications WHERE ParentId = @parentId ORDER BY Priority DESC, Name";
            command.Parameters.AddWithValue("@parentId", parentId);
        }

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            nodes.Add(MapToNode(reader));
        }

        return nodes;
    }

    /// <summary>
    /// Get all descendant node IDs recursively (children, grandchildren, etc.)
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
            command.CommandText = "SELECT Id FROM Classifications WHERE ParentId = @parentId";
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

    public async Task<ClassificationNode?> GetByNameAsync(string name)
    {

        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications WHERE Name = @name LIMIT 1";
        command.Parameters.AddWithValue("@name", name);

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapToNode(reader);
        }

        return null;
    }

    public async Task<ClassificationNode> InsertAsync(ClassificationNode node)
    {

        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Classifications (Id, Name, ParentId, ThumbnailPath, Priority, MatchMode, MatchPattern, Description, Metadata)
            VALUES (@id, @name, @parentId, @thumbnailPath, @priority, @matchMode, @matchPattern, @description, @metadata)
        ";

        command.Parameters.AddWithValue("@id", node.Id);
        command.Parameters.AddWithValue("@name", node.Name);
        command.Parameters.AddWithValue("@parentId", node.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", node.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", node.Priority);
        command.Parameters.AddWithValue("@matchMode", node.MatchMode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@matchPattern", node.MatchPattern ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@description", node.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", node.Metadata != null ? JsonHelper.Serialize(node.Metadata) : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return node;
    }

    public async Task<bool> UpdateAsync(ClassificationNode node)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET Name = @name,
                ParentId = @parentId,
                ThumbnailPath = @thumbnailPath,
                Priority = @priority,
                MatchMode = @matchMode,
                MatchPattern = @matchPattern,
                Description = @description,
                Metadata = @metadata,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", node.Id);
        command.Parameters.AddWithValue("@name", node.Name);
        command.Parameters.AddWithValue("@parentId", node.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", node.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", node.Priority);
        command.Parameters.AddWithValue("@matchMode", node.MatchMode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@matchPattern", node.MatchPattern ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@description", node.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", node.Metadata != null ? JsonHelper.Serialize(node.Metadata) : (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var count = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        return count > 0;
    }

    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Classifications";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET ParentId = @newParentId
            WHERE Id = @nodeId
        ";

        command.Parameters.AddWithValue("@nodeId", nodeId);
        command.Parameters.AddWithValue("@newParentId", newParentId ?? (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> UpdatePriorityAsync(string nodeId, int priority)
    {
        await EnsureInitializedAsync().ConfigureAwait(false); await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET Priority = @priority
            WHERE Id = @nodeId
        ";

        command.Parameters.AddWithValue("@nodeId", nodeId);
        command.Parameters.AddWithValue("@priority", priority);

        var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task<bool> ReorderSiblingsAsync(List<(string nodeId, int priority)> updates)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (nodeId, priority) in updates)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Classifications
                    SET Priority = @priority
                    WHERE Id = @nodeId
                ";

                command.Parameters.AddWithValue("@nodeId", nodeId);
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

    private ClassificationNode MapToNode(SqliteDataReader reader)
    {
        var metadataJson = reader["Metadata"] as string;
        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrEmpty(metadataJson))
        {
            metadata = JsonHelper.Deserialize<Dictionary<string, object>>(metadataJson);
        }

        return new ClassificationNode
        {
            Id = reader["Id"].ToString() ?? string.Empty,
            Name = reader["Name"].ToString() ?? string.Empty,
            ParentId = reader["ParentId"] as string,
            Thumbnail = reader["ThumbnailPath"] as string,
            Priority = Convert.ToInt32(reader["Priority"]),
            MatchMode = reader["MatchMode"] as string,
            MatchPattern = reader["MatchPattern"] as string,
            Description = reader["Description"] as string,
            Metadata = metadata,
            Children = new List<ClassificationNode>(),
            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString() ?? DateTime.UtcNow.ToString()),
            UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString() ?? DateTime.UtcNow.ToString())
        };
    }
}

