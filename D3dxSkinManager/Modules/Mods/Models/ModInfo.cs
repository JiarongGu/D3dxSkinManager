namespace D3dxSkinManager.Modules.Mods.Models;

/// <summary>
/// Mod information model
/// </summary>
public class ModInfo
{
    public string SHA { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;  // Classification ID (may be GUID or legacy path)
    public string CategoryName { get; set; } = string.Empty;  // Human-readable category name for display
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    public List<Tag> TagsWithMetadata { get; set; } = new(); // Tag objects with color information (populated on-demand)

    // Status flags (populated on-demand from file system, not stored in DB)
    public bool IsLoaded { get; set; }      // True if work directory exists without DISABLED- prefix
    public bool IsAvailable { get; set; }   // True if original archive file exists in mods folder

    // Note: Preview paths and thumbnails are scanned dynamically from previews/{SHA}/ folder
    // Allows users to add preview images directly to folder
    // Use GET_PREVIEW_PATHS IPC call to retrieve them
    // The first preview image (sorted alphabetically) is used as the thumbnail
}
