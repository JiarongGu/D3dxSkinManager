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
    /// JSON-serialized array of the mod's persisted d3dx_user.ini var lines captured with this preset
    /// (per-mod 3DMigoto $var state). Null when the preset didn't capture var state. Added 2026-07-13
    /// (migration 202607130001). See D3dmigotoUserConfigService.
    /// </summary>
    public string? ModState { get; set; }

    /// <summary>
    /// Timestamp when preset was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when preset was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
