namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Tag model - Master list of all available tags with styling information
/// This is the authoritative source for tag definitions
/// Mods reference tags by name in their Tags JSON array
/// </summary>
public class Tag
{
    /// <summary>
    /// Tag name (primary key, unique identifier)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tag color in hex format (e.g., "#1890ff")
    /// Randomly generated on creation, user-customizable
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// When this tag was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this tag was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
