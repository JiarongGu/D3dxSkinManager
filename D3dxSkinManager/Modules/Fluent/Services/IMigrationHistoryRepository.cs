using D3dxSkinManager.Modules.Fluent.Models;

namespace D3dxSkinManager.Modules.Fluent.Services;

/// <summary>
/// Repository for tracking migration history in the database
/// </summary>
public interface IMigrationHistoryRepository
{
    /// <summary>
    /// Ensure the migration history table exists
    /// </summary>
    Task EnsureHistoryTableExistsAsync();

    /// <summary>
    /// Get all applied migrations ordered by version
    /// </summary>
    Task<List<MigrationRecord>> GetAppliedMigrationsAsync();

    /// <summary>
    /// Check if a specific migration version has been applied
    /// </summary>
    Task<bool> IsMigrationAppliedAsync(long version);

    /// <summary>
    /// Record that a migration has been applied
    /// </summary>
    Task RecordMigrationAsync(long version, string? description, string? migrationName);

    /// <summary>
    /// Remove a migration record (for rollback)
    /// </summary>
    Task RemoveMigrationAsync(long version);
}
