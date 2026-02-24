using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for tag repository
/// Manages the master Tags table (authoritative source for tag definitions)
/// Note: Mods.Tags column stores which tags each mod uses (managed by ModRepository)
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Get all tags from the Tags table
    /// </summary>
    Task<List<Tag>> GetAllAsync();

    /// <summary>
    /// Get a specific tag by name
    /// </summary>
    Task<Tag?> GetByNameAsync(string name);

    /// <summary>
    /// Create or update a tag
    /// </summary>
    Task<bool> UpsertAsync(Tag tag);

    /// <summary>
    /// Delete a tag from the Tags table
    /// Note: This only removes the tag definition, not tag references in Mods.Tags
    /// Mods will keep their tags, but the tag won't appear in autocomplete/dialogs
    /// </summary>
    Task<bool> DeleteAsync(string name);

    /// <summary>
    /// Get all unique tag names that are actually used in mods (from Mods.Tags)
    /// This is different from GetAllAsync which returns tags from Tags table
    /// </summary>
    Task<List<string>> GetUsedTagNamesAsync();

    /// <summary>
    /// Get count of mods using a specific tag (searches Mods.Tags)
    /// </summary>
    Task<int> GetUsageCountAsync(string name);

    /// <summary>
    /// Search tags by name (case-insensitive substring match)
    /// </summary>
    Task<List<Tag>> SearchAsync(string searchTerm);
}
