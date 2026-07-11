using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for mod cache management
/// Responsibility: Manage disabled mod caches (DISABLED-{ID} directories)
/// </summary>
public interface IModCacheService
{
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);
    Task<bool> DeleteCacheAsync(string id);
    Task<BatchDeleteResult> BatchDeleteCachesAsync(List<string> ids);
    Task<bool> EnableCacheAsync(string id); // Rename DISABLED-{Id} to {Id}
    Task<bool> DisableCacheAsync(string id); // Rename {Id} to DISABLED-{Id}
    Task<int> CleanupOldDisabledCachesAsync(string? modCategory); // Cleanup old disabled caches for specific category
    bool HasCache(string id);
    string? GetCachePath(string id);
}

/// <summary>
/// Service for mod cache management
/// Responsibility: Manage disabled mod caches (DISABLED-{ID} directories)
///
/// Cache directory structure:
/// - Active/loaded cache: {WorkDirectory}/Mods/{Id}/
/// - Disabled/unloaded cache: {WorkDirectory}/Mods/DISABLED-{Id}/
/// </summary>
public class ModCacheService : IModCacheService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileOperationPlanner _operationPlanner;
    private readonly IModRepository _repository;
    private readonly IProfileService _profileService;
    private readonly IProfileContext _profileContext;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;
    private readonly IProcessRegistry _processRegistry;
    private const string DISABLED_PREFIX = ModConventions.DisabledCachePrefix;

    public ModCacheService(
        IProfilePathService profilePaths,
        IFileOperationPlanner operationPlanner,
        IModRepository repository,
        IProfileService profileService,
        IProfileContext profileContext,
        ILogHelper logger,
        IProfileEventBus eventBus,
        IProcessRegistry processRegistry)
    {
        _profilePaths = profilePaths;
        _operationPlanner = operationPlanner;
        _repository = repository;
        _profileService = profileService;
        _profileContext = profileContext;
        _logger = logger;
        _eventBus = eventBus;
        _processRegistry = processRegistry;
    }

    /// <summary>
    /// Enable cache by renaming DISABLED-{Id} to {Id}
    /// Extracted from ModFileService.LoadAsync (lines 188-210)
    /// Used when loading a mod that already has a disabled cache
    /// </summary>
    public async Task<bool> EnableCacheAsync(string id)
    {
        var disabledDirectory = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{id}");
        var targetDirectory = Path.Combine(_profilePaths.CacheModsDirectory, id);

        if (!Directory.Exists(disabledDirectory))
        {
            _logger.Warn($"Disabled cache not found: {disabledDirectory}", "ModCacheService");
            return false;
        }

        var moveOp = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = disabledDirectory,
            TargetPath = targetDirectory,
            Overwrite = true
        };

        var result = await _operationPlanner.SubmitOperationAsync(moveOp).ConfigureAwait(false);

        if (!result.Success)
        {
            var errorMessage = result.ErrorMessage ?? "Failed to enable mod from cache";

            if (result.Exception != null)
            {
                throw new OperationException(
                    ErrorCodes.FILE_ACCESS_DENIED,
                    new Dictionary<string, string> { { "id", id }, { "path", disabledDirectory } },
                    errorMessage,
                    result.Exception);
            }
            else
            {
                throw new OperationException(
                    ErrorCodes.FILE_ACCESS_DENIED,
                    new Dictionary<string, string> { { "id", id }, { "path", disabledDirectory } },
                    errorMessage);
            }
        }

        _logger.Info($"Enabled mod from cache: {id}", "ModCacheService");
        return true;
    }

    /// <summary>
    /// Disable cache by renaming {Id} to DISABLED-{Id}
    /// Extracted from ModFileService.UnloadInternalAsync (lines 377-408)
    /// Used when unloading a mod to preserve its cache in disabled state
    /// </summary>
    public async Task<bool> DisableCacheAsync(string id)
    {
        var cacheDirectory = Path.Combine(_profilePaths.CacheModsDirectory, id);
        if (!Directory.Exists(cacheDirectory))
        {
            _logger.Warn($"Mod not loaded: {id}", "ModCacheService");
            return false;
        }

        var disabledDirectory = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{id}");

        var moveOp = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = cacheDirectory,
            TargetPath = disabledDirectory,
            Overwrite = true
        };

        var result = await _operationPlanner.SubmitOperationAsync(moveOp).ConfigureAwait(false);

        if (!result.Success)
        {
            var errorMessage = result.ErrorMessage ?? "Failed to unload mod";

            if (result.Exception != null)
            {
                throw new OperationException(
                    ErrorCodes.MOD_FOLDER_IN_USE,
                    new Dictionary<string, string> { { "id", id }, { "path", cacheDirectory } },
                    errorMessage,
                    result.Exception);
            }
            else
            {
                throw new OperationException(
                    ErrorCodes.MOD_FOLDER_IN_USE,
                    new Dictionary<string, string> { { "id", id }, { "path", cacheDirectory } },
                    errorMessage);
            }
        }

        _logger.Info($"Unloaded mod (disabled cache): {id}", "ModCacheService");
        return true;
    }

    /// <summary>
    /// Delete specific cache by Id (both active and disabled cache)
    /// Extracted from ModFileService.DeleteCacheAsync (lines 695-757)
    /// Uses atomic file operation planner
    /// Emits CACHE_CHANGED event on success
    /// </summary>
    public async Task<bool> DeleteCacheAsync(string id)
    {
        bool anyDeleted = false;

        try
        {
            // Delete active/loaded cache: {Id}
            var activeCachePath = Path.Combine(_profilePaths.CacheModsDirectory, id);
            if (Directory.Exists(activeCachePath))
            {
                var deleteOp = new FileSystemOperation
                {
                    OperationType = FileSystemOperationType.DeleteDirectory,
                    SourcePath = activeCachePath
                };
                var result = await _operationPlanner.SubmitOperationAsync(deleteOp).ConfigureAwait(false);
                if (result.Success)
                {
                    _logger.Info($"Deleted active cache for Id: {id}", "ModCacheService");
                    anyDeleted = true;
                }
            }

            // Delete disabled/unloaded cache: DISABLED-{Id}
            var disabledCachePath = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{id}");
            if (Directory.Exists(disabledCachePath))
            {
                var deleteOp = new FileSystemOperation
                {
                    OperationType = FileSystemOperationType.DeleteDirectory,
                    SourcePath = disabledCachePath
                };
                var result = await _operationPlanner.SubmitOperationAsync(deleteOp).ConfigureAwait(false);
                if (result.Success)
                {
                    _logger.Info($"Deleted disabled cache for Id: {id}", "ModCacheService");
                    anyDeleted = true;
                }
            }

            if (!anyDeleted)
            {
                _logger.Warn($"No cache found to delete for Id: {id}", "ModCacheService");
            }
            else
            {
                // Emit cache changed event (FileSystemWatcher will also detect this, but emit anyway for consistency)
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
                {
                    Id = id,
                    WasLoaded = false, // Could be either, but now it's definitely gone
                    ChangeType = "deleted"
                }).ConfigureAwait(false);
            }

            return anyDeleted;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting cache for {id}: {ex.Message}", "ModCacheService", ex);
            return false;
        }
    }

    /// <summary>
    /// Batch delete caches for multiple mods
    /// Processes all deletions and returns summary of results
    /// Skips mods without cache (not counted as failure)
    /// Emits single CACHE_CHANGED event after all deletions complete
    /// </summary>
    public async Task<BatchDeleteResult> BatchDeleteCachesAsync(List<string> ids)
    {
        var result = new BatchDeleteResult();

        if (ids == null || ids.Count == 0)
        {
            return result;
        }

        _logger.Info($"Starting batch cache deletion for {ids.Count} mods", "ModCacheService");

        foreach (var id in ids)
        {
            try
            {
                // Check if cache exists before attempting deletion
                if (!HasCache(id))
                {
                    _logger.Debug($"Skipping {id} - no cache found", "ModCacheService");
                    continue; // Skip, don't count as success or failure
                }

                var success = await DeleteCacheAsync(id).ConfigureAwait(false);
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    // Cache exists but deletion failed - this is an actual error
                    result.FailedCount++;
                    result.FailedIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting cache for {id}: {ex.Message}", "ModCacheService", ex);
                result.FailedCount++;
                result.FailedIds.Add(id);
            }
        }

        _logger.Info($"Batch cache deletion completed: {result.SuccessCount} succeeded, {result.FailedCount} failed", "ModCacheService");

        // Emit single consolidated event after all deletions
        if (result.SuccessCount > 0)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
            {
                BatchOperation = true,
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                ChangeType = "batch_deleted"
            }).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Scan for disabled mod caches (DISABLED-{ID} directories) and categorize them
    /// Extracted from ModFileService.ScanCacheAsync (lines 571-624)
    /// </summary>
    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        var cacheItems = new List<CacheItem>();

        if (!Directory.Exists(_profilePaths.CacheModsDirectory))
        {
            return cacheItems;
        }

        // Get all mods from database
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);
        var allIds = entities.Select(e => e.Id).ToHashSet();
        var loadedIds = (await _repository.GetLoadedIdsAsync()).ToHashSet();

        // Scan for disabled cache directories
        var directories = Directory.GetDirectories(_profilePaths.CacheModsDirectory);

        foreach (var dir in directories)
        {
            var dirName = Path.GetFileName(dir);

            // Check if directory is a disabled cache
            if (!dirName.StartsWith(DISABLED_PREFIX))
            {
                continue;
            }

            // Extract ID from directory name
            var id = dirName.Substring(DISABLED_PREFIX.Length);

            // Calculate directory size
            long sizeBytes = FileUtilities.GetDirectorySize(dir);

            // Get last modified time
            var lastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss");

            // Categorize cache
            var category = CategorizCache(id, allIds, loadedIds);

            cacheItems.Add(new CacheItem
            {
                Path = dir,
                Id = id,
                SizeBytes = sizeBytes,
                Category = category,
                LastModified = lastModified
            });
        }

        return cacheItems;
    }

    /// <summary>
    /// Get cache statistics (counts and sizes by category)
    /// Extracted from ModFileService.GetCacheStatisticsAsync (lines 629-658)
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var cacheItems = await ScanCacheAsync().ConfigureAwait(false);

        var stats = new CacheStatistics();

        foreach (var item in cacheItems)
        {
            switch (item.Category)
            {
                case CacheCategory.Invalid:
                    stats.InvalidCount++;
                    stats.InvalidSizeBytes += item.SizeBytes;
                    break;
                case CacheCategory.RarelyUsed:
                    stats.RarelyUsedCount++;
                    stats.RarelyUsedSizeBytes += item.SizeBytes;
                    break;
                case CacheCategory.FrequentlyUsed:
                    stats.FrequentlyUsedCount++;
                    stats.FrequentlyUsedSizeBytes += item.SizeBytes;
                    break;
            }
        }

        stats.TotalCount = cacheItems.Count;
        stats.TotalSizeBytes = stats.InvalidSizeBytes + stats.RarelyUsedSizeBytes + stats.FrequentlyUsedSizeBytes;

        return stats;
    }

    /// <summary>
    /// Clean cache by category (delete all caches in the specified category)
    /// Extracted from ModFileService.CleanCacheAsync (lines 661-688)
    /// </summary>
    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var cacheItems = await ScanCacheAsync().ConfigureAwait(false);
        var itemsToDelete = cacheItems.Where(item => item.Category == category).ToList();

        int deletedCount = 0;
        var procId = _processRegistry.Start(ProcessType.Cleanup, $"Cleaning {category} caches",
            titleKey: "process.cleanCaches", titleArg: category.ToString());
        try
        {
            var total = itemsToDelete.Count;
            var processed = 0;
            foreach (var item in itemsToDelete)
            {
                try
                {
                    // Route through FileOperationPlanner so this delete is serialized with every
                    // other mod cache file operation (load/unload/extract/CleanupOldDisabledCaches).
                    // A raw Directory.Delete here runs concurrently with the planner worker and can
                    // collide with an in-flight move/extract on the same DISABLED-{id} directory.
                    var deleteOp = new FileSystemOperation
                    {
                        OperationType = FileSystemOperationType.DeleteDirectory,
                        SourcePath = item.Path
                    };

                    var result = await _operationPlanner.SubmitOperationAsync(deleteOp).ConfigureAwait(false);
                    if (result.Success)
                    {
                        deletedCount++;
                        _logger.Info($"Deleted cache: {item.Path}", "ModCacheService");
                    }
                    else
                    {
                        _logger.Error($"Failed to delete cache {item.Path}: {result.ErrorMessage}", "ModCacheService", result.Exception);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error deleting cache {item.Path}: {ex.Message}", "ModCacheService", ex);
                }
                processed++;
                _processRegistry.Report(procId, total > 0 ? (int)(processed * 100.0 / total) : null);
            }
            _processRegistry.Complete(procId);
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            throw;
        }

        if (deletedCount > 0)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
            {
                BatchOperation = true,
                ChangeType = "cleaned"
            }).ConfigureAwait(false);
        }

        return deletedCount;
    }

    /// <summary>
    /// Check if an Id has cached files (DISABLED-{Id} directory exists)
    /// Extracted from ModFileService.HasCache (lines 762-766)
    /// </summary>
    public bool HasCache(string id)
    {
        var cachePath = GetCachePath(id);
        return !string.IsNullOrEmpty(cachePath) && Directory.Exists(cachePath);
    }

    /// <summary>
    /// Get cache path for a specific Id
    /// Returns null if cache doesn't exist
    /// Checks both loaded ({Id}) and disabled (DISABLED-{Id}) cache directories
    /// </summary>
    public string? GetCachePath(string id)
    {
        // Check for loaded cache first (most common case when querying for deletion)
        var loadedCachePath = Path.Combine(_profilePaths.CacheModsDirectory, id);
        if (Directory.Exists(loadedCachePath))
        {
            return loadedCachePath;
        }

        // Check for disabled cache
        var disabledCachePath = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{id}");
        if (Directory.Exists(disabledCachePath))
        {
            return disabledCachePath;
        }

        return null;
    }

    /// <summary>
    /// Categorize cache based on ID presence in database and loaded state
    /// Extracted from ModFileService.CategorizCache (lines 778-797)
    /// </summary>
    private CacheCategory CategorizCache(string id, HashSet<string> allIds, HashSet<string> loadedIds)
    {
        // Invalid: ID not found in database at all
        if (!allIds.Contains(id))
        {
            return CacheCategory.Invalid;
        }

        // Frequently used: ID is currently loaded (shouldn't happen, but check anyway)
        if (loadedIds.Contains(id))
        {
            return CacheCategory.FrequentlyUsed;
        }

        // Rarely used: ID exists in database but not loaded
        return CacheCategory.RarelyUsed;
    }

    /// <summary>
    /// Clean up old disabled caches for a specific category based on profile configuration
    /// Only affects disabled caches of mods in the same category
    /// Unclassified mods (null/empty/whitespace category) are NOT cleaned up
    /// Keeps only the most recently disabled caches (by LastWriteTime)
    /// Uses FileOperationPlanner for atomic deletions
    /// </summary>
    /// <param name="modCategory">The category to clean up caches for. If null/empty/whitespace, no cleanup is performed.</param>
    /// <returns>Number of caches deleted</returns>
    public async Task<int> CleanupOldDisabledCachesAsync(string? modCategory)
    {
        try
        {
            // Skip cleanup for unclassified mods (null, empty, or whitespace category)
            if (string.IsNullOrWhiteSpace(modCategory))
            {
                _logger.Verbose("Skipping cache cleanup for unclassified mod", "ModCacheService");
                return 0;
            }

            // Get cache cleanup configuration
            var config = await _profileService.GetProfileConfigurationAsync(_profileContext.ProfileId).ConfigureAwait(false);

            if (config?.ModWork?.CleanupEnabled != true)
            {
                _logger.Verbose("Cache cleanup disabled in configuration", "ModCacheService");
                return 0; // Feature disabled
            }

            var maxCaches = config.ModWork.CleanupMaxCaches;
            if (maxCaches <= 0)
            {
                _logger.Warn($"Invalid MaxDisabledCaches value: {maxCaches}", "ModCacheService");
                return 0;
            }

            // Get all mods in the same category
            var categoryEntities = await _repository.GetByCategoryAsync(modCategory).ConfigureAwait(false);
            var categoryIds = categoryEntities.Select(e => e.Id).ToHashSet();

            // Scan for disabled caches of mods in this category
            var disabledCaches = GetDisabledCacheDirectories()
                .Where(path => {
                    var dirName = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(dirName) || !dirName.StartsWith(DISABLED_PREFIX))
                        return false;

                    var id = dirName.Substring(DISABLED_PREFIX.Length);
                    return categoryIds.Contains(id);
                })
                .OrderByDescending(d => Directory.GetLastWriteTime(d)) // Most recent first
                .ToList();

            if (disabledCaches.Count <= maxCaches)
            {
                _logger.Verbose($"Disabled cache count for category '{modCategory}': {disabledCaches.Count} (within limit {maxCaches})", "ModCacheService");
                return 0; // Within limit
            }

            // Delete oldest caches (beyond the limit)
            var cachesToDelete = disabledCaches.Skip(maxCaches).ToList();
            _logger.Info($"Cleaning up {cachesToDelete.Count} old disabled cache(s) for category '{modCategory}' (limit: {maxCaches}, current: {disabledCaches.Count})", "ModCacheService");

            int deletedCount = 0;

            foreach (var cachePath in cachesToDelete)
            {
                try
                {
                    var deleteOp = new FileSystemOperation
                    {
                        OperationType = FileSystemOperationType.DeleteDirectory,
                        SourcePath = cachePath
                    };

                    var result = await _operationPlanner.SubmitOperationAsync(deleteOp).ConfigureAwait(false);

                    if (result.Success)
                    {
                        deletedCount++;
                        var dirName = Path.GetFileName(cachePath);
                        _logger.Info($"Cleaned up old disabled cache: {dirName} (category: {modCategory})", "ModCacheService");
                    }
                    else
                    {
                        _logger.Warn($"Failed to clean up cache {cachePath}: {result.ErrorMessage}", "ModCacheService");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error cleaning up cache {cachePath}: {ex.Message}", "ModCacheService", ex);
                }
            }

            if (deletedCount > 0)
            {
                _logger.Info($"Cache cleanup completed for category '{modCategory}': {deletedCount} old cache(s) removed", "ModCacheService");
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during cache cleanup: {ex.Message}", "ModCacheService", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get all disabled cache directories (DISABLED-{ID})
    /// </summary>
    private List<string> GetDisabledCacheDirectories()
    {
        if (!Directory.Exists(_profilePaths.CacheModsDirectory))
        {
            return new List<string>();
        }

        return Directory.GetDirectories(_profilePaths.CacheModsDirectory)
            .Where(d => Path.GetFileName(d)?.StartsWith(DISABLED_PREFIX) == true)
            .ToList();
    }
}
