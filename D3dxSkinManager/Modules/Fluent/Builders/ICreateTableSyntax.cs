namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Syntax for creating tables with columns
/// </summary>
public interface ICreateTableWithColumnSyntax
{
    /// <summary>
    /// Add a column to the table being created
    /// </summary>
    ICreateTableColumnTypeSyntax WithColumn(string columnName);
}

/// <summary>
/// Syntax for specifying column type
/// </summary>
public interface ICreateTableColumnTypeSyntax
{
    /// <summary>
    /// Define column as TEXT type
    /// </summary>
    ICreateTableColumnOptionsSyntax AsText();

    /// <summary>
    /// Define column as TEXT with max length
    /// </summary>
    ICreateTableColumnOptionsSyntax AsString(int? maxLength = null);

    /// <summary>
    /// Define column as INTEGER type
    /// </summary>
    ICreateTableColumnOptionsSyntax AsInt32();

    /// <summary>
    /// Define column as INTEGER (alias for AsInt32)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsInteger();

    /// <summary>
    /// Define column as BIGINT/INTEGER type (SQLite uses INTEGER for all int types)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsInt64();

    /// <summary>
    /// Define column as REAL type (floating point)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsReal();

    /// <summary>
    /// Define column as REAL (alias for AsReal)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsDouble();

    /// <summary>
    /// Define column as BLOB type
    /// </summary>
    ICreateTableColumnOptionsSyntax AsBlob();

    /// <summary>
    /// Define column as BOOLEAN (stored as INTEGER 0/1 in SQLite)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsBoolean();

    /// <summary>
    /// Define column as DATETIME (stored as TEXT in ISO8601 format)
    /// </summary>
    ICreateTableColumnOptionsSyntax AsDateTime();
}

/// <summary>
/// Syntax for column options (constraints, defaults, etc.)
/// </summary>
public interface ICreateTableColumnOptionsSyntax : ICreateTableWithColumnSyntax
{
    /// <summary>
    /// Make column NOT NULL
    /// </summary>
    ICreateTableColumnOptionsSyntax NotNullable();

    /// <summary>
    /// Make column nullable (default)
    /// </summary>
    ICreateTableColumnOptionsSyntax Nullable();

    /// <summary>
    /// Make column PRIMARY KEY
    /// </summary>
    ICreateTableColumnOptionsSyntax PrimaryKey();

    /// <summary>
    /// Make column UNIQUE
    /// </summary>
    ICreateTableColumnOptionsSyntax Unique();

    /// <summary>
    /// Add AUTOINCREMENT (only valid for INTEGER PRIMARY KEY)
    /// </summary>
    ICreateTableColumnOptionsSyntax Identity();

    /// <summary>
    /// Set default value for column
    /// </summary>
    ICreateTableColumnOptionsSyntax WithDefaultValue(object value);

    /// <summary>
    /// Set default to CURRENT_TIMESTAMP
    /// </summary>
    ICreateTableColumnOptionsSyntax WithDefaultCurrentTimestamp();

    /// <summary>
    /// Add foreign key constraint
    /// </summary>
    ICreateTableColumnOptionsSyntax ForeignKey(string referencedTable, string referencedColumn);

    /// <summary>
    /// Add CHECK constraint
    /// </summary>
    ICreateTableColumnOptionsSyntax Check(string checkExpression);

    /// <summary>
    /// Add COLLATE clause
    /// </summary>
    ICreateTableColumnOptionsSyntax Collate(string collation);
}
