namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Implementation of DELETE expression root
/// </summary>
public class DeleteExpressionRoot : IDeleteExpressionRoot
{
    private readonly MigrationContext _context;

    public DeleteExpressionRoot(MigrationContext context)
    {
        _context = context;
    }

    public void Table(string tableName)
    {
        _context.AddStatement($"DROP TABLE IF EXISTS {tableName}");
    }

    public void Index(string indexName)
    {
        _context.AddStatement($"DROP INDEX IF EXISTS {indexName}");
    }

    public IDeleteColumnFromTableSyntax Column(string columnName)
    {
        return new DeleteColumnBuilder(_context, columnName);
    }
}
