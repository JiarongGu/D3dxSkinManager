namespace D3dxSkinManager.Modules.Mod.Entities;

/// <summary>
/// Database entity for ModPresets table
/// Maps 1:1 to database columns for Dapper
/// </summary>
public class ModPresetEntity
{
    /// <summary>
    /// Primary key - GUID identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the preset
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized array of mod IDs that should be active
    /// Stored as: ["MODID1", "MODID2", ...]
    /// </summary>
    public string ModIds { get; set; } = "[]";

    /// <summary>
    /// Timestamp when preset was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when preset was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
