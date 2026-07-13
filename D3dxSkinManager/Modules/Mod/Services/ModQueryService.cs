using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Context;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod query service
/// </summary>
public interface IModQueryService
{
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
    private readonly IMemoryCache _cache;
    private readonly IProfileEventBus _eventBus;
    private readonly string _activeModsCacheKey;

    public ModQueryService(
        IModRepository repository,
        ICategoryRepository categoryRepository,
        IModEnrichmentService enrichmentService,
        IProfilePathService profilePaths,
        IMemoryCache cache,
        IProfileEventBus eventBus,
        IProfileContext profileContext)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _enrichmentService = enrichmentService;
        _profilePaths = profilePaths;
        _cache = cache;
        _eventBus = eventBus;

        // Use profile-specific cache key since IMemoryCache is shared across all profiles
        _activeModsCacheKey = $"ActiveMods_{profileContext.ProfileId}";

        // Subscribe to CACHE_CHANGED event to invalidate active mods cache
        _eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED, _ =>
        {
            _cache.Remove(_activeModsCacheKey);
            return Task.CompletedTask;
        });
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
        // Get entities from repository
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);

        // Convert to domain models
        var mods = ModMapper.ToDomainList(entities);

        // Enrich with computed properties
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

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
        // Get entities from repository
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);

        // Convert to domain models
        var allMods = ModMapper.ToDomainList(entities);

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
        var entities = await _repository.GetByMultipleCategoriesAsync(descendantIds).ConfigureAwait(false);

        // Convert to domain models
        var matchingMods = ModMapper.ToDomainList(entities);

        // Enrich with computed properties
        await _enrichmentService.EnrichAllAsync(matchingMods).ConfigureAwait(false);

        return SortMods(matchingMods);
    }

    /// <summary>
    /// Get all mods that don't have a category assigned or have invalid categories
    /// Returns mods with empty/Unknown category OR categories that don't match any Category ID
    /// </summary>
    public async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        // Get entities from repository
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);

        // Convert to domain models
        var allMods = ModMapper.ToDomainList(entities);

        // Enrich with computed properties
        await _enrichmentService.EnrichAllAsync(allMods).ConfigureAwait(false);

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
        // Get entities from repository
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);

        // Convert to domain models
        var allMods = ModMapper.ToDomainList(entities);

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
    /// Uses IMemoryCache for performance - cache invalidated on CACHE_CHANGED event (load/unload/delete)
    /// Orchestration:
    /// 1. Check IMemoryCache first (fast path)
    /// 2. Scan cache folder for active mod IDs (not DISABLED-)
    /// 3. For each ID, get ModInfo from repository
    /// 4. Enrich ModInfo using enrichment service (populates status flags)
    /// 5. For orphaned mods (not in DB), return minimal ModInfo with IsOrphaned flag for cleanup
    /// </summary>
    public async Task<List<ModInfo>> GetActiveModsAsync()
    {
        // Use GetOrCreateAsync for cleaner cache-first pattern
        return await _cache.GetOrCreateAsync(_activeModsCacheKey, async entry =>
        {
            // CRITICAL: run the whole scan on the thread pool. This method is reached from an IPC handler
            // that executes on the WinForms UI thread, and the per-mod DB query (SQLite) + EnrichAsync
            // complete SYNCHRONOUSLY (SQLite has no real async I/O), so a bare `await Task.Yield()` only
            // yields once — the loop then hammers the UI thread. For a large loaded library that froze the
            // UI for ~5s on first call (blocking WebResourceRequested → thumbnails wouldn't load). Task.Run
            // moves it off the UI thread so the IPC handler's await genuinely yields the pump.
            return await Task.Run(async () =>
            {
                var activeMods = new List<ModInfo>();
                var cacheModsDir = _profilePaths.CacheModsDirectory;

                if (!Directory.Exists(cacheModsDir))
                {
                    return activeMods;  // Return empty list
                }

                // Step 1: Scan cache folder for active mod ids (not DISABLED-)
                var activeIds = Directory.GetDirectories(cacheModsDir)
                    .Where(dir => !ModConventions.IsIgnoredNonModFolder(dir))
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name) && !ModConventions.IsDisabledCacheName(name))
                    .Select(name => name!)
                    .ToList();

                if (activeIds.Count == 0) return activeMods;

                // Step 2: ONE DB query for all entities, indexed by id. Was N separate GetByIdAsync calls
                // (one SQLite round-trip per active mod) — O(N) queries collapsed to O(1).
                var byId = (await _repository.GetAllAsync().ConfigureAwait(false))
                    .GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());

                foreach (var id in activeIds)
                {
                    if (byId.TryGetValue(id, out var entity))
                    {
                        activeMods.Add(ModMapper.ToDomain(entity));
                    }
                    else
                    {
                        // Orphaned mod - exists in cache but not the DB. Name from truncated id; the
                        // remaining flags (IsLoaded/HasCache/CachePath) are filled by EnrichAllAsync below.
                        activeMods.Add(new ModInfo
                        {
                            Id = id,
                            Name = id.Length >= 6 ? id.Substring(0, 6) : id,
                            IsOrphaned = true,
                        });
                    }
                }

                // Step 3: ONE batch enrichment — scans the Mods/cache/previews directories ONCE for the
                // whole list (PopulateStatusFlags). Was per-mod EnrichAsync, which re-scanned the entire
                // library for every active mod (O(N*M)) — the cause of the multi-second UI freeze.
                var enriched = await _enrichmentService.EnrichAllAsync(activeMods).ConfigureAwait(false);

                return SortMods(enriched);
            }).ConfigureAwait(false);
        }) ?? new List<ModInfo>();  // Fallback to empty list if null
    }
}
