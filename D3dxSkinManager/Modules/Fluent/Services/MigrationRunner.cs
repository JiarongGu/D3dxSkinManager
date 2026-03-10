using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Fluent.Services;

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

/// <summary>
/// Service for running database migrations using FluentMigrator
/// </summary>
public class MigrationRunner : IMigrationRunner
{
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly string _connectionString;

    public MigrationRunner(
        IProfilePathService profilePaths,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;

        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    public Task<List<Type>> GetPendingMigrationsAsync()
    {
        try
        {
            var serviceProvider = CreateServices();
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<FluentMigrator.Runner.IMigrationRunner>();
                var versionLoader = scope.ServiceProvider.GetRequiredService<FluentMigrator.Runner.IVersionLoader>();

                // Get all applied migration versions
                var versionInfo = versionLoader.VersionInfo;
                var appliedVersions = versionInfo.AppliedMigrations();

                // Get all available migrations and find unapplied ones
                var allMigrations = runner.MigrationLoader.LoadMigrations();
                var pendingMigrations = allMigrations
                    .Where(m => !appliedVersions.Contains(m.Key))
                    .Select(m => m.Value.Migration.GetType())
                    .ToList();

                return Task.FromResult(pendingMigrations);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to check pending migrations: {ex.Message}", "MigrationRunner", ex);
            // If we can't determine, assume migrations are needed to be safe
            return Task.FromResult(new List<Type> { typeof(object) });
        }
    }

    public async Task<bool> IsDatabaseUpToDateAsync()
    {
        var pending = await GetPendingMigrationsAsync();
        return pending.Count == 0;
    }

    public Task MigrateToLatestAsync()
    {
        _logger.Info("Starting migration to latest version", "MigrationRunner");

        try
        {
            var serviceProvider = CreateServices();
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<FluentMigrator.Runner.IMigrationRunner>();
                runner.MigrateUp();
            }

            _logger.Info("Migration completed successfully", "MigrationRunner");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Migration failed: {ex.Message}", "MigrationRunner", ex);
            throw;
        }
    }

    public Task MigrateToVersionAsync(long targetVersion)
    {
        _logger.Info($"Migrating to version {targetVersion}", "MigrationRunner");

        try
        {
            var serviceProvider = CreateServices();
            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<FluentMigrator.Runner.IMigrationRunner>();
                runner.MigrateUp(targetVersion);
            }

            _logger.Info($"Migration to version {targetVersion} completed successfully", "MigrationRunner");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Migration to version {targetVersion} failed: {ex.Message}", "MigrationRunner", ex);
            throw;
        }
    }

    private IServiceProvider CreateServices()
    {
        // Get the assembly that contains our migrations (not the test assembly)
        var migrationAssembly = typeof(MigrationRunner).Assembly;

        try
        {
            var services = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddSQLite()
                    .WithGlobalConnectionString(_connectionString)
                    .ScanIn(migrationAssembly).For.Migrations())
                .AddLogging(lb => { /* No console logging */ });

            var provider = services.BuildServiceProvider(false);

            _logger.Info($"FluentMigrator configured with connection: {_connectionString}", "MigrationRunner");

            return provider;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to configure FluentMigrator: {ex.Message}", "MigrationRunner", ex);
            throw;
        }
    }
}
