namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Entry point for CREATE operations
/// </summary>
public interface ICreateExpressionRoot
{
    /// <summary>
    /// Start creating a table
    /// </summary>
    ICreateTableWithColumnSyntax Table(string tableName);

    /// <summary>
    /// Create an index
    /// </summary>
    ICreateIndexOnColumnSyntax Index(string indexName);
}
