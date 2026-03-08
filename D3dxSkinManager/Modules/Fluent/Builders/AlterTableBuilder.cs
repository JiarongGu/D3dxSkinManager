using System.Text;

namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Builder for ALTER TABLE statements
/// </summary>
public class AlterTableBuilder : IAlterTableSyntax, IAlterTableColumnTypeSyntax, IAlterTableColumnOptionsSyntax
{
    private readonly MigrationContext _context;
    private readonly string _tableName;
    private string? _currentColumnName;
    private string? _currentColumnType;
    private readonly List<string> _constraints = new();

    public AlterTableBuilder(MigrationContext context, string tableName)
    {
        _context = context;
        _tableName = tableName;
    }

    public IAlterTableColumnTypeSyntax AddColumn(string columnName)
    {
        _currentColumnName = columnName;
        return this;
    }

    public void RenameTo(string newTableName)
    {
        _context.AddStatement($"ALTER TABLE {_tableName} RENAME TO {newTableName}");
    }

    // Type methods
    public IAlterTableColumnOptionsSyntax AsText()
    {
        _currentColumnType = "TEXT";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsString(int? maxLength = null)
    {
        _currentColumnType = maxLength.HasValue ? $"TEXT({maxLength.Value})" : "TEXT";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsInt32()
    {
        _currentColumnType = "INTEGER";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsInteger() => AsInt32();

    public IAlterTableColumnOptionsSyntax AsInt64()
    {
        _currentColumnType = "INTEGER";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsReal()
    {
        _currentColumnType = "REAL";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsDouble() => AsReal();

    public IAlterTableColumnOptionsSyntax AsBlob()
    {
        _currentColumnType = "BLOB";
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsBoolean()
    {
        _currentColumnType = "INTEGER";
        _constraints.Add($"CHECK ({_currentColumnName} IN (0, 1))");
        return this;
    }

    public IAlterTableColumnOptionsSyntax AsDateTime()
    {
        _currentColumnType = "TEXT";
        return this;
    }

    // Constraint methods
    public IAlterTableColumnOptionsSyntax NotNullable()
    {
        _constraints.Add("NOT NULL");
        return this;
    }

    public IAlterTableColumnOptionsSyntax Nullable()
    {
        return this;
    }

    public IAlterTableColumnOptionsSyntax WithDefaultValue(object value)
    {
        string defaultValue = value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
        _constraints.Add($"DEFAULT {defaultValue}");
        return this;
    }

    public IAlterTableColumnOptionsSyntax WithDefaultCurrentTimestamp()
    {
        _constraints.Add("DEFAULT CURRENT_TIMESTAMP");
        return this;
    }

    public IAlterTableColumnOptionsSyntax Check(string checkExpression)
    {
        _constraints.Add($"CHECK ({checkExpression})");
        return this;
    }

    internal void Complete()
    {
        if (string.IsNullOrEmpty(_currentColumnName) || string.IsNullOrEmpty(_currentColumnType))
        {
            return;
        }

        var sql = new StringBuilder();
        sql.Append($"ALTER TABLE {_tableName} ADD COLUMN {_currentColumnName} {_currentColumnType}");

        foreach (var constraint in _constraints)
        {
            sql.Append($" {constraint}");
        }

        _context.AddStatement(sql.ToString());
    }
}
