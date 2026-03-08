namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Implementation of CREATE expression root
/// </summary>
public class CreateExpressionRoot : ICreateExpressionRoot
{
    private readonly MigrationContext _context;

    public CreateExpressionRoot(MigrationContext context)
    {
        _context = context;
    }

    public ICreateTableWithColumnSyntax Table(string tableName)
    {
        var builder = new CreateTableBuilder(_context, tableName);
        _context.RegisterBuilder(builder);
        return builder;
    }

    public ICreateIndexOnColumnSyntax Index(string indexName)
    {
        var builder = new CreateIndexBuilder(_context, indexName);
        _context.RegisterBuilder(builder);
        return builder;
    }
}
