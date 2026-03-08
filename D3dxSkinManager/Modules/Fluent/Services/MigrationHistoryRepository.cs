using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Fluent.Models;

namespace D3dxSkinManager.Modules.Fluent.Services;

/// <summary>
/// SQLite implementation of migration history repository
/// Tracks which migrations have been applied to this profile's database
/// </summary>
public class MigrationHistoryRepository : IMigrationHistoryRepository
{
    private readonly string _connectionString;
    private const string HistoryTableName = "_MigrationHistory";

    public MigrationHistoryRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
    }

    public async Task EnsureHistoryTableExistsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {HistoryTableName} (
                Version INTEGER PRIMARY KEY,
                Description TEXT,
                MigrationName TEXT,
                AppliedAt TEXT NOT NULL
            );
        ";

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<MigrationRecord>> GetAppliedMigrationsAsync()
    {
        await EnsureHistoryTableExistsAsync();

        var records = new List<MigrationRecord>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT Version, Description, MigrationName, AppliedAt
            FROM {HistoryTableName}
            ORDER BY Version ASC
        ";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new MigrationRecord
            {
                Version = reader.GetInt64(0),
                Description = reader.IsDBNull(1) ? null : reader.GetString(1),
                MigrationName = reader.IsDBNull(2) ? null : reader.GetString(2),
                AppliedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return records;
    }

    public async Task<bool> IsMigrationAppliedAsync(long version)
    {
        await EnsureHistoryTableExistsAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT COUNT(*) FROM {HistoryTableName}
            WHERE Version = @version
        ";
        command.Parameters.AddWithValue("@version", version);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    public async Task RecordMigrationAsync(long version, string? description, string? migrationName)
    {
        await EnsureHistoryTableExistsAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            INSERT INTO {HistoryTableName} (Version, Description, MigrationName, AppliedAt)
            VALUES (@version, @description, @migrationName, @appliedAt)
        ";
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@migrationName", migrationName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }


    /// <summary>
    /// Record migration using an existing connection and transaction
    /// </summary>
    internal async Task RecordMigrationAsync(SqliteConnection connection, SqliteTransaction transaction, long version, string? description, string? migrationName)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
            INSERT INTO {HistoryTableName} (Version, Description, MigrationName, AppliedAt)
            VALUES (@version, @description, @migrationName, @appliedAt)
        ";
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@migrationName", migrationName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Remove migration using an existing connection and transaction
    /// </summary>
    internal async Task RemoveMigrationAsync(SqliteConnection connection, SqliteTransaction transaction, long version)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
            DELETE FROM {HistoryTableName}
            WHERE Version = @version
        ";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync();
    }
    public async Task RemoveMigrationAsync(long version)
    {
        await EnsureHistoryTableExistsAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            DELETE FROM {HistoryTableName}
            WHERE Version = @version
        ";
        command.Parameters.AddWithValue("@version", version);

        await command.ExecuteNonQueryAsync();
    }
}
