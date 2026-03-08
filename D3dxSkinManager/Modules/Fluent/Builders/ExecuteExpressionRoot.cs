namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Implementation of EXECUTE expression root
/// </summary>
public class ExecuteExpressionRoot : IExecuteExpressionRoot
{
    private readonly MigrationContext _context;

    public ExecuteExpressionRoot(MigrationContext context)
    {
        _context = context;
    }

    public void Sql(string sql)
    {
        _context.AddStatement(sql);
    }
}
