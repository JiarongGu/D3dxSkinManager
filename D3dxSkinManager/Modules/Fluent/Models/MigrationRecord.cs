namespace D3dxSkinManager.Modules.Fluent.Models;

/// <summary>
/// Represents a migration history record in the database
/// </summary>
public class MigrationRecord
{
    /// <summary>
    /// Migration version (e.g., 202603081735)
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Description of the migration
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When this migration was applied
    /// </summary>
    public required DateTime AppliedAt { get; init; }

    /// <summary>
    /// Name of the migration class
    /// </summary>
    public string? MigrationName { get; init; }
}
