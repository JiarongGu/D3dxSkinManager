namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Represents a specific error that occurred during migration
/// </summary>
public class MigrationError
{
    /// <summary>
    /// Error message (raw exception message or description)
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Message code for i18n lookup (e.g., "MOD_MIGRATION_FAILED")
    /// Frontend will map this to i18n key: "migration.error.code.modMigrationFailed"
    /// </summary>
    public string? MessageCode { get; set; }

    /// <summary>
    /// Name of the mod that failed (if applicable)
    /// </summary>
    public string? ModName { get; set; }

    /// <summary>
    /// Id of the mod (for preview errors)
    /// </summary>
    public string? ModId { get; set; }

    /// <summary>
    /// Step code where the error occurred (e.g., "MIGRATE_MOD_ARCHIVES")
    /// Frontend will map this to i18n key: "migration.error.step.migrateModArchives"
    /// </summary>
    public string? StepCode { get; set; }

    /// <summary>
    /// Category code (e.g., "MOD_MIGRATION", "PREVIEW_MIGRATION")
    /// Frontend will map this to i18n key: "migration.error.category.modMigration"
    /// </summary>
    public string? CategoryCode { get; set; }

    /// <summary>
    /// Timestamp when error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional parameters for i18n interpolation
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}
