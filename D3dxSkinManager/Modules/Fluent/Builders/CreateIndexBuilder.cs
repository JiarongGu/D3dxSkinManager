using System.Text;

namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Builder for CREATE INDEX statements
/// </summary>
public class CreateIndexBuilder : ICreateIndexOnColumnSyntax, ICreateIndexColumnSyntax
{
    private readonly MigrationContext _context;
    private readonly string _indexName;
    private string? _tableName;
    private readonly List<IndexColumn> _columns = new();
    private bool _isUnique = false;

    public CreateIndexBuilder(MigrationContext context, string indexName)
    {
        _context = context;
        _indexName = indexName;
    }

    public ICreateIndexColumnSyntax OnTable(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    public ICreateIndexColumnSyntax OnColumn(string columnName)
    {
        _columns.Add(new IndexColumn { Name = columnName, Order = "ASC" });
        return this;
    }

    public ICreateIndexColumnSyntax Unique()
    {
        _isUnique = true;
        return this;
    }

    public ICreateIndexColumnSyntax Ascending()
    {
        if (_columns.Count > 0)
        {
            _columns[^1].Order = "ASC";
        }
        return this;
    }

    public ICreateIndexColumnSyntax Descending()
    {
        if (_columns.Count > 0)
        {
            _columns[^1].Order = "DESC";
        }
        return this;
    }

    internal void Complete()
    {
        if (string.IsNullOrEmpty(_tableName) || _columns.Count == 0)
        {
            return;
        }

        var sql = new StringBuilder();
        sql.Append("CREATE ");
        if (_isUnique)
        {
            sql.Append("UNIQUE ");
        }
        sql.Append($"INDEX IF NOT EXISTS {_indexName} ON {_tableName} (");

        for (int i = 0; i < _columns.Count; i++)
        {
            var col = _columns[i];
            sql.Append($"{col.Name} {col.Order}");
            if (i < _columns.Count - 1)
            {
                sql.Append(", ");
            }
        }

        sql.Append(")");

        _context.AddStatement(sql.ToString());
    }

    private class IndexColumn
    {
        public required string Name { get; init; }
        public string Order { get; set; } = "ASC";
    }
}
