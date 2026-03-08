using System.Reflection;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Fluent.Builders;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Fluent.Services;

/// <summary>
/// Service for discovering and running database migrations
/// </summary>
public class MigrationRunner : IMigrationRunner
{
    private readonly IMigrationHistoryRepository _historyRepository;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly string _connectionString;
    private readonly List<Type> _migrationTypes;

    public MigrationRunner(
        IMigrationHistoryRepository historyRepository,
        IProfilePathService profilePaths,
        ILogHelper logger)
    {
        _historyRepository = historyRepository;
        _profilePaths = profilePaths;
        _logger = logger;
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";

        // Discover all migration classes in the assembly
        _migrationTypes = DiscoverMigrations();
    }

    /// <summary>
    /// Discover all migration classes marked with [Migration] attribute
    /// </summary>
    private List<Type> DiscoverMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var migrations = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Migration)))
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() != null)
            .OrderBy(t => t.GetCustomAttribute<MigrationAttribute>()!.Version)
            .ToList();

        _logger.Info($"Discovered {migrations.Count} migrations", "MigrationRunner");
        return migrations;
    }

    public async Task<List<Type>> GetPendingMigrationsAsync()
    {
        var appliedMigrations = await _historyRepository.GetAppliedMigrationsAsync();
        var appliedVersions = new HashSet<long>(appliedMigrations.Select(m => m.Version));

        var pending = _migrationTypes
            .Where(t =>
            {
                var attr = t.GetCustomAttribute<MigrationAttribute>();
                return attr != null && !appliedVersions.Contains(attr.Version);
            })
            .ToList();

        return pending;
    }

    public async Task<bool> IsDatabaseUpToDateAsync()
    {
        var pending = await GetPendingMigrationsAsync();
        return pending.Count == 0;
    }

    public async Task MigrateToLatestAsync()
    {
        // Yield to prevent blocking UI thread

        _logger.Info("Starting migration to latest version", "MigrationRunner");

        var pendingMigrations = await GetPendingMigrationsAsync();

        if (pendingMigrations.Count == 0)
        {
            _logger.Info("Database is already up to date", "MigrationRunner");
            return;
        }

        _logger.Info($"Found {pendingMigrations.Count} pending migrations", "MigrationRunner");

        foreach (var migrationType in pendingMigrations)
        {
            await ExecuteMigrationAsync(migrationType, isUp: true);
        }

        _logger.Info("Migration completed successfully", "MigrationRunner");
    }

    public async Task MigrateToVersionAsync(long targetVersion)
    {

        var appliedMigrations = await _historyRepository.GetAppliedMigrationsAsync();
        var currentVersion = appliedMigrations.LastOrDefault()?.Version ?? 0;

        if (targetVersion > currentVersion)
        {
            // Migrate up
            var migrationsToApply = _migrationTypes
                .Where(t =>
                {
                    var attr = t.GetCustomAttribute<MigrationAttribute>();
                    return attr != null && attr.Version > currentVersion && attr.Version <= targetVersion;
                })
                .ToList();

            foreach (var migrationType in migrationsToApply)
            {
                await ExecuteMigrationAsync(migrationType, isUp: true);
            }
        }
        else if (targetVersion < currentVersion)
        {
            // Migrate down
            var migrationsToRollback = appliedMigrations
                .Where(m => m.Version > targetVersion)
                .OrderByDescending(m => m.Version)
                .Select(m => _migrationTypes.FirstOrDefault(t =>
                    t.GetCustomAttribute<MigrationAttribute>()?.Version == m.Version))
                .Where(t => t != null)
                .Cast<Type>()
                .ToList();

            foreach (var migrationType in migrationsToRollback)
            {
                await ExecuteMigrationAsync(migrationType, isUp: false);
            }
        }
    }

    /// <summary>
    /// Execute a single migration (up or down)
    /// </summary>
    private async Task ExecuteMigrationAsync(Type migrationType, bool isUp)
    {
        var attribute = migrationType.GetCustomAttribute<MigrationAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException($"Migration {migrationType.Name} is missing [Migration] attribute");
        }

        var direction = isUp ? "UP" : "DOWN";
        _logger.Info($"Executing migration {attribute.Version} ({migrationType.Name}) - {direction}", "MigrationRunner");

        try
        {
            // Create migration instance
            var migration = (Migration?)Activator.CreateInstance(migrationType);
            if (migration == null)
            {
                throw new InvalidOperationException($"Failed to create instance of {migrationType.Name}");
            }

            // Create migration context and expression roots
            var context = new MigrationContext();
            var create = new CreateExpressionRoot(context);
            var delete = new DeleteExpressionRoot(context);
            var alter = new AlterExpressionRoot(context);
            var execute = new ExecuteExpressionRoot(context);

            migration.SetExpressionRoots(create, delete, alter, execute);

            // Execute Up() or Down()
            if (isUp)
            {
                migration.Up();
            }
            else
            {
                migration.Down();
            }

            // Finalize any pending builders
            context.CompleteBuilders();

            // Get generated SQL
            var sqlStatements = context.GetStatements();

            _logger.Info($"Generated {sqlStatements.Count} SQL statements", "MigrationRunner");
            if (sqlStatements.Count == 0)
            {
                _logger.Warn($"Migration {attribute.Version} generated no SQL statements", "MigrationRunner");
                return;
            }

            // Execute SQL statements
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var sql in sqlStatements)
                {
                    _logger.Verbose($"Executing: {sql}", "MigrationRunner");

                    var command = connection.CreateCommand();
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                }

                // Update migration history (using same connection/transaction to avoid deadlock)
                var historyRepo = (MigrationHistoryRepository)_historyRepository;
                if (isUp)
                {
                    await historyRepo.RecordMigrationAsync(
                        connection,
                        (SqliteTransaction)transaction,
                        attribute.Version,
                        attribute.Description,
                        migrationType.Name);
                }
                else
                {
                    await historyRepo.RemoveMigrationAsync(
                        connection,
                        (SqliteTransaction)transaction,
                        attribute.Version);
                }

                await transaction.CommitAsync();

                _logger.Info($"Migration {attribute.Version} completed successfully", "MigrationRunner");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error($"Migration {attribute.Version} failed: {ex.Message}", "MigrationRunner", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to execute migration {attribute.Version}: {ex.Message}", "MigrationRunner", ex);
            throw;
        }
    }
}
