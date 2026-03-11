# Fluent Database Migration Module

A fluent API for managing SQLite database schema migrations in the D3dxSkinManager application.

## Overview

The Fluent module provides:
1. **Base Schema Reference** - Migrations define the canonical schema for new databases
2. **Migration History Tracking** - `_MigrationHistory` table tracks applied migrations per profile
3. **Fluent API** - FluentMigrator-style API for defining schema changes
4. **Automatic Startup Execution** - Migrations run automatically when a profile starts

## Architecture

### Components

- **Migration** - Base class for all migrations
- **MigrationAttribute** - Marks migration classes with version numbers
- **MigrationRunner** - Discovers and executes migrations
- **MigrationHistoryRepository** - Tracks applied migrations
- **DatabaseMigrationService** - Integrates migrations into startup process

### Design

- **Profile-scoped**: Each profile has its own database and migration history
- **Version-based**: Migrations use `YYYYMMDDHHmm` format (e.g., `202603081735`)
- **Transactional**: Each migration runs in a transaction (all-or-nothing)
- **Discovery**: Migrations are discovered via reflection at runtime
- **Synchronous execution**: Migrations run synchronously during profile initialization using `.GetAwaiter().GetResult()` to avoid async deadlock
- **Single connection**: Migration history updates use the same connection/transaction to prevent SQLite deadlock

## Usage

### Creating a Migration

```csharp
using D3dxSkinManager.Modules.Fluent;
using D3dxSkinManager.Modules.Fluent.Migrations;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

[Migration(202603081735, Description = "Add IsActive column to Mods table")]
public class Migration_202603081735_AddIsActiveToMods : Migration
{
    public override void Up()
    {
        Alter.Table("Mods")
            .AddColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true);
    }

    public override void Down()
    {
        // SQLite 3.35.0+ only
        Delete.Column("IsActive").FromTable("Mods");
    }
}
```

### Fluent API Reference

#### CREATE TABLE

```csharp
Create.Table("TableName")
    .WithColumn("Id").AsInteger().NotNullable().PrimaryKey().Identity()
    .WithColumn("Name").AsString(100).NotNullable()
    .WithColumn("Email").AsString(255).Nullable().Unique()
    .WithColumn("Age").AsInteger().Nullable()
    .WithColumn("Balance").AsReal().WithDefaultValue(0.0)
    .WithColumn("IsActive").AsBoolean().WithDefaultValue(true)
    .WithColumn("CreatedAt").AsDateTime().WithDefaultCurrentTimestamp()
    .WithColumn("Metadata").AsText().Nullable();
```

#### CREATE INDEX

```csharp
Create.Index("idx_table_column")
    .OnTable("TableName")
    .OnColumn("ColumnName")
    .Descending();

Create.Index("idx_table_unique")
    .OnTable("TableName")
    .OnColumn("Email")
    .Unique();
```

#### ALTER TABLE

```csharp
// Add column
Alter.Table("TableName")
    .AddColumn("NewColumn").AsText().Nullable();

// Rename table
Alter.Table("OldName")
    .RenameTo("NewName");
```

#### DROP OPERATIONS

```csharp
// Drop table
Delete.Table("TableName");

// Drop index
Delete.Index("idx_name");

// Drop column (SQLite 3.35.0+)
Delete.Column("ColumnName").FromTable("TableName");
```

#### RAW SQL

```csharp
Execute.Sql("UPDATE Mods SET IsActive = 1 WHERE Category = 'Character'");
```

### Column Types

| Method | SQLite Type | Notes |
|--------|-------------|-------|
| `AsText()` | TEXT | Unlimited length |
| `AsString(n)` | TEXT | Documented max length |
| `AsInteger()` | INTEGER | 32/64-bit integer |
| `AsInt32()` | INTEGER | Alias for AsInteger |
| `AsInt64()` | INTEGER | SQLite uses INTEGER for all ints |
| `AsReal()` | REAL | Floating point |
| `AsDouble()` | REAL | Alias for AsReal |
| `AsBoolean()` | INTEGER | 0/1 with CHECK constraint |
| `AsDateTime()` | TEXT | ISO8601 format |
| `AsBlob()` | BLOB | Binary data |

### Column Constraints

```csharp
.NotNullable()                          // NOT NULL
.Nullable()                             // NULL (default)
.PrimaryKey()                           // PRIMARY KEY
.Unique()                               // UNIQUE
.Identity()                             // AUTOINCREMENT (INTEGER PRIMARY KEY only)
.WithDefaultValue(value)                // DEFAULT value
.WithDefaultCurrentTimestamp()          // DEFAULT CURRENT_TIMESTAMP
.ForeignKey("RefTable", "RefColumn")    // REFERENCES RefTable(RefColumn)
.Check("Age > 0")                       // CHECK (Age > 0)
.Collate("NOCASE")                      // COLLATE NOCASE
```

## Migration Versioning

**Format:** `YYYYMMDDHHmm` (12 digits)

- **YYYY** - Year (2026)
- **MM** - Month (03 = March)
- **DD** - Day (08)
- **HH** - Hour (17 = 5 PM)
- **mm** - Minute (35)

**Example:** `202603081735` = March 8, 2026 at 5:35 PM

**Convention:**
- Use current timestamp when creating migration
- Ensures chronological ordering
- Prevents version conflicts in team environments

## Base Schema Migrations

The module includes base schema migrations for existing tables:

1. **202603080001** - Create Mods table
2. **202603080002** - Create Categories table
3. **202603080003** - Create Tags table
4. **202603080004** - Create Workflows table

