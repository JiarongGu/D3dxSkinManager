using System;
using Microsoft.Data.Sqlite;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Fluent.Migrations;

namespace D3dxSkinManager.Tests.Modules.Fluent.Migrations;

/// <summary>
/// Verifies the Categories UNIQUE(Name)→per-parent-unique migration by running its exact SQL against a
/// fresh SQLite database seeded with the OLD (global-unique) schema. Locks the intended constraint:
/// same name under DIFFERENT parents is allowed; duplicates under the SAME parent — and duplicate roots
/// (NULL parent, folded via IFNULL) — are still rejected; and the rebuild preserves existing rows.
/// </summary>
public class MakeCategoryNameUniquePerParentTests : IDisposable
{
    private readonly SqliteConnection _conn;

    public MakeCategoryNameUniquePerParentTests()
    {
        _conn = new SqliteConnection($"Data Source=file:catmig_{Guid.NewGuid():N}?mode=memory&cache=shared");
        _conn.Open();

        // OLD schema (as shipped by 202603080002): global UNIQUE on Name.
        Exec(@"CREATE TABLE Categories (
                    Id TEXT NOT NULL PRIMARY KEY,
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
                CREATE INDEX idx_Categories_priority ON Categories(Priority DESC);");

        // Seed two root categories with distinct names (valid under the old rule).
        InsertCat("root1", "CharacterA", null);
        InsertCat("root2", "CharacterB", null);
    }

    public void Dispose()
    {
        _conn.Close();
        _conn.Dispose();
    }

    private void Exec(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void RunMigration(string[] statements)
    {
        foreach (var s in statements) Exec(s);
    }

    private void InsertCat(string id, string name, string? parentId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            @"INSERT INTO Categories (Id, Name, ParentId, Priority, CreatedAt, UpdatedAt)
              VALUES ($id, $name, $parent, 0, '2026-07-15', '2026-07-15')";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$parent", (object?)parentId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private long Count(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public void Up_PreservesRows()
    {
        RunMigration(_202607151200_MakeCategoryNameUniquePerParent.UpStatements);
        Count("SELECT COUNT(*) FROM Categories").Should().Be(2, "the table rebuild must copy all existing rows");
    }

    [Fact]
    public void Up_AllowsSameNameUnderDifferentParents()
    {
        RunMigration(_202607151200_MakeCategoryNameUniquePerParent.UpStatements);

        InsertCat("c1", "Skirt", "root1");
        // Same child name under a DIFFERENT parent — the whole point of the fix.
        var act = () => InsertCat("c2", "Skirt", "root2");
        act.Should().NotThrow("siblings under different parents may share a name");

        Count("SELECT COUNT(*) FROM Categories WHERE Name = 'Skirt'").Should().Be(2);
    }

    [Fact]
    public void Up_RejectsDuplicateNameUnderSameParent()
    {
        RunMigration(_202607151200_MakeCategoryNameUniquePerParent.UpStatements);

        InsertCat("c1", "Skirt", "root1");
        var act = () => InsertCat("c2", "Skirt", "root1");
        act.Should().Throw<SqliteException>("two children of the SAME parent may not share a name");
    }

    [Fact]
    public void Up_RejectsDuplicateRootNames()
    {
        RunMigration(_202607151200_MakeCategoryNameUniquePerParent.UpStatements);

        // Both roots (NULL parent) — IFNULL(ParentId,'') folds them into one key so names stay unique.
        var act = () => InsertCat("root3", "CharacterA", null);
        act.Should().Throw<SqliteException>("root categories must keep unique names");
    }
}
