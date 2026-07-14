using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;

namespace D3dxSkinManager.Modules.Mod.Mappers;

/// <summary>
/// Mapper for converting between TagEntity (database) and Tag (domain)
/// </summary>
public static class TagMapper
{
    /// <summary>
    /// Convert TagEntity (database) to Tag (domain)
    /// </summary>
    public static Tag ToDomain(TagEntity entity)
    {
        return new Tag
        {
            Name = entity.Name,
            Color = entity.Color,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// Convert Tag (domain) to TagEntity (database)
    /// </summary>
    public static TagEntity ToEntity(Tag domain)
    {
        return new TagEntity
        {
            Name = domain.Name,
            Color = domain.Color,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }

    /// <summary>
    /// Convert list of TagEntity to list of Tag
    /// </summary>
    public static List<Tag> ToDomainList(IEnumerable<TagEntity> entities)
    {
        return entities.Select(ToDomain).ToList();
    }
}
