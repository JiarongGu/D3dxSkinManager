using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod preset repository
/// </summary>
public interface IModPresetRepository
{
    Task<List<ModPresetEntity>> GetAllAsync();
    Task<ModPresetEntity?> GetByIdAsync(string id);
    Task<ModPresetEntity?> GetByNameAsync(string name);
    Task<ModPresetEntity> InsertAsync(ModPresetEntity entity);
    Task<bool> UpdateAsync(ModPresetEntity entity);
    Task<bool> DeleteAsync(string id);
}

/// <summary>
/// Repository for mod preset database operations (CRUD)
/// Uses Dapper for data access
/// </summary>
public class ModPresetRepository : IModPresetRepository
{
    private readonly string _connectionString;
    private readonly ILogHelper _logger;

    public ModPresetRepository(IProfilePathService profilePaths, ILogHelper logger)
    {
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
        _logger = logger;
    }

    public async Task<List<ModPresetEntity>> GetAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<ModPresetEntity>(
            "SELECT * FROM ModPresets ORDER BY Name");
        return entities.ToList();
    }

    public async Task<ModPresetEntity?> GetByIdAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<ModPresetEntity>(
            "SELECT * FROM ModPresets WHERE Id = @id",
            new { id });
    }

    public async Task<ModPresetEntity?> GetByNameAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<ModPresetEntity>(
            "SELECT * FROM ModPresets WHERE Name = @name",
            new { name });
    }

    public async Task<ModPresetEntity> InsertAsync(ModPresetEntity entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        var sql = @"
            INSERT INTO ModPresets (Id, Name, ModIds, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @ModIds, @CreatedAt, @UpdatedAt)";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name,
            entity.ModIds,
            entity.CreatedAt,
            entity.UpdatedAt
        });
        return entity;
    }

    public async Task<bool> UpdateAsync(ModPresetEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        var sql = @"
            UPDATE ModPresets SET
                Name = @Name,
                ModIds = @ModIds,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name,
            entity.ModIds,
            entity.UpdatedAt
        });
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM ModPresets WHERE Id = @id",
            new { id });
        return rowsAffected > 0;
    }
}
