namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Entry point for ALTER operations
/// </summary>
public interface IAlterExpressionRoot
{
    /// <summary>
    /// Start altering a table
    /// </summary>
    IAlterTableSyntax Table(string tableName);
}
