using D3dxSkinManager.Modules.Fluent.Builders;

namespace D3dxSkinManager.Modules.Fluent;

/// <summary>
/// Base class for all database migrations
/// Provides fluent API for creating and modifying database schema
/// </summary>
public abstract class Migration
{
    /// <summary>
    /// Fluent API entry point for creating database objects
    /// </summary>
    protected ICreateExpressionRoot Create { get; private set; } = null!;

    /// <summary>
    /// Fluent API entry point for deleting database objects
    /// </summary>
    protected IDeleteExpressionRoot Delete { get; private set; } = null!;

    /// <summary>
    /// Fluent API entry point for altering database objects
    /// </summary>
    protected IAlterExpressionRoot Alter { get; private set; } = null!;

    /// <summary>
    /// Execute raw SQL (use sparingly, prefer fluent API)
    /// </summary>
    protected IExecuteExpressionRoot Execute { get; private set; } = null!;

    /// <summary>
    /// Internal method to set the expression roots (called by migration runner)
    /// </summary>
    internal void SetExpressionRoots(
        ICreateExpressionRoot create,
        IDeleteExpressionRoot delete,
        IAlterExpressionRoot alter,
        IExecuteExpressionRoot execute)
    {
        Create = create;
        Delete = delete;
        Alter = alter;
        Execute = execute;
    }

    /// <summary>
    /// Define the migration operations to apply
    /// </summary>
    public abstract void Up();

    /// <summary>
    /// Define the migration operations to rollback (optional)
    /// </summary>
    public virtual void Down()
    {
        // Default: no rollback
    }
}
