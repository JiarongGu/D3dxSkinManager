namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Entry point for executing raw SQL
/// </summary>
public interface IExecuteExpressionRoot
{
    /// <summary>
    /// Execute raw SQL statement
    /// </summary>
    void Sql(string sql);
}
