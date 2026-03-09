using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Moq;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Fluent.Services;

namespace D3dxSkinManager.Tests.Helpers;

/// <summary>
/// Base class for tests that need an in-memory SQLite database with migrations applied
/// Handles database setup, migration execution, and cleanup
/// </summary>
public abstract class InMemoryDatabaseTestBase : IDisposable
{
    protected readonly SqliteConnection Connection;
    protected readonly Mock<IProfilePathService> MockProfilePathService;
    protected readonly Mock<ILogHelper> MockLogger;
    protected readonly string DatabasePath;

    protected InMemoryDatabaseTestBase()
    {
        // Create unique in-memory database name
        var dbName = $"test_{Guid.NewGuid():N}";
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        DatabasePath = connectionString;

        // Keep connection open to maintain in-memory database
        Connection = new SqliteConnection(connectionString);
        Connection.Open();

        MockProfilePathService = new Mock<IProfilePathService>();
        MockProfilePathService.Setup(p => p.ProfileDatabasePath).Returns(dbName);

        MockLogger = new Mock<ILogHelper>();

        // Run migrations to create schema
        RunMigrations();
    }

    /// <summary>
    /// Run all migrations on the in-memory database
    /// </summary>
    private void RunMigrations()
    {
        // Create migration history repository
        var historyRepo = new MigrationHistoryRepository(MockProfilePathService.Object);

        // Create migration runner
        var migrationRunner = new MigrationRunner(
            historyRepo,
            MockProfilePathService.Object,
            MockLogger.Object
        );

        // Run migrations synchronously for test setup
        migrationRunner.MigrateToLatestAsync().GetAwaiter().GetResult();
    }

    public virtual void Dispose()
    {
        Connection?.Close();
        Connection?.Dispose();
    }
}
