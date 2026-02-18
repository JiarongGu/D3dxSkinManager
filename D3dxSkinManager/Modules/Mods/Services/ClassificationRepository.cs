using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for classification repository
/// </summary>
public interface IClassificationRepository
{
    Task<List<ClassificationNode>> GetAllAsync();
    Task<ClassificationNode?> GetByIdAsync(string id);
    Task<List<ClassificationNode>> GetChildrenAsync(string? parentId);
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

    public ClassificationRepository(string dataPath)
    {
        var dbPath = Path.Combine(dataPath, "classifications.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabaseAsync().Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Classifications (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentId TEXT NULL,
                ThumbnailPath TEXT NULL,
                Priority INTEGER DEFAULT 0,
                Description TEXT NULL,
                Metadata TEXT NULL,
                FOREIGN KEY (ParentId) REFERENCES Classifications(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_classifications_parent ON Classifications(ParentId);
            CREATE INDEX IF NOT EXISTS idx_classifications_name ON Classifications(Name);
        ";
        await createTableCmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ClassificationNode>> GetAllAsync()
    {
        var nodes = new List<ClassificationNode>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications ORDER BY Priority DESC, Name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nodes.Add(MapToNode(reader));
        }

        return nodes;
    }

    public async Task<ClassificationNode?> GetByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToNode(reader);
        }

        return null;
    }

    public async Task<List<ClassificationNode>> GetChildrenAsync(string? parentId)
    {
        var nodes = new List<ClassificationNode>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            nodes.Add(MapToNode(reader));
        }

        return nodes;
    }

    public async Task<ClassificationNode?> GetByNameAsync(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Classifications WHERE Name = @name LIMIT 1";
        command.Parameters.AddWithValue("@name", name);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToNode(reader);
        }

        return null;
    }

    public async Task<ClassificationNode> InsertAsync(ClassificationNode node)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Classifications (Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata)
            VALUES (@id, @name, @parentId, @thumbnailPath, @priority, @description, @metadata)
        ";

        command.Parameters.AddWithValue("@id", node.Id);
        command.Parameters.AddWithValue("@name", node.Name);
        command.Parameters.AddWithValue("@parentId", node.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", node.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", node.Priority);
        command.Parameters.AddWithValue("@description", node.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", node.Metadata != null ? JsonConvert.SerializeObject(node.Metadata) : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
        return node;
    }

    public async Task<bool> UpdateAsync(ClassificationNode node)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET Name = @name,
                ParentId = @parentId,
                ThumbnailPath = @thumbnailPath,
                Priority = @priority,
                Description = @description,
                Metadata = @metadata
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", node.Id);
        command.Parameters.AddWithValue("@name", node.Name);
        command.Parameters.AddWithValue("@parentId", node.ParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@thumbnailPath", node.Thumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@priority", node.Priority);
        command.Parameters.AddWithValue("@description", node.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@metadata", node.Metadata != null ? JsonConvert.SerializeObject(node.Metadata) : (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Classifications WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    public async Task ClearAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Classifications";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET ParentId = @newParentId
            WHERE Id = @nodeId
        ";

        command.Parameters.AddWithValue("@nodeId", nodeId);
        command.Parameters.AddWithValue("@newParentId", newParentId ?? (object)DBNull.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> UpdatePriorityAsync(string nodeId, int priority)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Classifications
            SET Priority = @priority
            WHERE Id = @nodeId
        ";

        command.Parameters.AddWithValue("@nodeId", nodeId);
        command.Parameters.AddWithValue("@priority", priority);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> ReorderSiblingsAsync(List<(string nodeId, int priority)> updates)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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

                await command.ExecuteNonQueryAsync();
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
            metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(metadataJson);
        }

        return new ClassificationNode
        {
            Id = reader["Id"].ToString() ?? string.Empty,
            Name = reader["Name"].ToString() ?? string.Empty,
            ParentId = reader["ParentId"] as string,
            Thumbnail = reader["ThumbnailPath"] as string,
            Priority = Convert.ToInt32(reader["Priority"]),
            Description = reader["Description"] as string,
            Metadata = metadata,
            Children = new List<ClassificationNode>()
        };
    }
}
