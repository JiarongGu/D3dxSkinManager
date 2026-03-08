using System.Text;

namespace D3dxSkinManager.Modules.Fluent.Builders;

/// <summary>
/// Context for building migration SQL statements
/// Collects all SQL operations as they are defined
/// </summary>
public class MigrationContext
{
    private readonly List<string> _sqlStatements = new();
    private readonly List<object> _activeBuilders = new();

    /// <summary>
    /// Register a builder that needs finalization
    /// </summary>
    internal void RegisterBuilder(object builder)
    {
        _activeBuilders.Add(builder);
    }

    /// <summary>
    /// Complete all active builders
    /// </summary>
    internal void CompleteBuilders()
    {
        foreach (var builder in _activeBuilders)
        {
            switch (builder)
            {
                case CreateTableBuilder ctb:
                    ctb.Complete();
                    break;
                case CreateIndexBuilder cib:
                    cib.Complete();
                    break;
                case AlterTableBuilder atb:
                    atb.Complete();
                    break;
            }
        }
        _activeBuilders.Clear();
    }

    /// <summary>
    /// Add a SQL statement to execute
    /// </summary>
    public void AddStatement(string sql)
    {
        if (!string.IsNullOrWhiteSpace(sql))
        {
            _sqlStatements.Add(sql);
        }
    }

    /// <summary>
    /// Get all SQL statements as a single script
    /// </summary>
    public string GetSqlScript()
    {
        return string.Join(";\n", _sqlStatements) + ";";
    }

    /// <summary>
    /// Get all SQL statements as a list
    /// </summary>
    public IReadOnlyList<string> GetStatements() => _sqlStatements.AsReadOnly();

    /// <summary>
    /// Clear all collected statements
    /// </summary>
    public void Clear()
    {
        _sqlStatements.Clear();
    }
}
