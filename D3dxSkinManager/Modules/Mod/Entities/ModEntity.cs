namespace D3dxSkinManager.Modules.Mod.Entities;

/// <summary>
/// Database entity for Mods table
/// Maps 1:1 to database columns - no computed properties
/// Property names match database column names exactly for Dapper
/// Use ModMapper to convert between ModEntity and ModInfo (domain model)
/// </summary>
public class ModEntity
{
    /// <summary>
    /// Primary key - SHA hash of mod archive
    /// </summary>
    public string SHA { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Categories table (ID or legacy path)
    /// Property name matches database column "Category"
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Mod display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Mod author name (nullable in database)
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Mod description (nullable in database)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Mod archive type (default: "7z")
    /// </summary>
    public string Type { get; set; } = "7z";

    /// <summary>
    /// Mod content rating (default: "G")
    /// </summary>
    public string Grading { get; set; } = "G";

    /// <summary>
    /// JSON-serialized array of tag names (nullable in database)
    /// Stored as: ["tag1", "tag2", "tag3"]
    /// Property name matches database column "Tags"
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Whether preview images should be disabled for this mod
    /// Stored as 0 (false) or 1 (true) in SQLite
    /// </summary>
    public bool DisablePreview { get; set; } = false;

    /// <summary>
    /// Timestamp when mod was first added
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when mod metadata was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Extension field for future use - stores JSON data without database migration
    /// </summary>
    public string? Metadata { get; set; }

    // Note: No computed properties like IsLoaded, CategoryName, file paths
    // Those belong in ModInfo (domain model) and are populated by ModService
}
