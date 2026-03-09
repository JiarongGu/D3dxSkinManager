using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Category.Entities;
using D3dxSkinManager.Modules.Category.Models;

namespace D3dxSkinManager.Modules.Category.Mappers;

/// <summary>
/// Mapper for converting between CategoryEntity (database) and CategoryInfo (domain)
/// </summary>
public static class CategoryMapper
{
    /// <summary>
    /// Convert CategoryEntity (database) to CategoryInfo (domain)
    /// Deserializes JSON Metadata to Dictionary
    /// </summary>
    public static CategoryInfo ToDomain(CategoryEntity entity)
    {
        var category = new CategoryInfo
        {
            Id = entity.Id,
            Name = entity.Name,
            ParentId = entity.ParentId,
            Thumbnail = entity.ThumbnailPath,  // Map ThumbnailPath -> Thumbnail
            Priority = entity.Priority,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Children = new List<CategoryInfo>()
        };

        // Deserialize Metadata JSON if present
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            category.Metadata = JsonHelper.Deserialize<Dictionary<string, object>>(entity.Metadata);
        }

        return category;
    }

    /// <summary>
    /// Convert CategoryInfo (domain) to CategoryEntity (database)
    /// Serializes Dictionary Metadata to JSON
    /// </summary>
    public static CategoryEntity ToEntity(CategoryInfo domain)
    {
        return new CategoryEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            ParentId = domain.ParentId,
            ThumbnailPath = domain.Thumbnail,  // Map Thumbnail -> ThumbnailPath
            Priority = domain.Priority,
            Description = domain.Description,
            Metadata = domain.Metadata != null ? JsonHelper.Serialize(domain.Metadata) : null,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }

    /// <summary>
    /// Convert list of CategoryEntity to list of CategoryInfo
    /// </summary>
    public static List<CategoryInfo> ToDomainList(IEnumerable<CategoryEntity> entities)
    {
        return entities.Select(ToDomain).ToList();
    }

    /// <summary>
    /// Update existing CategoryEntity from CategoryInfo
    /// </summary>
    public static void UpdateEntity(CategoryEntity entity, CategoryInfo domain)
    {
        entity.Name = domain.Name;
        entity.ParentId = domain.ParentId;
        entity.ThumbnailPath = domain.Thumbnail;
        entity.Priority = domain.Priority;
        entity.Description = domain.Description;
        entity.Metadata = domain.Metadata != null ? JsonHelper.Serialize(domain.Metadata) : null;
        entity.UpdatedAt = domain.UpdatedAt;
    }
}
