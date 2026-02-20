namespace D3dxSkinManager.Modules.Mods.Models;

/// <summary>
/// Result of a mod load operation with affected mods for efficient frontend updates
/// Avoids full mod list refresh by returning only what changed
/// </summary>
public class ModLoadResult
{
    /// <summary>
    /// SHA of the mod that was loaded
    /// </summary>
    public required string LoadedModSha { get; set; }

    /// <summary>
    /// SHAs of mods that were automatically unloaded (same category conflicts)
    /// Frontend can update these specific mods instead of refreshing entire list
    /// </summary>
    public required List<string> UnloadedModShas { get; set; }

    /// <summary>
    /// Whether the load operation succeeded
    /// </summary>
    public required bool Success { get; set; }
}
