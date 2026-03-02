using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Modules.Category.Services;

/// <summary>
/// Interface for Category service
/// </summary>
public interface ICategoryService
{
    Task<List<CategoryInfo>> GetCategoryTreeAsync();

    Task<bool> UpdateParentAsync(string categoryId, string? newParentId, int? dropPosition = null);

    Task<bool> UpdateCategoryAsync(string categoryId, string name, string? description = null, string? thumbnailPath = null, string? matchMode = null, string? matchPattern = null);

    Task<bool> UpdateThumbnailAsync(string categoryId, string thumbnailPath);

    Task<CategoryInfo?> GetByNameAsync(string name);

    Task<bool> DeleteAsync(string categoryId);

    Task<bool> ExistsAsync(string categoryId);

    Task<CategoryInfo?> CreateAsync(string categoryId, string name, string? parentId = null, int priority = 100, string? description = null, string? thumbnailPath = null, string? matchMode = null, string? matchPattern = null);

    void InvalidateTreeCache();
}

/// <summary>
/// Service for managing Category tree
/// Reads from SQLite database populated by migration
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IModRepository _modRepository;
    private readonly IPathHelper _pathHelper;
    private readonly IHashHelper _hashHelper;
    private readonly IImageHelper _imageHelper;
    private readonly IFileTransferService _fileTransferService;
    private readonly IProfilePathService _profilePaths;
    private readonly IMemoryCache _cache;
    private readonly IProfileEventBus _eventBus;
    private readonly string _cacheKey;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public CategoryService(
        ICategoryRepository repository,
        IModRepository modRepository,
        IPathHelper pathHelper,
        IHashHelper hashHelper,
        IImageHelper imageHelper,
        IFileTransferService fileTransferService,
        IProfilePathService profilePaths,
        IMemoryCache cache,
        IProfileEventBus eventBus,
        IProfileContext profileContext)
    {
        _repository = repository;
        _fileTransferService = fileTransferService;
        _profilePaths = profilePaths;
        _modRepository = modRepository;
        _pathHelper = pathHelper;
        _hashHelper = hashHelper;
        _imageHelper = imageHelper;
        _cache = cache;
        _eventBus = eventBus;

        // Use profile-specific cache key since IMemoryCache is shared across all profiles
        _cacheKey = $"CategoryTree_{profileContext.ProfileId}";
    }

    /// <summary>
    /// Get the full Category tree with all children populated
    /// Uses MemoryCache with automatic expiration
    /// </summary>
    public async Task<List<CategoryInfo>> GetCategoryTreeAsync()
    {
        // Try to get from cache, or create if not exists
        return await _cache.GetOrCreateAsync(_cacheKey, async entry =>
        {
            entry.SlidingExpiration = CacheExpiry;
            return await BuildTreeAsync().ConfigureAwait(false);
        }).ConfigureAwait(false) ?? new List<CategoryInfo>();
    }

    /// <summary>
    /// Invalidate the cache and emit CATEGORY_TREE_UPDATED event
    /// Called when mod categories change or category structure changes to recalculate counts
    /// Next GetCategoryTreeAsync call will rebuild from database
    /// </summary>
    public void InvalidateTreeCache()
    {
        _cache.Remove(_cacheKey);

        // Emit event to notify frontend that tree needs refresh
        // Use fire-and-forget since this is a synchronous method and we don't want to block
        _ = Task.Run(async () =>
        {
            await _eventBus.EmitAsync(ModuleNames.CATEGORY, CategoryEvents.CATEGORY_TREE_UPDATED).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Build the tree from database
    /// </summary>
    private async Task<List<CategoryInfo>> BuildTreeAsync()
    {
        // Get all categories from database
        var allCategories = await _repository.GetAllAsync().ConfigureAwait(false);

        // Build dictionary for quick lookup
        var categoryDict = allCategories.ToDictionary(c => c.Id);

        // Build tree structure by connecting parents to children
        var rootCategories = new List<CategoryInfo>();

        foreach (var category in allCategories)
        {
            if (string.IsNullOrEmpty(category.ParentId))
            {
                // Root category
                rootCategories.Add(category);
            }
            else if (categoryDict.TryGetValue(category.ParentId, out var parent))
            {
                // Add to parent's children
                parent.Children.Add(category);
            }
            else
            {
                // Parent doesn't exist - treat as root category (orphaned Category)
                rootCategories.Add(category);
            }
        }

        // Calculate mod counts for all categories
        await CalculateModCountsAsync(rootCategories).ConfigureAwait(false);

        return rootCategories;
    }

    /// <summary>
    /// Move a category to a new parent (or root level if newParentId is null)
    /// </summary>
    public async Task<bool> UpdateParentAsync(string categoryId, string? newParentId, int? dropPosition = null)
    {
        try
        {
            // Move the category to new parent
            var moved = await _repository.MoveCategoryAsync(categoryId, newParentId).ConfigureAwait(false);
            if (!moved) return false;

            // If dropPosition is specified, reorder siblings
            if (dropPosition.HasValue)
            {
                var siblings = await _repository.GetChildrenAsync(newParentId).ConfigureAwait(false);
                var updates = new List<(string categoryId, int priority)>();

                // Calculate new priorities based on drop position
                int priority = siblings.Count * 100;
                for (int i = 0; i < siblings.Count; i++)
                {
                    if (i == dropPosition.Value)
                    {
                        // Insert the moved category here
                        updates.Add((categoryId, priority));
                        priority -= 100;
                    }

                    if (siblings[i].Id != categoryId)
                    {
                        updates.Add((siblings[i].Id, priority));
                        priority -= 100;
                    }
                }

                // If dropPosition is at the end
                if (dropPosition.Value >= siblings.Count)
                {
                    updates.Add((categoryId, priority));
                }

                await _repository.ReorderSiblingsAsync(updates).ConfigureAwait(false);
            }

            // Invalidate cache
            InvalidateTreeCache();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update a Category's name
    /// Uses stable IDs - only the display name changes, ID remains the same
    /// </summary>
    public async Task<bool> UpdateCategoryAsync(string categoryId, string name, string? description = null, string? thumbnailPath = null, string? matchMode = null, string? matchPattern = null)
    {
        try
        {
            var category = await _repository.GetByIdAsync(categoryId).ConfigureAwait(false);
            if (category == null) return false;

            // Check if name would conflict globally (ensure uniqueness across entire database)
            if (category.Name != name)
            {
                var allCategories = await _repository.GetAllAsync().ConfigureAwait(false);
                var otherCategories = allCategories.Where(c => c.Id != categoryId).ToList();

                // Check for name uniqueness globally
                if (otherCategories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    // Another category with this name already exists
                    Console.WriteLine($"Category with name '{name}' already exists");
                    return false;
                }
            }

            // Handle thumbnail change if needed
            if (thumbnailPath != category.Thumbnail)
            {
                // Convert and copy new thumbnail to data folder if provided
                if (thumbnailPath != null)
                {
                    // Convert thumbnail to PNG format for compatibility
                    var thumbnailsDir = _profilePaths.ThumbnailsDirectory;
                    var hash = await _hashHelper.CalculateFileSHA256Async(thumbnailPath).ConfigureAwait(false);
                    var convertedPath = await _imageHelper.ConvertToPngAsync(thumbnailPath, thumbnailsDir, hash).ConfigureAwait(false);

                    if (convertedPath != null)
                    {
                        // Convert to relative path for portability
                        var relativePath = _pathHelper.ToRelativePath(convertedPath) ?? convertedPath;
                        category.Thumbnail = relativePath;
                    }
                    else
                    {
                        category.Thumbnail = null;
                    }
                }
                else
                {
                    category.Thumbnail = null;
                }
                // Note: Old thumbnails are not deleted here to avoid file lock issues
                // A separate cleanup tool can be used to remove orphaned thumbnails later
            }

            // Update fields - ID remains stable
            category.Name = name;
            category.Description = description;
            if (matchMode != null) category.MatchMode = matchMode;
            if (matchPattern != null) category.MatchPattern = matchPattern;

            var updated = await _repository.UpdateAsync(category).ConfigureAwait(false);
            if (updated)
            {
                InvalidateTreeCache();
            }

            return updated;
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Error updating Category: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete a Category and all its children
    /// </summary>
    public async Task<bool> DeleteAsync(string categoryId)
    {
        try
        {
            // Check if category exists
            var category = await _repository.GetByIdAsync(categoryId).ConfigureAwait(false);
            if (category == null) return false;

            // Delete all children recursively
            await DeleteCategoryAndChildrenRecursiveAsync(categoryId).ConfigureAwait(false);

            // Invalidate cache
            InvalidateTreeCache();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create a new Category with provided or auto-generated GUID
    /// Returns null if name already exists globally
    /// </summary>
    /// <param name="categoryId">The category ID (GUID). If empty/whitespace, a new GUID will be generated automatically.</param>
    /// <param name="name">The category name (must be globally unique)</param>
    /// <param name="parentId">Optional parent category ID</param>
    /// <param name="priority">Priority for sorting (default 100)</param>
    /// <param name="description">Optional description</param>
    /// <param name="thumbnailPath">Optional thumbnail path</param>
    /// <param name="matchMode">Optional match mode for auto-detection</param>
    /// <param name="matchPattern">Optional match pattern for auto-detection</param>
    /// <returns>The created CategoryInfo or null if name already exists</returns>
    public async Task<CategoryInfo?> CreateAsync(
        string categoryId,
        string name,
        string? parentId = null,
        int priority = 100,
        string? description = null,
        string? thumbnailPath = null,
        string? matchMode = null,
        string? matchPattern = null)
    {
        try
        {
            // Use provided categoryId if specified, otherwise generate a new GUID
            var generatedId = string.IsNullOrWhiteSpace(categoryId) ? Guid.NewGuid().ToString() : categoryId.Trim();

            // Check if name already exists globally (Category names must be unique across entire database)
            var allCategories = await _repository.GetAllAsync().ConfigureAwait(false);

            if (allCategories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"[CategoryService] Category with name '{name}' already exists");
                return null; // Name conflict - must be globally unique
            }

            // Check if the generated ID already exists (extremely unlikely with GUIDs)
            if (await _repository.ExistsAsync(generatedId))
            {
                // Try again with a new GUID (this should almost never happen)
                generatedId = Guid.NewGuid().ToString();
            }

            // Convert and copy thumbnail to data folder if provided
            string? relativeThumbnailPath = null;
            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                // Convert thumbnail to PNG format for compatibility
                var thumbnailsDir = _profilePaths.ThumbnailsDirectory;
                var hash = await _hashHelper.CalculateFileSHA256Async(thumbnailPath).ConfigureAwait(false);
                var convertedPath = await _imageHelper.ConvertToPngAsync(thumbnailPath, thumbnailsDir, hash).ConfigureAwait(false);

                if (convertedPath != null)
                {
                    // Convert to relative path for portability
                    relativeThumbnailPath = _pathHelper.ToRelativePath(convertedPath) ?? convertedPath;
                }
                else
                {
                    // If conversion failed, store original path (will fail gracefully on display)
                    Console.WriteLine($"[CategoryService] Failed to convert thumbnail, storing original path");
                    relativeThumbnailPath = thumbnailPath;
                }
            }

            var category = new CategoryInfo
            {
                Id = generatedId, // Use the generated GUID
                Name = name,
                ParentId = parentId,
                Thumbnail = relativeThumbnailPath,
                Priority = priority,
                Description = description ?? $"Category: {name}",
                MatchMode = matchMode,
                MatchPattern = matchPattern,
                Children = new List<CategoryInfo>()
            };

            await _repository.InsertAsync(category).ConfigureAwait(false);
            InvalidateTreeCache();
            return category;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Set thumbnail for a Category
    /// </summary>
    public async Task<bool> UpdateThumbnailAsync(string categoryId, string thumbnailPath)
    {
        try
        {
            var category = await _repository.GetByIdAsync(categoryId).ConfigureAwait(false);
            if (category == null)
                return false;

            // Convert thumbnail to PNG format for compatibility
            var thumbnailsDir = _profilePaths.ThumbnailsDirectory;
            var hash = await _hashHelper.CalculateFileSHA256Async(thumbnailPath).ConfigureAwait(false);
            var convertedPath = await _imageHelper.ConvertToPngAsync(thumbnailPath, thumbnailsDir, hash).ConfigureAwait(false);

            if (convertedPath == null)
            {
                return false;
            }

            // Convert to relative path if under data folder for portability
            var relativeThumbnailPath = _pathHelper.ToRelativePath(convertedPath) ?? convertedPath;

            category.Thumbnail = relativeThumbnailPath;
            var updated = await _repository.UpdateAsync(category).ConfigureAwait(false);

            if (updated)
            {
                InvalidateTreeCache();
            }

            return updated;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get Category by name (useful for migration thumbnail association)
    /// </summary>
    public async Task<CategoryInfo?> GetByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name).ConfigureAwait(false);
    }

    /// <summary>
    /// Check if a Category exists
    /// </summary>
    public async Task<bool> ExistsAsync(string categoryId)
    {
        return await _repository.ExistsAsync(categoryId).ConfigureAwait(false);
    }

    /// <summary>
    /// Recursively delete a category and all its children
    /// </summary>
    private async Task DeleteCategoryAndChildrenRecursiveAsync(string categoryId)
    {
        // Get all children
        var children = await _repository.GetChildrenAsync(categoryId).ConfigureAwait(false);

        // Recursively delete children first
        foreach (var child in children)
        {
            await DeleteCategoryAndChildrenRecursiveAsync(child.Id).ConfigureAwait(false);
        }

        // Delete the category itself
        await _repository.DeleteAsync(categoryId).ConfigureAwait(false);

        // Note: Thumbnails are not deleted here to avoid file lock issues
        // A separate cleanup tool can be used to remove orphaned thumbnails later
    }

    /// <summary>
    /// Calculate mod counts for all categories recursively
    /// Each category's ModCount = direct mods + all descendant mods
    /// </summary>
    private async Task CalculateModCountsAsync(List<CategoryInfo> categories)
    {
        // Get all mods from database once
        var allMods = await _modRepository.GetAllAsync().ConfigureAwait(false);

        // Group mods by category for quick lookup
        var modsByCategory = allMods
            .GroupBy(m => m.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate counts recursively for each root category
        foreach (var category in categories)
        {
            CalculateCategoryModCount(category, modsByCategory);
        }
    }

    /// <summary>
    /// Recursively calculate mod count for a category and all its descendants
    /// Returns the total count (category's mods + all descendant mods)
    /// </summary>
    private int CalculateCategoryModCount(CategoryInfo category, Dictionary<string, int> modsByCategory)
    {
        // Get direct mod count for this category
        var directCount = modsByCategory.TryGetValue(category.Id, out var count) ? count : 0;

        // Recursively calculate counts for all children
        var childrenCount = 0;
        foreach (var child in category.Children)
        {
            childrenCount += CalculateCategoryModCount(child, modsByCategory);
        }

        // Total count is direct + children
        category.ModCount = directCount + childrenCount;
        return category.ModCount;
    }
}
