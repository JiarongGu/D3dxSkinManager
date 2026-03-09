using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for mod cache management
/// Responsibility: Manage disabled mod caches (DISABLED-{SHA} directories)
/// </summary>
public interface IModCacheService
{
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);
    Task<bool> DeleteCacheAsync(string sha);
    Task<BatchDeleteResult> BatchDeleteCachesAsync(List<string> shas);
    Task<bool> EnableCacheAsync(string sha); // Rename DISABLED-{SHA} to {SHA}
    Task<bool> DisableCacheAsync(string sha); // Rename {SHA} to DISABLED-{SHA}
    Task<int> CleanupOldDisabledCachesAsync(string? modCategory); // Cleanup old disabled caches for specific category
    bool HasCache(string sha);
    string? GetCachePath(string sha);
}

/// <summary>
/// Service for mod cache management
/// Responsibility: Manage disabled mod caches (DISABLED-{SHA} directories)
///
/// Cache directory structure:
/// - Active/loaded cache: {WorkDirectory}/Mods/{SHA}/
/// - Disabled/unloaded cache: {WorkDirectory}/Mods/DISABLED-{SHA}/
/// </summary>
public class ModCacheService : IModCacheService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileOperationPlanner _operationPlanner;
    private readonly IModRepository _repository;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileContext _profileContext;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;
    private const string DISABLED_PREFIX = "DISABLED-";

    public ModCacheService(
        IProfilePathService profilePaths,
        IFileOperationPlanner operationPlanner,
        IModRepository repository,
        IProfileRepository profileRepository,
        IProfileContext profileContext,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _profilePaths = profilePaths;
        _operationPlanner = operationPlanner;
        _repository = repository;
        _profileRepository = profileRepository;
        _profileContext = profileContext;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Enable cache by renaming DISABLED-{SHA} to {SHA}
    /// Extracted from ModFileService.LoadAsync (lines 188-210)
    /// Used when loading a mod that already has a disabled cache
    /// </summary>
    public async Task<bool> EnableCacheAsync(string sha)
    {
        var disabledDirectory = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{sha}");
        var targetDirectory = Path.Combine(_profilePaths.CacheModsDirectory, sha);

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
                    new Dictionary<string, string> { { "sha", sha }, { "path", disabledDirectory } },
                    errorMessage,
                    result.Exception);
            }
            else
            {
                throw new OperationException(
                    ErrorCodes.FILE_ACCESS_DENIED,
                    new Dictionary<string, string> { { "sha", sha }, { "path", disabledDirectory } },
                    errorMessage);
            }
        }

        _logger.Info($"Enabled mod from cache: {sha}", "ModCacheService");
        return true;
    }

    /// <summary>
    /// Disable cache by renaming {SHA} to DISABLED-{SHA}
    /// Extracted from ModFileService.UnloadInternalAsync (lines 377-408)
    /// Used when unloading a mod to preserve its cache in disabled state
    /// </summary>
    public async Task<bool> DisableCacheAsync(string sha)
    {
        var cacheDirectory = Path.Combine(_profilePaths.CacheModsDirectory, sha);
        if (!Directory.Exists(cacheDirectory))
        {
            _logger.Warn($"Mod not loaded: {sha}", "ModCacheService");
            return false;
        }

        var disabledDirectory = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{sha}");

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
                    new Dictionary<string, string> { { "sha", sha }, { "path", cacheDirectory } },
                    errorMessage,
                    result.Exception);
            }
            else
            {
                throw new OperationException(
                    ErrorCodes.MOD_FOLDER_IN_USE,
                    new Dictionary<string, string> { { "sha", sha }, { "path", cacheDirectory } },
                    errorMessage);
            }
        }

        _logger.Info($"Unloaded mod (disabled cache): {sha}", "ModCacheService");
        return true;
    }

    /// <summary>
    /// Delete specific cache by SHA (both active and disabled cache)
    /// Extracted from ModFileService.DeleteCacheAsync (lines 695-757)
    /// Uses atomic file operation planner
    /// Emits CACHE_CHANGED event on success
    /// </summary>
    public async Task<bool> DeleteCacheAsync(string sha)
    {
        bool anyDeleted = false;

        try
        {
            // Delete active/loaded cache: {SHA}
            var activeCachePath = Path.Combine(_profilePaths.CacheModsDirectory, sha);
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
                    _logger.Info($"Deleted active cache for SHA: {sha}", "ModCacheService");
                    anyDeleted = true;
                }
            }

            // Delete disabled/unloaded cache: DISABLED-{SHA}
            var disabledCachePath = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{sha}");
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
                    _logger.Info($"Deleted disabled cache for SHA: {sha}", "ModCacheService");
                    anyDeleted = true;
                }
            }

            if (!anyDeleted)
            {
                _logger.Warn($"No cache found to delete for SHA: {sha}", "ModCacheService");
            }
            else
            {
                // Emit cache changed event (FileSystemWatcher will also detect this, but emit anyway for consistency)
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
                {
                    Sha = sha,
                    WasLoaded = false, // Could be either, but now it's definitely gone
                    ChangeType = "deleted"
                }).ConfigureAwait(false);
            }

            return anyDeleted;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting cache for {sha}: {ex.Message}", "ModCacheService", ex);
            return false;
        }
    }

    /// <summary>
    /// Batch delete caches for multiple mods
    /// Processes all deletions and returns summary of results
    /// Skips mods without cache (not counted as failure)
    /// Emits single CACHE_CHANGED event after all deletions complete
    /// </summary>
    public async Task<BatchDeleteResult> BatchDeleteCachesAsync(List<string> shas)
    {
        var result = new BatchDeleteResult();

        if (shas == null || shas.Count == 0)
        {
            return result;
        }

        _logger.Info($"Starting batch cache deletion for {shas.Count} mods", "ModCacheService");

        foreach (var sha in shas)
        {
            try
            {
                // Check if cache exists before attempting deletion
                if (!HasCache(sha))
                {
                    _logger.Debug($"Skipping {sha} - no cache found", "ModCacheService");
                    continue; // Skip, don't count as success or failure
                }

                var success = await DeleteCacheAsync(sha).ConfigureAwait(false);
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    // Cache exists but deletion failed - this is an actual error
                    result.FailedCount++;
                    result.FailedShas.Add(sha);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting cache for {sha}: {ex.Message}", "ModCacheService", ex);
                result.FailedCount++;
                result.FailedShas.Add(sha);
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
    /// Scan for disabled mod caches (DISABLED-{SHA} directories) and categorize them
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
        var allMods = await _repository.GetAllAsync().ConfigureAwait(false);
        var allShas = allMods.Select(m => m.SHA).ToHashSet();
        var loadedShas = (await _repository.GetLoadedIdsAsync()).ToHashSet();

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

            // Extract SHA from directory name
            var sha = dirName.Substring(DISABLED_PREFIX.Length);

            // Calculate directory size
            long sizeBytes = FileUtilities.GetDirectorySize(dir);

            // Get last modified time
            var lastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss");

            // Categorize cache
            var category = CategorizCache(sha, allShas, loadedShas);

            cacheItems.Add(new CacheItem
            {
                Path = dir,
                Sha = sha,
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

        foreach (var item in itemsToDelete)
        {
            try
            {
                if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, recursive: true);
                    deletedCount++;
                    _logger.Info($"Deleted cache: {item.Path}", "ModCacheService");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting cache {item.Path}: {ex.Message}", "ModCacheService", ex);
            }
        }

        return deletedCount;
    }

    /// <summary>
    /// Check if a SHA has cached files (DISABLED-{SHA} directory exists)
    /// Extracted from ModFileService.HasCache (lines 762-766)
    /// </summary>
    public bool HasCache(string sha)
    {
        var cachePath = GetCachePath(sha);
        return !string.IsNullOrEmpty(cachePath) && Directory.Exists(cachePath);
    }

    /// <summary>
    /// Get cache path for a specific SHA
    /// Returns null if cache doesn't exist
    /// Checks both loaded ({SHA}) and disabled (DISABLED-{SHA}) cache directories
    /// </summary>
    public string? GetCachePath(string sha)
    {
        // Check for loaded cache first (most common case when querying for deletion)
        var loadedCachePath = Path.Combine(_profilePaths.CacheModsDirectory, sha);
        if (Directory.Exists(loadedCachePath))
        {
            return loadedCachePath;
        }

        // Check for disabled cache
        var disabledCachePath = Path.Combine(_profilePaths.CacheModsDirectory, $"{DISABLED_PREFIX}{sha}");
        if (Directory.Exists(disabledCachePath))
        {
            return disabledCachePath;
        }

        return null;
    }

    /// <summary>
    /// Categorize cache based on SHA presence in database and loaded state
    /// Extracted from ModFileService.CategorizCache (lines 778-797)
    /// </summary>
    private CacheCategory CategorizCache(string sha, HashSet<string> allShas, HashSet<string> loadedShas)
    {
        // Invalid: SHA not found in database at all
        if (!allShas.Contains(sha))
        {
            return CacheCategory.Invalid;
        }

        // Frequently used: SHA is currently loaded (shouldn't happen, but check anyway)
        if (loadedShas.Contains(sha))
        {
            return CacheCategory.FrequentlyUsed;
        }

        // Rarely used: SHA exists in database but not loaded
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
            var config = await _profileRepository.GetProfileConfigurationAsync(_profileContext.ProfileId).ConfigureAwait(false);

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
            var categoryMods = await _repository.GetByCategoryAsync(modCategory).ConfigureAwait(false);
            var categoryShas = categoryMods.Select(m => m.SHA).ToHashSet();

            // Scan for disabled caches of mods in this category
            var disabledCaches = GetDisabledCacheDirectories()
                .Where(path => {
                    var dirName = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(dirName) || !dirName.StartsWith(DISABLED_PREFIX))
                        return false;

                    var sha = dirName.Substring(DISABLED_PREFIX.Length);
                    return categoryShas.Contains(sha);
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
    /// Get all disabled cache directories (DISABLED-{SHA})
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
