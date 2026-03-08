namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Entry point for DELETE operations
/// </summary>
public interface IDeleteExpressionRoot
{
    /// <summary>
    /// Delete a table
    /// </summary>
    void Table(string tableName);

    /// <summary>
    /// Delete an index
    /// </summary>
    void Index(string indexName);

    /// <summary>
    /// Start deleting a column from a table
    /// </summary>
    IDeleteColumnFromTableSyntax Column(string columnName);
}