These migrations serve as:
- Reference schema for new databases
- Migration starting point for existing databases
- Documentation of current schema

## Integration

### Service Registration

```csharp
// In Program.cs or service configuration
services.AddFluentMigrationServices();
```

### Startup Integration

Migrations are automatically executed in `ProfileServiceRouter.CreateProfileServices()`:

```csharp
// In ProfileServiceRouter.cs - CreateProfileServices()
var serviceProvider = services.BuildServiceProvider();

// Run database migrations for this profile
var migrationService = serviceProvider.GetService<IDatabaseMigrationService>();
if (migrationService != null)
{
    try
    {
        // Executes synchronously to avoid async deadlock
        migrationService.RunStartupMigrationsAsync().GetAwaiter().GetResult();
        _logger.Info($"Database migrations completed for profile: {profile.Name}", "ProfileServiceRouter");
    }
    catch (Exception ex)
    {
        _logger.Error($"Database migration failed for profile {profile.Name}: {ex.Message}", "ProfileServiceRouter", ex);
        throw; // Re-throw to prevent service creation with invalid schema
    }
}
```

**Key Implementation Details:**
- Migrations run **synchronously** using `.GetAwaiter().GetResult()` to maintain thread affinity for SQLite transactions
- Executed **before** other profile services are initialized to ensure schema is ready
- Migration history updates use the **same connection and transaction** to avoid SQLite deadlock
- Repositories **no longer** contain `CREATE TABLE IF NOT EXISTS` code - schema is entirely managed by migrations

## Migration History

The `_MigrationHistory` table tracks applied migrations:

```sql
CREATE TABLE _MigrationHistory (
    Version INTEGER PRIMARY KEY,
    Description TEXT,
    MigrationName TEXT,
    AppliedAt TEXT NOT NULL
);
```

**Behavior:**
- New database: All migrations run in order
- Existing database: Only pending migrations run
- Manual rollback: Use `MigrationRunner.MigrateToVersionAsync(targetVersion)`

## Best Practices

### 1. Never Modify Existing Migrations
Once a migration is committed and deployed, never change it. Create a new migration instead.

```csharp
// ❌ DON'T: Modify existing migration
[Migration(202603080001)]
public class Migration_202603080001_CreateModsTable : Migration
{
    public override void Up()
    {
        Create.Table("Mods")
            .WithColumn("Id").AsText().PrimaryKey()
            .WithColumn("NewColumn").AsText(); // ❌ Added later
    }
}

// ✅ DO: Create new migration
[Migration(202603081800)]
public class Migration_202603081800_AddNewColumnToMods : Migration
{
    public override void Up()
    {
        Alter.Table("Mods")
            .AddColumn("NewColumn").AsText().Nullable();
    }
}
```

### 2. Always Provide Down() When Possible
Allow rollback by implementing `Down()`:

```csharp
public override void Down()
{
    Delete.Table("NewTable");
}
```

### 3. Use Transactions
Migrations run in transactions automatically. Keep migrations focused and atomic.

### 4. Test Migrations
- Test on a copy of production database
- Test both Up() and Down()
- Verify data integrity after migration

### 5. Default Values for NOT NULL Columns
When adding NOT NULL columns to existing tables, always provide a default:

```csharp
Alter.Table("Mods")
    .AddColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true);
```

### 6. Name Migrations Descriptively
Use clear, descriptive names:

```csharp
// ✅ Good
Migration_202603081735_AddIsActiveToModsTable
Migration_202603081800_CreateUserPreferencesTable
Migration_202603081815_AddIndexOnModCategory

// ❌ Bad
Migration_202603081735_Update
Migration_202603081800_Fix
Migration_202603081815_Changes
```

## Troubleshooting

### Migration Fails

**Check:**
1. SQL syntax errors in generated statements
2. Constraint violations (NOT NULL without default, etc.)
3. Table/column already exists
4. SQLite version compatibility

**Solution:**
- Review error logs
- Check `_MigrationHistory` table
- Manually rollback if needed
- Create compensating migration

### Database Out of Sync

**Symptoms:**
- Missing tables/columns
- Migration errors on startup

**Solution:**
1. Check `_MigrationHistory` table
2. Compare with expected migrations
3. Run `MigrateToLatestAsync()` manually
4. If corrupted, restore from backup

### Version Conflicts

**Symptoms:**
- Two migrations with same version number

**Prevention:**
- Use current timestamp when creating migrations
- Check existing migrations before committing

## Future Enhancements

Potential improvements:

1. **Data Migrations** - Support for data transformation migrations
2. **Schema Validation** - Compare actual schema vs expected schema
3. **Migration Rollback UI** - UI for manual rollback operations
4. **Migration Preview** - Show SQL before execution
5. **Migration Dependencies** - Explicit migration dependencies
6. **Seed Data** - Support for initial data seeding

## SQLite Limitations

**DROP COLUMN:**
- Only supported in SQLite 3.35.0+ (2021)
- Older versions require table recreation

**ALTER COLUMN:**
- Not supported in SQLite
- Requires table recreation

**Workaround for table recreation:**
```csharp
Execute.Sql(@"
    CREATE TABLE Mods_New (...);
    INSERT INTO Mods_New SELECT ... FROM Mods;
    DROP TABLE Mods;
    ALTER TABLE Mods_New RENAME TO Mods;
");
```

## References

- **FluentMigrator** - Inspiration for fluent API design
- **Entity Framework Migrations** - Inspiration for history tracking
- **SQLite Documentation** - https://www.sqlite.org/lang.html
