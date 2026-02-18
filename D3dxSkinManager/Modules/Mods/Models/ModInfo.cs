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
    public bool IsLoaded { get; set; }
    public bool IsAvailable { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? PreviewPath { get; set; }

    // File paths for mod file operations
    public string? OriginalPath { get; set; }  // Path to original archive file
    public string? WorkPath { get; set; }      // Path to extracted/working directory
    public string? CachePath { get; set; }     // Path to cache directory (for disabled mods)
}
