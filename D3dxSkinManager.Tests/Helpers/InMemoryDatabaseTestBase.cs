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
        // IMPORTANT: Return the full connection string so MigrationRunner connects to the same in-memory database
        MockProfilePathService.Setup(p => p.ProfileDatabasePath).Returns(connectionString);

        MockLogger = new Mock<ILogHelper>();

        // Run migrations to create schema
        RunMigrations();
    }

    /// <summary>
    /// Run all migrations on the in-memory database
    /// </summary>
    private void RunMigrations()
    {
        // FluentMigrator has issues with in-memory databases in tests
        // Use direct SQL to create schema for tests instead
        var sql = @"
            CREATE TABLE Mods (
                Id TEXT PRIMARY KEY NOT NULL,
                Category TEXT NOT NULL,
                Name TEXT NOT NULL,
                Author TEXT,
                Description TEXT,
                Type TEXT DEFAULT '7z',
                Grading TEXT DEFAULT 'G',
                Tags TEXT,
                DisablePreview INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                Metadata TEXT,
                RemoteLibraryId TEXT
            );

            CREATE INDEX idx_mods_category ON Mods(Category);
            CREATE INDEX idx_mods_author ON Mods(Author);

            CREATE TABLE Categories (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL UNIQUE,
                ParentId TEXT,
                ThumbnailPath TEXT,
                Priority INTEGER DEFAULT 0,
                Description TEXT,
                Metadata TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX idx_Categories_parent ON Categories(ParentId);
            CREATE INDEX idx_Categories_priority ON Categories(Priority DESC);

            CREATE TABLE Tags (
                Name TEXT PRIMARY KEY NOT NULL,
                Color TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX idx_tags_name ON Tags(Name);

            CREATE TABLE Workflows (
                Id TEXT PRIMARY KEY NOT NULL,
                Type TEXT NOT NULL,
                Status INTEGER NOT NULL,
                Context TEXT NOT NULL DEFAULT '{}',
                ErrorMessage TEXT,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT
            );

            CREATE INDEX idx_workflows_type ON Workflows(Type);
            CREATE INDEX idx_workflows_status ON Workflows(Status);

            CREATE TABLE ScreenCaptureProfiles (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                X INTEGER NOT NULL,
                Y INTEGER NOT NULL,
                Width INTEGER NOT NULL,
                Height INTEGER NOT NULL
            );

            CREATE INDEX IX_ScreenCaptureProfiles_Name ON ScreenCaptureProfiles(Name);
        ";

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public virtual void Dispose()
    {
        Connection?.Close();
        Connection?.Dispose();
    }
}
