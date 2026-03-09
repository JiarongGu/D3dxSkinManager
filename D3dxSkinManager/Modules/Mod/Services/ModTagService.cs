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
    Task<Tag?> GetTagByNameAsync(string name);
    Task<bool> UpsertTagAsync(string name, string color);
    Task<bool> DeleteTagAsync(string name);
    Task<List<string>> GetUsedTagNamesAsync();
    Task<int> GetTagUsageCountAsync(string tag);
    Task<List<Tag>> SearchTagsAsync(string searchTerm);
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

    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        return await _tagRepository.GetByNameAsync(name);
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

    public async Task<int> GetTagUsageCountAsync(string tag)
    {
        var entities = await _modRepository.GetAllAsync();
        var mods = ModMapper.ToDomainList(entities);
        return mods.Count(m => m.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<List<Tag>> SearchTagsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await _tagRepository.GetAllAsync();
        }

        var allTags = await _tagRepository.GetAllAsync();
        return allTags
            .Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
