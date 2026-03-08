using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod query service
/// </summary>
public interface IModQueryService
{
    Task<List<ModInfo>> SearchAsync(string searchTerm);
    Task<List<ModInfo>> FilterAsync(string? category = null, string? author = null,
        string? grading = null, bool? isLoaded = null, bool? isAvailable = null);
    Task<ModStatistics> GetStatisticsAsync();
    Task<List<ModInfo>> GetModsByCategoryAsync(string categoryId);
    Task<List<ModInfo>> GetUnclassifiedModsAsync();
    Task<int> GetUnclassifiedCountAsync();
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<List<string>> GetDistinctAuthorsAsync();
    Task<List<ModInfo>> GetActiveModsAsync();
}

/// <summary>
/// Service for querying and searching mods
/// Responsibility: Complex queries, search logic, filtering
/// </summary>
public class ModQueryService : IModQueryService
{
    private readonly IModRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IProfilePathService _profilePaths;

    public ModQueryService(
        IModRepository repository,
        ICategoryRepository categoryRepository,
        IModEnrichmentService enrichmentService,
        IProfilePathService profilePaths)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _enrichmentService = enrichmentService;
        _profilePaths = profilePaths;
    }

    /// <summary>
    /// Search mods by keyword with support for negation (!) and AND logic
    /// </summary>
    public async Task<List<ModInfo>> SearchAsync(string searchTerm)
    {
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);

        // Filter by search term
        List<ModInfo> results;
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            results = allMods;
        }
        else
        {
            // Split search term into individual terms
            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            results = allMods.Where(mod =>
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
        }

        return SortMods(results);
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

        // Apply database-level filters
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

        // Apply runtime filters for computed properties (IsLoaded, IsAvailable)
        // These can't be done in SQL as they're calculated from file system state
        if (isLoaded.HasValue)
        {
            mods = mods.Where(m => m.IsLoaded == isLoaded.Value).ToList();
        }

        if (isAvailable.HasValue)
        {
            mods = mods.Where(m => m.IsAvailable == isAvailable.Value).ToList();
        }

        return SortMods(mods);
    }

    /// <summary>
    /// Get statistics about mods
    /// IMPORTANT: Enriches mods to populate IsLoaded and IsAvailable flags from file system
    /// </summary>
    public async Task<ModStatistics> GetStatisticsAsync()
    {
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);

        // CRITICAL: Populate status flags (IsLoaded, IsAvailable) by scanning directories
        // Without this, IsLoaded and IsAvailable will always be false!
        _enrichmentService.PopulateStatusFlags(allMods);

        return new ModStatistics
        {
            TotalMods = allMods.Count,
            LoadedMods = allMods.Count(m => m.IsLoaded),
            AvailableMods = allMods.Count(m => m.IsAvailable),
            TotalCategories = allMods.Select(m => m.Category).Distinct().Count(),
            TotalAuthors = allMods.Where(m => !string.IsNullOrEmpty(m.Author))
                                    .Select(m => m.Author)
                                    .Distinct()
                                    .Count()
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

        // Get mods by categories
        var matchingMods = await _repository.GetByMultipleCategoriesAsync(descendantIds).ConfigureAwait(false);

        return SortMods(matchingMods);
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
        // 1. Don't have a category assigned (null/empty)
        // 2. Have a category that doesn't match any Category ID in the tree
        var unclassifiedMods = allMods
            .Where(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                         !validCategoryIds.Contains(mod.Category))
            .ToList();

        return SortMods(unclassifiedMods);
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
        // 1. Don't have a category assigned (null/empty)
        // 2. Have a category that doesn't match any Category ID in the tree
        var count = allMods.Count(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                                         !validCategoryIds.Contains(mod.Category));

        return count;
    }

    /// <summary>
    /// Centralized sorting for all mod queries: sort by category name then mod name
    /// This ensures consistent ordering across all mod list views
    /// </summary>
    private List<ModInfo> SortMods(List<ModInfo> mods)
    {
        // Sort by CategoryName, fallback to Category ID
        // Then sort by mod Name
        return mods.OrderBy(mod => mod.CategoryName ?? mod.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
    /// Get active mods by scanning cache folder first, then matching with database
    /// Returns mods that are currently active in cache (not DISABLED-), including orphaned ones not in DB
    /// Orchestration:
    /// 1. Scan cache folder for active mod SHAs (not DISABLED-)
    /// 2. For each SHA, get ModInfo from repository
    /// 3. Enrich ModInfo using enrichment service (populates status flags)
    /// 4. For orphaned mods (not in DB), return minimal ModInfo with IsOrphaned flag for cleanup
    /// </summary>
    public async Task<List<ModInfo>> GetActiveModsAsync()
    {
        var activeMods = new List<ModInfo>();
        var cacheModsDir = _profilePaths.CacheModsDirectory;

        if (!Directory.Exists(cacheModsDir))
        {
            return activeMods;
        }

        // Step 1: Scan cache folder for active mods (not DISABLED-)
        var cacheDirs = Directory.GetDirectories(cacheModsDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && !name.StartsWith("DISABLED-"))
            .ToList();

        // Step 2-3: For each SHA found in cache, get from repository and enrich
        foreach (var sha in cacheDirs)
        {
            if (string.IsNullOrEmpty(sha)) continue;

            // Step 2: Get ModInfo from repository
            var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

            if (mod != null)
            {
                // Step 3: Enrich ModInfo (populate status flags, cache paths, etc.)
                var enriched = await _enrichmentService.EnrichAsync(mod).ConfigureAwait(false);
                activeMods.Add(enriched);
            }
            else
            {
                // Step 4: Orphaned mod - not in database but exists in cache
                // Create minimal ModInfo with IsOrphaned flag for frontend to handle i18n
                // Use truncated SHA (first 6 characters) for display name
                activeMods.Add(new ModInfo
                {
                    SHA = sha,
                    Name = sha.Length >= 6 ? sha.Substring(0, 6) : sha, // Truncate SHA for display
                    IsLoaded = true,
                    HasCache = true,
                    IsOrphaned = true,
                    CachePath = Path.Combine(cacheModsDir, sha)
                });
            }
        }

        return SortMods(activeMods);
    }
}
