using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Mods.Models;

/// <summary>
/// Represents a node in the classification tree (can be a folder or leaf)
/// Supports recursive N-layer hierarchy
/// </summary>
public class ClassificationNode
{
    /// <summary>
    /// Unique identifier for this node (folder path relative to classifications root)
    /// e.g., "终末地" or "终末地/干员-物理"
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
    /// Description of this classification
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Child nodes (subfolders)
    /// </summary>
    public List<ClassificationNode> Children { get; set; } = new();

    /// <summary>
    /// Whether this is a leaf node (no children)
    /// </summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Parent node ID (null for root nodes)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Configuration file structure for classification folders
/// Maps to _config.json files
/// </summary>
public class ClassificationConfig
{
    public string Name { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
    public int Priority { get; set; } = 0;
    public string? Description { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
