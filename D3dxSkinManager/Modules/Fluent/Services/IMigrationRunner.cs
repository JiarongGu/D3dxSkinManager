namespace D3dxSkinManager.Modules.Fluent.Services;

/// <summary>
/// Service for running database migrations
/// </summary>
public interface IMigrationRunner
{
    /// <summary>
    /// Run all pending migrations for the current profile
    /// </summary>
    Task MigrateToLatestAsync();

    /// <summary>
    /// Migrate to a specific version
    /// </summary>
    Task MigrateToVersionAsync(long targetVersion);

    /// <summary>
    /// Get list of pending migrations
    /// </summary>
    Task<List<Type>> GetPendingMigrationsAsync();

    /// <summary>
    /// Check if database is up to date
    /// </summary>
    Task<bool> IsDatabaseUpToDateAsync();
}
