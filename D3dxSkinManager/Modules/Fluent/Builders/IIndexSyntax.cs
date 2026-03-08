namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Syntax for creating indexes
/// </summary>
public interface ICreateIndexOnColumnSyntax
{
    /// <summary>
    /// Specify the table to create the index on
    /// </summary>
    ICreateIndexColumnSyntax OnTable(string tableName);
}

/// <summary>
/// Syntax for specifying index columns
/// </summary>
public interface ICreateIndexColumnSyntax
{
    /// <summary>
    /// Add a column to the index
    /// </summary>
    ICreateIndexColumnSyntax OnColumn(string columnName);

    /// <summary>
    /// Make the index unique
    /// </summary>
    ICreateIndexColumnSyntax Unique();

    /// <summary>
    /// Specify ascending order (default)
    /// </summary>
    ICreateIndexColumnSyntax Ascending();

    /// <summary>
    /// Specify descending order
    /// </summary>
    ICreateIndexColumnSyntax Descending();
}
