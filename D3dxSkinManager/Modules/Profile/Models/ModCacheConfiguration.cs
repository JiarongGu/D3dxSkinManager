namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Mod cache storage configuration
/// </summary>
public class ModCacheConfiguration
{
    /// <summary>
    /// Storage mode: "internal" or "external"
    /// internal: Uses {profile folder}\work\Mods
    /// external: Uses custom directory path
    /// </summary>
    public string Mode { get; set; } = "internal";

    /// <summary>
    /// Custom directory path (only used when Mode is "external")
    /// </summary>
    public string? Directory { get; set; }
}
