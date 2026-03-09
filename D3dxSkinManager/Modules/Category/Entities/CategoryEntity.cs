namespace D3dxSkinManager.Modules.Category.Entities;

/// <summary>
/// Database entity for Category
/// Maps 1:1 with Categories table columns
/// Metadata is stored as JSON string in database
/// </summary>
public class CategoryEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? ThumbnailPath { get; set; }  // Matches DB column name
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? Metadata { get; set; }  // JSON string
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
