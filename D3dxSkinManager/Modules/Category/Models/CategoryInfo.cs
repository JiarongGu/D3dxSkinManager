namespace D3dxSkinManager.Modules.Category.Models;

/// <summary>
/// Represents a category in the Category tree (can be a folder or leaf)
/// Supports recursive N-layer hierarchy
/// </summary>
public class CategoryInfo
{
    /// <summary>
    /// Unique identifier for this category (GUID)
    /// e.g., "550e8400-e29b-41d4-a716-446655440000"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to thumbnail image (file:// URL)
    /// </summary>
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Priority for pattern matching (higher = checked first)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Match mode for auto-detection: "Wildcard" or "Regex"
    /// Wildcard uses simple pattern matching (e.g., "*Ayaka*")
    /// Regex uses regular expression matching
    /// </summary>
    public string? MatchMode { get; set; }

    /// <summary>
    /// Match pattern for auto-detection
    /// Used to automatically assign mods to this Category based on folder/file names
    /// </summary>
    public string? MatchPattern { get; set; }

    /// <summary>
    /// Description of this Category
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Child categories (subfolders)
    /// </summary>
    public List<CategoryInfo> Children { get; set; } = new();

    /// <summary>
    /// Whether this is a leaf category (no children)
    /// </summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Parent category ID (null for root categories)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Total number of mods in this category and all descendant categories
    /// Calculated recursively when tree is built
    /// </summary>
    public int ModCount { get; set; } = 0;

    /// <summary>
    /// When this Category was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this Category was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}