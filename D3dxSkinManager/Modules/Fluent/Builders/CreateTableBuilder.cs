using System.Text;

namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Builder for CREATE TABLE statements
/// </summary>
public class CreateTableBuilder : ICreateTableWithColumnSyntax, ICreateTableColumnTypeSyntax, ICreateTableColumnOptionsSyntax
{
    private readonly MigrationContext _context;
    private readonly string _tableName;
    private readonly List<ColumnDefinition> _columns = new();
    private ColumnDefinition? _currentColumn;

    public CreateTableBuilder(MigrationContext context, string tableName)
    {
        _context = context;
        _tableName = tableName;
    }

    public ICreateTableColumnTypeSyntax WithColumn(string columnName)
    {
        // Complete previous column if exists
        if (_currentColumn != null)
        {
            _columns.Add(_currentColumn);
        }

        // Start new column
        _currentColumn = new ColumnDefinition { Name = columnName };
        return this;
    }

    // Type methods
    public ICreateTableColumnOptionsSyntax AsText()
    {
        _currentColumn!.Type = "TEXT";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsString(int? maxLength = null)
    {
        _currentColumn!.Type = maxLength.HasValue ? $"TEXT({maxLength.Value})" : "TEXT";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsInt32()
    {
        _currentColumn!.Type = "INTEGER";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsInteger() => AsInt32();

    public ICreateTableColumnOptionsSyntax AsInt64()
    {
        _currentColumn!.Type = "INTEGER";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsReal()
    {
        _currentColumn!.Type = "REAL";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsDouble() => AsReal();

    public ICreateTableColumnOptionsSyntax AsBlob()
    {
        _currentColumn!.Type = "BLOB";
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsBoolean()
    {
        _currentColumn!.Type = "INTEGER"; // SQLite uses INTEGER for boolean
        _currentColumn!.Constraints.Add("CHECK (" + _currentColumn.Name + " IN (0, 1))");
        return this;
    }

    public ICreateTableColumnOptionsSyntax AsDateTime()
    {
        _currentColumn!.Type = "TEXT"; // SQLite stores datetime as TEXT in ISO8601
        return this;
    }

    // Constraint methods
    public ICreateTableColumnOptionsSyntax NotNullable()
    {
        _currentColumn!.Constraints.Add("NOT NULL");
        return this;
    }

    public ICreateTableColumnOptionsSyntax Nullable()
    {
        // NULL is default in SQLite, no need to add anything
        return this;
    }

    public ICreateTableColumnOptionsSyntax PrimaryKey()
    {
        _currentColumn!.Constraints.Add("PRIMARY KEY");
        return this;
    }

    public ICreateTableColumnOptionsSyntax Unique()
    {
        _currentColumn!.Constraints.Add("UNIQUE");
        return this;
    }

    public ICreateTableColumnOptionsSyntax Identity()
    {
        _currentColumn!.Constraints.Add("AUTOINCREMENT");
        return this;
    }

    public ICreateTableColumnOptionsSyntax WithDefaultValue(object value)
    {
        string defaultValue = value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
        _currentColumn!.Constraints.Add($"DEFAULT {defaultValue}");
        return this;
    }

    public ICreateTableColumnOptionsSyntax WithDefaultCurrentTimestamp()
    {
        _currentColumn!.Constraints.Add("DEFAULT CURRENT_TIMESTAMP");
        return this;
    }

    public ICreateTableColumnOptionsSyntax ForeignKey(string referencedTable, string referencedColumn)
    {
        _currentColumn!.Constraints.Add($"REFERENCES {referencedTable}({referencedColumn})");
        return this;
    }

    public ICreateTableColumnOptionsSyntax Check(string checkExpression)
    {
        _currentColumn!.Constraints.Add($"CHECK ({checkExpression})");
        return this;
    }

    public ICreateTableColumnOptionsSyntax Collate(string collation)
    {
        _currentColumn!.Constraints.Add($"COLLATE {collation}");
        return this;
    }

    /// <summary>
    /// Complete the table creation and generate SQL
    /// Called when builder is disposed or implicitly completed
    /// </summary>
    internal void Complete()
    {
        // Add the last column
        if (_currentColumn != null)
        {
            _columns.Add(_currentColumn);
            _currentColumn = null;
        }

        // Generate CREATE TABLE SQL
        if (_columns.Count == 0)
        {
            return; // No columns defined
        }

        var sql = new StringBuilder();
        sql.AppendLine($"CREATE TABLE IF NOT EXISTS {_tableName} (");

        for (int i = 0; i < _columns.Count; i++)
        {
            var col = _columns[i];
            sql.Append($"    {col.Name} {col.Type}");

            foreach (var constraint in col.Constraints)
            {
                sql.Append($" {constraint}");
            }

            if (i < _columns.Count - 1)
            {
                sql.AppendLine(",");
            }
            else
            {
                sql.AppendLine();
            }
        }

        sql.Append(")");

        _context.AddStatement(sql.ToString());
    }

    private class ColumnDefinition
    {
        public required string Name { get; init; }
        public string Type { get; set; } = "TEXT";
        public List<string> Constraints { get; } = new();
    }
}
