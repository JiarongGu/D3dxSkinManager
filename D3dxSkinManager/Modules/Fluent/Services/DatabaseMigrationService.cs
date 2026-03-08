using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Fluent.Services;

/// <summary>
/// Service for managing database migrations at application startup
/// Ensures each profile database is up to date with the latest schema
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Run migrations for the current profile on startup
    /// This should be called during profile initialization
    /// </summary>
    Task RunStartupMigrationsAsync();

    /// <summary>
    /// Check if database needs migration
    /// </summary>
    Task<bool> NeedsMigrationAsync();
}

/// <summary>
/// Implementation of database migration service
/// Coordinates migration runner for profile startup
/// </summary>
public class DatabaseMigrationService : IDatabaseMigrationService
{
    private readonly IMigrationRunner _migrationRunner;
    private readonly ILogHelper _logger;

    public DatabaseMigrationService(
        IMigrationRunner migrationRunner,
        ILogHelper logger)
    {
        _migrationRunner = migrationRunner;
        _logger = logger;
    }

    public async Task RunStartupMigrationsAsync()
    {
        try
        {
            _logger.Info("Starting database migration check", "DatabaseMigrationService");

            var needsMigration = await NeedsMigrationAsync();

            if (!needsMigration)
            {
                _logger.Info("Database is up to date, no migrations needed", "DatabaseMigrationService");
                return;
            }

            _logger.Info("Database needs migration, running pending migrations", "DatabaseMigrationService");
            await _migrationRunner.MigrateToLatestAsync();

            _logger.Info("Database migration completed successfully", "DatabaseMigrationService");
        }
        catch (Exception ex)
        {
            _logger.Error($"Database migration failed: {ex.Message}", "DatabaseMigrationService", ex);
            throw;
        }
    }

    public async Task<bool> NeedsMigrationAsync()
    {
        var isUpToDate = await _migrationRunner.IsDatabaseUpToDateAsync();
        return !isUpToDate;
    }
}
