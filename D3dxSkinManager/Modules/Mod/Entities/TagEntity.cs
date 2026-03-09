namespace D3dxSkinManager.Modules.Mod.Entities;

/// <summary>
/// Database entity for Tag
/// Maps 1:1 with Tags table columns
/// </summary>
public class TagEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";  // Default gray
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
