namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Implementation of ALTER expression root
/// </summary>
public class AlterExpressionRoot : IAlterExpressionRoot
{
    private readonly MigrationContext _context;

    public AlterExpressionRoot(MigrationContext context)
    {
        _context = context;
    }

    public IAlterTableSyntax Table(string tableName)
    {
        var builder = new AlterTableBuilder(_context, tableName);
        _context.RegisterBuilder(builder);
        return builder;
    }
}
