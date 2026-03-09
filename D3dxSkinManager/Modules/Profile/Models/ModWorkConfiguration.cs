using System.Text.Json.Serialization;

namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Mod work directory configuration (parent of Mods folder) with cache cleanup settings
/// </summary>
public class ModWorkConfiguration
{
    /// <summary>
    /// Storage mode: "internal" or "external"
    /// internal: Uses {profile folder}\work
    /// external: Uses custom work directory path
    /// </summary>
    public string Mode { get; set; } = "internal";

    /// <summary>
    /// Custom work directory path (only used when Mode is "external")
    /// This should point to the work directory (parent of Mods folder)
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// Enable automatic cleanup of old disabled caches
    /// Default: true
    /// </summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of disabled caches to keep (default: 10)
    /// When exceeded, oldest caches (by LastWriteTime) are deleted automatically
    /// Valid range: 1-100
    /// </summary>
    public int CleanupMaxCaches { get; set; } = 10;

    /// <summary>
    /// Computed internal work directory path (computed by backend, not user-editable)
    /// This is included in IPC responses for display in UI but not saved to config.json
    /// Excluded from serialization when null (during save), included when has value (during read)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternalDirectory { get; set; }

    /// <summary>
    /// Check if the work directory mode is external (case-insensitive)
    /// </summary>
    /// <returns>True if external mode is enabled</returns>
    public bool IsExternal()
    {
        return "external".Equals(Mode, StringComparison.OrdinalIgnoreCase);
    }
}
