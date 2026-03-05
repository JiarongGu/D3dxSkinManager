using System.Text.Json.Serialization;

namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Work directory configuration (parent of Mods folder)
/// </summary>
public class WorkDirectoryConfiguration
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
    /// Computed internal work directory path (computed by backend, not user-editable)
    /// This is included in IPC responses for display in UI
    /// Frontend should not send this when updating config
    /// </summary>
    public string? InternalWorkDirectory { get; set; }

    /// <summary>
    /// Check if the work directory mode is external (case-insensitive)
    /// </summary>
    /// <returns>True if external mode is enabled</returns>
    public bool IsExternal()
    {
        return "external".Equals(Mode, StringComparison.OrdinalIgnoreCase);
    }
}
