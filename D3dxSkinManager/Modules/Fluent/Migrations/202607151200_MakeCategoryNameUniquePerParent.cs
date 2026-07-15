using FluentMigrator;

namespace D3dxSkinManager.Modules.Fluent.Migrations;

/// <summary>
/// Code-review fix: the original Categories table declared a GLOBAL <c>UNIQUE</c> on <c>Name</c>, which
/// wrongly rejected two sibling categories with the same name under DIFFERENT parents (e.g. a "Skirt"
/// category under both "CharacterA" and "CharacterB"). Replace it with a per-parent unique index.
///
/// SQLite cannot drop a column-level UNIQUE in place, so the table is rebuilt (the documented SQLite
/// workaround). Root categories store <c>ParentId = NULL</c>; a plain <c>UNIQUE(ParentId, Name)</c> would
/// let duplicate roots through (SQLite treats NULLs as distinct), so the index keys on
/// <c>IFNULL(ParentId, '')</c> — folding all roots into one bucket so root names stay unique too.
///
/// Statements are executed one-per-<c>Execute.Sql</c> (robust across providers) and share the migration
/// transaction. Only the original 9 columns exist (no later migration altered Categories), so the copy
/// is lossless. Existing data satisfied the stricter global-unique rule, so it always satisfies the new
/// looser per-parent rule — the rebuild never fails on real data.
/// </summary>
[Migration(202607151200)]
public class _202607151200_MakeCategoryNameUniquePerParent : FluentMigrator.Migration
{
    /// <summary>Rebuild Categories without the global UNIQUE(Name) + add the per-parent unique index.
    /// Exposed for the migration test to run the exact same SQL against a seeded SQLite database.</summary>
    public static readonly string[] UpStatements =
    {
        @"CREATE TABLE Categories_new (
            Id TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            ParentId TEXT,
            ThumbnailPath TEXT,
            Priority INTEGER DEFAULT 0,
            Description TEXT,
            Metadata TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )",
        @"INSERT INTO Categories_new (Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt)
            SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories",
        @"DROP TABLE Categories",
        @"ALTER TABLE Categories_new RENAME TO Categories",
        @"CREATE INDEX idx_Categories_parent ON Categories (ParentId)",
        @"CREATE INDEX idx_Categories_priority ON Categories (Priority DESC)",
        @"CREATE UNIQUE INDEX idx_Categories_parent_name ON Categories (IFNULL(ParentId, ''), Name)",
    };

    /// <summary>Reverse: rebuild WITH the global UNIQUE(Name). Best-effort — fails if the table now holds
    /// two same-named categories under different parents (which the new schema legitimately allows).</summary>
    public static readonly string[] DownStatements =
    {
        @"CREATE TABLE Categories_old (
            Id TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL UNIQUE,
            ParentId TEXT,
            ThumbnailPath TEXT,
            Priority INTEGER DEFAULT 0,
            Description TEXT,
            Metadata TEXT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )",
        @"INSERT INTO Categories_old (Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt)
            SELECT Id, Name, ParentId, ThumbnailPath, Priority, Description, Metadata, CreatedAt, UpdatedAt FROM Categories",
        @"DROP TABLE Categories",
        @"ALTER TABLE Categories_old RENAME TO Categories",
        @"CREATE INDEX idx_Categories_parent ON Categories (ParentId)",
        @"CREATE INDEX idx_Categories_priority ON Categories (Priority DESC)",
    };

    public override void Up()
    {
        foreach (var sql in UpStatements) Execute.Sql(sql);
    }

    public override void Down()
    {
        foreach (var sql in DownStatements) Execute.Sql(sql);
    }
}
