namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Syntax for deleting columns
/// </summary>
public interface IDeleteColumnFromTableSyntax
{
    /// <summary>
    /// Specify the table to delete the column from
    /// Note: SQLite doesn't support DROP COLUMN directly, requires table recreation
    /// </summary>
    void FromTable(string tableName);
}
