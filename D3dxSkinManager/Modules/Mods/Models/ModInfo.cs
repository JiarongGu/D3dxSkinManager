using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Mods.Models;

/// <summary>
/// Mod information model
/// </summary>
public class ModInfo
{
    public string SHA { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();

    // Status flags (populated on-demand from file system, not stored in DB)
    public bool IsLoaded { get; set; }      // True if work directory exists without DISABLED- prefix
    public bool IsAvailable { get; set; }   // True if original archive file exists in mods folder

    // File paths for mod file operations (populated on-demand, not stored in DB)
    public string? OriginalPath { get; set; }  // Path to original archive file
    public string? WorkPath { get; set; }      // Path to extracted/working directory
    public string? CachePath { get; set; }     // Path to cache directory (for disabled mods)

    // Note: Preview paths and thumbnails are scanned dynamically from previews/{SHA}/ folder
    // Allows users to add preview images directly to folder
    // Use GET_PREVIEW_PATHS IPC call to retrieve them
    // The first preview image (sorted alphabetically) is used as the thumbnail
}
