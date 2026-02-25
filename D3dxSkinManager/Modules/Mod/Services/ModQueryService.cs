using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod query service
/// </summary>
public interface IModQueryService
{
    Task<List<ModInfo>> SearchAsync(string searchTerm);
    Task<List<ModInfo>> FilterAsync(string? category = null, string? author = null,
        string? grading = null, bool? isLoaded = null, bool? isAvailable = null);
    Task<Dictionary<string, List<ModInfo>>> GetGroupedByObjectAsync();
    Task<ModStatistics> GetStatisticsAsync();
    Task<List<ModInfo>> GetModsByCategoryAsync(string categoryId);
    Task<List<ModInfo>> GetUnclassifiedModsAsync();
    Task<int> GetUnclassifiedCountAsync();
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<List<string>> GetDistinctAuthorsAsync();

    // Enrichment operations
    void PopulateStatusFlagsBulk(List<ModInfo> mods);
    Task PopulateCategoryNamesBulkAsync(List<ModInfo> mods);
    Task PopulateTagMetadataBulkAsync(List<ModInfo> mods);
}

/// <summary>
/// Service for querying and searching mods
/// Responsibility: Complex queries, search logic, filtering
/// </summary>
public class ModQueryService : IModQueryService
{
    private readonly IModRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProfilePathService _profilePaths;
    private readonly ICategoryService _categoryService;
    private readonly ITagRepository _tagRepository;

    public ModQueryService(
        IModRepository repository,
        ICategoryRepository categoryRepository,
        IProfilePathService profilePaths,
        ICategoryService categoryService,
        ITagRepository tagRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _profilePaths = profilePaths;
        _categoryService = categoryService;
        _tagRepository = tagRepository;
    }

