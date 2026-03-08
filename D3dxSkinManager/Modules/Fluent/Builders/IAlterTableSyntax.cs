namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Syntax for altering tables
/// </summary>
public interface IAlterTableSyntax
{
    /// <summary>
    /// Add a column to the table
    /// </summary>
    IAlterTableColumnTypeSyntax AddColumn(string columnName);

    /// <summary>
    /// Rename the table
    /// </summary>
    void RenameTo(string newTableName);
}

/// <summary>
/// Syntax for ALTER TABLE ADD COLUMN type
/// </summary>
public interface IAlterTableColumnTypeSyntax
{
    /// <summary>
    /// Define column as TEXT type
    /// </summary>
    IAlterTableColumnOptionsSyntax AsText();

    /// <summary>
    /// Define column as TEXT with max length
    /// </summary>
    IAlterTableColumnOptionsSyntax AsString(int? maxLength = null);

    /// <summary>
    /// Define column as INTEGER type
    /// </summary>
    IAlterTableColumnOptionsSyntax AsInt32();

    /// <summary>
    /// Define column as INTEGER (alias for AsInt32)
    /// </summary>
    IAlterTableColumnOptionsSyntax AsInteger();

    /// <summary>
    /// Define column as BIGINT/INTEGER type
    /// </summary>
    IAlterTableColumnOptionsSyntax AsInt64();

    /// <summary>
    /// Define column as REAL type
    /// </summary>
    IAlterTableColumnOptionsSyntax AsReal();

    /// <summary>
    /// Define column as REAL (alias for AsReal)
    /// </summary>
    IAlterTableColumnOptionsSyntax AsDouble();

    /// <summary>
    /// Define column as BLOB type
    /// </summary>
    IAlterTableColumnOptionsSyntax AsBlob();

    /// <summary>
    /// Define column as BOOLEAN
    /// </summary>
    IAlterTableColumnOptionsSyntax AsBoolean();

    /// <summary>
    /// Define column as DATETIME
    /// </summary>
    IAlterTableColumnOptionsSyntax AsDateTime();
}

/// <summary>
/// Syntax for ALTER TABLE ADD COLUMN options
/// </summary>
public interface IAlterTableColumnOptionsSyntax
{
    /// <summary>
    /// Make column NOT NULL (must have default for existing rows)
    /// </summary>
    IAlterTableColumnOptionsSyntax NotNullable();

    /// <summary>
    /// Make column nullable (default)
    /// </summary>
    IAlterTableColumnOptionsSyntax Nullable();

    /// <summary>
    /// Set default value for column
    /// </summary>
    IAlterTableColumnOptionsSyntax WithDefaultValue(object value);

    /// <summary>
    /// Set default to CURRENT_TIMESTAMP
    /// </summary>
    IAlterTableColumnOptionsSyntax WithDefaultCurrentTimestamp();

    /// <summary>
    /// Add CHECK constraint
    /// </summary>
    IAlterTableColumnOptionsSyntax Check(string checkExpression);
}
