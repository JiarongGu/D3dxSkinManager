namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Builder for DROP COLUMN statements
/// Note: SQLite doesn't support DROP COLUMN directly until version 3.35.0 (2021)
/// For older SQLite versions, this requires table recreation
/// </summary>
public class DeleteColumnBuilder : IDeleteColumnFromTableSyntax
{
    private readonly MigrationContext _context;
    private readonly string _columnName;

    public DeleteColumnBuilder(MigrationContext context, string columnName)
    {
        _context = context;
        _columnName = columnName;
    }

    public void FromTable(string tableName)
    {
        // SQLite 3.35.0+ supports DROP COLUMN
        // For older versions, this would need table recreation (complex)
        _context.AddStatement($"ALTER TABLE {tableName} DROP COLUMN {_columnName}");
    }
}
