namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Mod information model
/// </summary>
public class ModInfo
{
    public string SHA { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;  // Category ID (may be GUID or legacy path)
    public string CategoryName { get; set; } = string.Empty;  // Human-readable category name for display
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    public List<Tag> TagsWithMetadata { get; set; } = new(); // Tag objects with color information (populated on-demand)

    // Preview settings
    public bool DisablePreview { get; set; } = false; // If true, preview images won't be loaded/displayed for this mod

    // Status flags (populated on-demand from file system, not stored in DB)
    public bool IsLoaded { get; set; }      // True if work directory exists without DISABLED- prefix
    public bool IsAvailable { get; set; }   // True if original archive file exists in mods folder
    public bool HasCache { get; set; }      // True if cache directory exists (either active or with DISABLED- prefix)
    public bool HasPreviewFolder { get; set; } // True if preview directory exists with preview images
    public bool IsOrphaned { get; set; }    // True if mod exists in cache but not in database (allows cleanup)

    // File paths (populated on-demand from file system, not stored in DB)
    public string? CachePath { get; set; }  // Absolute path to cache directory (if exists)
    public string? PreviewFolderPath { get; set; }  // Absolute path to preview directory (if exists)
    public string? ArchiveFolderPath { get; set; }  // Absolute path to mods directory containing the archive file

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Extension field for future use - can store JSON data without database migration
    public string? Metadata { get; set; }

    // Note: Preview paths and thumbnails are scanned dynamically from previews/{SHA}/ folder
    // Allows users to add preview images directly to folder
    // Use GET_PREVIEW_PATHS IPC call to retrieve them
    // The first preview image (sorted alphabetically) is used as the thumbnail
}
