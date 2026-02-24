using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for tag repository
/// Manages tag operations within the mods module
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Get all unique tags from all mods
    /// </summary>
    Task<List<string>> GetAllTagsAsync();

    /// <summary>
    /// Search tags by search term (case-insensitive substring match)
    /// </summary>
    Task<List<string>> SearchTagsAsync(string searchTerm);

    /// <summary>
    /// Add a tag to a specific mod
    /// </summary>
    Task<bool> AddTagToModAsync(string sha, string tag);

    /// <summary>
    /// Remove a tag from a specific mod
    /// </summary>
    Task<bool> RemoveTagFromModAsync(string sha, string tag);

    /// <summary>
    /// Rename a tag globally across all mods
    /// </summary>
    Task<int> RenameTagGloballyAsync(string oldTag, string newTag);

    /// <summary>
    /// Delete a tag globally from all mods
    /// </summary>
    Task<int> DeleteTagGloballyAsync(string tag);

    /// <summary>
    /// Get tags for a specific mod
    /// </summary>
    Task<List<string>> GetTagsForModAsync(string sha);

    /// <summary>
    /// Get count of mods using a specific tag
    /// </summary>
    Task<int> GetTagUsageCountAsync(string tag);
}
