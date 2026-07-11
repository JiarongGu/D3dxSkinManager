using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for tag management operations
/// Handles all tag-related business logic
/// </summary>
public interface IModTagService
{
    Task<List<Tag>> GetAllTagsAsync();
    Task<bool> UpsertTagAsync(string name, string color);
    Task<bool> DeleteTagAsync(string name);
    Task<List<string>> GetUsedTagNamesAsync();
}

public class ModTagService : IModTagService
{
    private readonly ITagRepository _tagRepository;
    private readonly IModRepository _modRepository;

    public ModTagService(ITagRepository tagRepository, IModRepository modRepository)
    {
        _tagRepository = tagRepository;
        _modRepository = modRepository;
    }

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        return await _tagRepository.GetAllAsync();
    }

    public async Task<bool> UpsertTagAsync(string name, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Color cannot be empty", nameof(color));

        var tag = new Tag
        {
            Name = name,
            Color = color,
            UpdatedAt = DateTime.UtcNow
        };

        return await _tagRepository.UpsertAsync(tag);
    }

    public async Task<bool> DeleteTagAsync(string name)
    {
        return await _tagRepository.DeleteAsync(name);
    }

    public async Task<List<string>> GetUsedTagNamesAsync()
    {
        return await _tagRepository.GetUsedTagNamesAsync();
    }
}