    /// <summary>
    /// Search mods by keyword with support for negation (!) and AND logic
    /// </summary>
    public async Task<List<ModInfo>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await _repository.GetAllAsync().ConfigureAwait(false);
        }

        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);

        // Split search term into individual terms
        var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var results = allMods.Where(mod =>
        {
            // All terms must match (AND logic)
            foreach (var term in terms)
            {
                var isNegation = term.StartsWith("!");
                var searchValue = isNegation ? term.Substring(1) : term;

                var matches = ModMatchesSearchTerm(mod, searchValue);

                // If negation and matches, exclude
                if (isNegation && matches)
                {
                    return false;
                }

                // If not negation and doesn't match, exclude
                if (!isNegation && !matches)
                {
                    return false;
                }
            }

            return true;
        }).ToList();

        return results;
    }

    /// <summary>
    /// Filter mods by multiple criteria
    /// </summary>
    public async Task<List<ModInfo>> FilterAsync(
        string? category = null,
        string? author = null,
        string? grading = null,
        bool? isLoaded = null,
        bool? isAvailable = null)
    {
        var mods = await _repository.GetAllAsync().ConfigureAwait(false);

        if (!string.IsNullOrEmpty(category))
        {
            mods = mods.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(author))
        {
            mods = mods.Where(m => m.Author?.Equals(author, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        if (!string.IsNullOrEmpty(grading))
        {
            mods = mods.Where(m => m.Grading.Equals(grading, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (isLoaded.HasValue)
        {
            mods = mods.Where(m => m.IsLoaded == isLoaded.Value).ToList();
        }

        if (isAvailable.HasValue)
        {
            mods = mods.Where(m => m.IsAvailable == isAvailable.Value).ToList();
        }

        return mods;
    }

    /// <summary>
    /// Get mods grouped by object name
    /// </summary>
    public async Task<Dictionary<string, List<ModInfo>>> GetGroupedByObjectAsync()
    {
        var mods = await _repository.GetAllAsync().ConfigureAwait(false);
        return mods.GroupBy(m => m.Category)
                   .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Get statistics about mods
    /// </summary>
    public async Task<ModStatistics> GetStatisticsAsync()
    {
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);

        return new ModStatistics
        {
            TotalMods = allMods.Count,
            LoadedMods = allMods.Count(m => m.IsLoaded),
            AvailableMods = allMods.Count(m => m.IsAvailable),
            UniqueObjects = allMods.Select(m => m.Category).Distinct().Count(),
            UniqueAuthors = allMods.Where(m => !string.IsNullOrEmpty(m.Author))
                                    .Select(m => m.Author)
                                    .Distinct()
                                    .Count(),
            ModsByGrading = allMods.GroupBy(m => m.Grading)
                                    .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Get all mods that belong to a specific Category node
    /// If the node has children, includes all mods from child nodes recursively
    /// Uses the Category field to match mods (Category = categoryId)
    /// </summary>
    public async Task<List<ModInfo>> GetModsByCategoryAsync(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return new List<ModInfo>();
        }

        // Get all descendant node IDs (includes self + all children recursively)
        var descendantIds = await _categoryRepository.GetAllDescendantIdsAsync(categoryId).ConfigureAwait(false);

        // Get all mods matching any of these categories
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);
        var matchingMods = allMods
            .Where(mod => descendantIds.Contains(mod.Category))
            .ToList();

        return matchingMods;
    }

    /// <summary>
    /// Get all mods that don't have a category assigned or have invalid categories
    /// Returns mods with empty/Unknown category OR categories that don't match any Category ID
    /// </summary>
    public async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);
        var allCategories = await _categoryRepository.GetAllAsync().ConfigureAwait(false);

        // Get all valid Category IDs
        var validCategoryIds = new HashSet<string>(
            allCategories.Select(c => c.Id),
            StringComparer.OrdinalIgnoreCase
        );

        // Filter mods that:
        // 1. Don't have a category assigned (null/empty/unknown)
        // 2. Have a category that doesn't match any Category ID in the tree
        var unclassifiedMods = allMods
            .Where(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                         mod.Category.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                         !validCategoryIds.Contains(mod.Category))
            .ToList();

        return unclassifiedMods;
    }

    /// <summary>
    /// Get count of mods that don't have a category assigned or have invalid categories
    /// </summary>
    public async Task<int> GetUnclassifiedCountAsync()
    {
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);
        var allCategories = await _categoryRepository.GetAllAsync().ConfigureAwait(false);

        // Get all valid Category IDs
        var validCategoryIds = new HashSet<string>(
            allCategories.Select(c => c.Id),
            StringComparer.OrdinalIgnoreCase
        );

        // Count mods that:
        // 1. Don't have a category assigned (null/empty/unknown)
        // 2. Have a category that doesn't match any Category ID in the tree
        var count = allMods.Count(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                                         mod.Category.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                                         !validCategoryIds.Contains(mod.Category));

        return count;
    }

    private bool ModMatchesSearchTerm(ModInfo mod, string searchTerm)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();

        return mod.SHA.ToLowerInvariant().Contains(lowerSearch) ||
               mod.Name.ToLowerInvariant().Contains(lowerSearch) ||
               mod.Category.ToLowerInvariant().Contains(lowerSearch) ||
               (mod.Author?.ToLowerInvariant().Contains(lowerSearch) == true) ||
               (mod.Description?.ToLowerInvariant().Contains(lowerSearch) == true) ||
               mod.Tags.Any(t => t.ToLowerInvariant().Contains(lowerSearch));
    }

    /// <summary>
    /// Get distinct categories (object names) used by mods
    /// </summary>
    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        return await _repository.GetDistinctCategoriesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get distinct authors from all mods
    /// </summary>
    public async Task<List<string>> GetDistinctAuthorsAsync()
    {
        return await _repository.GetDistinctAuthorsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Populates status flags (IsLoaded, IsAvailable) for all mods in bulk by scanning directories once
    /// </summary>
    public void PopulateStatusFlagsBulk(List<ModInfo> mods)
    {
        var availableFiles = Directory.Exists(_profilePaths.ModsDirectory)
            ? Directory.GetFiles(_profilePaths.ModsDirectory)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f!)
                .ToHashSet()
            : new HashSet<string>();

        var loadedDirectories = Directory.Exists(_profilePaths.CacheModsDirectory)
            ? Directory.GetDirectories(_profilePaths.CacheModsDirectory)
                .Select(Path.GetFileName)
                .Where(d => !string.IsNullOrEmpty(d) && !d.StartsWith("DISABLED-"))
                .Select(d => d!)
                .ToHashSet()
            : new HashSet<string>();

        var allCacheDirectories = Directory.Exists(_profilePaths.CacheModsDirectory)
            ? Directory.GetDirectories(_profilePaths.CacheModsDirectory)
                .Select(Path.GetFileName)
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => d!.StartsWith("DISABLED-") ? d.Substring(9) : d)  // Remove DISABLED- prefix
                .ToHashSet()
            : new HashSet<string>();

        foreach (var mod in mods)
        {
            mod.IsAvailable = availableFiles.Contains(mod.SHA);
            mod.IsLoaded = loadedDirectories.Contains(mod.SHA);
            mod.HasCache = allCacheDirectories.Contains(mod.SHA);
        }
    }

    /// <summary>
    /// Populates CategoryName field for all mods based on their Category (Category ID)
    /// </summary>
    public async Task PopulateCategoryNamesBulkAsync(List<ModInfo> mods)
    {
        var categoryIds = mods
            .Where(m => !string.IsNullOrEmpty(m.Category))
            .Select(m => m.Category)
            .Distinct()
            .ToList();

        if (!categoryIds.Any())
            return;

        var categoryTree = await _categoryService.GetCategoryTreeAsync().ConfigureAwait(false);
        var categoryMap = new Dictionary<string, string>();

        void BuildCategoryMap(CategoryInfo node)
        {
            if (!string.IsNullOrEmpty(node.Id))
                categoryMap[node.Id] = node.Name;

            foreach (var child in node.Children)
                BuildCategoryMap(child);
        }

        foreach (var root in categoryTree)
            BuildCategoryMap(root);

        foreach (var mod in mods)
        {
            if (!string.IsNullOrEmpty(mod.Category) && categoryMap.TryGetValue(mod.Category, out var categoryName))
                mod.CategoryName = categoryName;
        }
    }

    /// <summary>
    /// Populates tag metadata (colors) for all mods
    /// </summary>
    public async Task PopulateTagMetadataBulkAsync(List<ModInfo> mods)
    {
        var allTagNames = mods
            .Where(m => m.Tags != null && m.Tags.Count > 0)
            .SelectMany(m => m.Tags!)
            .Distinct()
            .ToList();

        if (!allTagNames.Any())
            return;

        var allTags = await _tagRepository.GetAllAsync().ConfigureAwait(false);
        var tagMap = allTags.ToDictionary(t => t.Name, t => t);

        foreach (var mod in mods)
        {
            if (mod.Tags == null || mod.Tags.Count == 0)
                continue;

            mod.TagsWithMetadata = mod.Tags
                .Where(tagName => tagMap.ContainsKey(tagName))
                .Select(tagName => tagMap[tagName])
                .ToList();
        }
    }
}
