using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for mod lifecycle operations (load/unload)
/// Responsibility: Load/unload operations with business logic (category conflicts, preview imports, events)
/// </summary>
public interface IModLifecycleService
{
    Task<ModLoadResult> LoadAsync(string sha);
    Task<bool> UnloadAsync(string sha);
}

/// <summary>
/// Service for mod lifecycle operations (load/unload)
/// Handles:
/// - Category conflict resolution (unload same-category mods)
/// - Cache enabling/disabling
/// - Archive extraction
/// - Preview image auto-import
/// - Event emissions (LOADED, UNLOADED)
/// - Mod type detection and updates
/// </summary>
public class ModLifecycleService : IModLifecycleService
{
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModCacheService _cacheService;
    private readonly IImageService _imageService;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModLifecycleService(
        IModRepository repository,
        IModArchiveService archiveService,
        IModCacheService cacheService,
        IImageService imageService,
        IProfilePathService profilePaths,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _repository = repository;
        _archiveService = archiveService;
        _cacheService = cacheService;
        _imageService = imageService;
        _profilePaths = profilePaths;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Load a mod by extracting its archive to cache directory
    /// Handles category conflict resolution: unloads all other mods in the same category
    /// If mod is already cached (disabled state), just rename it to enable
    /// Cache can be in loaded (active) or unloaded/disabled mode
    /// Detects and updates archive type if needed
    /// Emits LOADED and UNLOADED events for affected mods
    ///
    /// CONCURRENCY: Uses atomic file operation planner via cache/archive services - no locks needed
    /// RETRY: Planner handles automatic retries for transient IOException
    /// </summary>
    /// <param name="sha">SHA hash of the mod</param>
    public async Task<ModLoadResult> LoadAsync(string sha)
    {
        // Get mod information for category checking
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new ModException(ErrorCodes.MOD_NOT_FOUND, $"Mod not found: {sha}", new { sha });
        }

        // Track unloaded mods for efficient frontend updates
        var unloadedModShas = new List<string>();
        var modName = mod.Name ?? $"Mod {sha.Substring(0, 8)}";

        try
        {
            // BUSINESS LOGIC: Unload all mods in the same category first to prevent conflicts
            // This ensures only one mod per category is loaded at a time
            // EXCEPTION: Unclassified mods (empty/null/whitespace category) can be co-loaded
            var isUnclassified = string.IsNullOrWhiteSpace(mod.Category);

            if (!isUnclassified)
            {
                var sameCategoryMods = await _repository.GetByCategoryAsync(mod.Category).ConfigureAwait(false);
                var loadedSameCategoryMods = sameCategoryMods.Where(m => m.SHA != sha).ToList();

                // Populate IsLoaded flags to check which mods need to be unloaded
                PopulateIsLoadedFlags(loadedSameCategoryMods);

                var modsToUnload = loadedSameCategoryMods.Where(m => m.IsLoaded).ToList();

                if (modsToUnload.Any())
                {
                    _logger.Info($"Unloading {modsToUnload.Count} mod(s) in category '{mod.Category}' before loading '{modName}'", "ModLifecycleService");

                    foreach (var modToUnload in modsToUnload)
                    {
                        // Unload the mod via cache service
                        var unloadSuccess = await _cacheService.DisableCacheAsync(modToUnload.SHA).ConfigureAwait(false);

                        if (unloadSuccess)
                        {
                            // Track successfully unloaded mods for efficient frontend update
                            unloadedModShas.Add(modToUnload.SHA);
                            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.UNLOADED, new { Sha = modToUnload.SHA }).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.Warn($"Failed to unload mod '{modToUnload.Name}' (SHA: {modToUnload.SHA})", "ModLifecycleService");
                        }
                    }
                }
            }
            else
            {
                _logger.Info($"Loading unclassified mod '{modName}' - skipping category-based unloading (unclassified mods can be co-loaded)", "ModLifecycleService");
            }

            // Load the requested mod
            // Try to enable cache first (if mod was previously unloaded), otherwise extract archive
            var cacheEnabled = await _cacheService.EnableCacheAsync(sha).ConfigureAwait(false);

            if (cacheEnabled)
            {
                _logger.Info($"Enabled mod from cache: {sha}", "ModLifecycleService");
            }
            else
            {
                // Emit LOADING event before extraction (decompression takes time)
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADING, new { Sha = sha }).ConfigureAwait(false);

                // No cache exists, extract from archive
                var cacheDir = Path.Combine(_profilePaths.CacheModsDirectory, sha);
                var extractResult = await _archiveService.ExtractAsync(sha, cacheDir).ConfigureAwait(false);

                if (!extractResult.Success)
                {
                    var errorMessage = extractResult.ErrorMessage ?? "Failed to extract mod archive";

                    if (extractResult.Exception != null)
                    {
                        throw new ModException(ErrorCodes.MOD_EXTRACTION_FAILED,
                            errorMessage,
                            extractResult.Exception,
                            new { sha });
                    }
                    else
                    {
                        throw new ModException(ErrorCodes.MOD_EXTRACTION_FAILED,
                            errorMessage,
                            new { sha });
                    }
                }

                // Update mod Type in database if detected
                if (!string.IsNullOrEmpty(extractResult.DetectedType))
                {
                    await UpdateModTypeIfNeededAsync(sha, extractResult.DetectedType).ConfigureAwait(false);
                }

                _logger.Info($"Loaded mod: {sha} ({extractResult.FileCount} files)", "ModLifecycleService");
            }

            // Try to auto-import preview images from cache folder after loading
            var importedPreviews = await _imageService.TryAutoImportPreviewsFromCacheAsync(sha).ConfigureAwait(false);

            if (importedPreviews > 0)
            {
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new {
                    Sha = sha
                }).ConfigureAwait(false);
            }

            // Emit LOADED event
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Sha = sha }).ConfigureAwait(false);

            // Trigger cache cleanup for this category (fire-and-forget, non-blocking)
            // Only cleans up disabled caches within the same category
            // Skips cleanup for unclassified mods (null/empty/whitespace category)
            _ = Task.Run(async () =>
            {
                try
                {
                    var cleanedCount = await _cacheService.CleanupOldDisabledCachesAsync(mod.Category).ConfigureAwait(false);
                    if (cleanedCount > 0)
                    {
                        _logger.Info($"Cache cleanup: removed {cleanedCount} old disabled cache(s) from category '{mod.Category}'", "ModLifecycleService");
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.Warn($"Cache cleanup failed: {cleanupEx.Message}", "ModLifecycleService");
                }
            });

            return new ModLoadResult
            {
                LoadedModSha = sha,
                UnloadedModShas = unloadedModShas,
                Success = true
            };
        }
        catch (ModException)
        {
            // Re-throw ModException as-is for proper error handling
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading mod {sha}: {ex.Message}", "ModLifecycleService", ex);
            throw new ModException(ErrorCodes.UNKNOWN_ERROR, $"Failed to load mod: {ex.Message}", ex, new { sha, modName });
        }
    }

    /// <summary>
    /// Unload a mod by renaming its cache directory to DISABLED-{SHA} (disables cache)
    /// Emits UNLOADED event on success
    ///
    /// CONCURRENCY: Uses atomic file operation planner via cache service - no locks needed
    /// RETRY: Planner handles automatic retries for transient IOException
    /// </summary>
    public async Task<bool> UnloadAsync(string sha)
    {
        // Get mod info to retrieve category before unloading
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        var modCategory = mod?.Category;

        var success = await _cacheService.DisableCacheAsync(sha).ConfigureAwait(false);

        if (success)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.UNLOADED, new { Sha = sha }).ConfigureAwait(false);

            // Trigger cache cleanup for this category (fire-and-forget, non-blocking)
            // Only cleans up disabled caches within the same category
            // Skips cleanup for unclassified mods (null/empty/whitespace category)
            _ = Task.Run(async () =>
            {
                try
                {
                    var cleanedCount = await _cacheService.CleanupOldDisabledCachesAsync(modCategory).ConfigureAwait(false);
                    if (cleanedCount > 0)
                    {
                        _logger.Info($"Cache cleanup: removed {cleanedCount} old disabled cache(s) from category '{modCategory}'", "ModLifecycleService");
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.Warn($"Cache cleanup failed: {cleanupEx.Message}", "ModLifecycleService");
                }
            });
        }

        return success;
    }

    #region Private Helper Methods

    /// <summary>
    /// Populates IsLoaded flag for mods by checking if cache directory exists
    /// Lightweight version that only checks IsLoaded
    /// </summary>
    private void PopulateIsLoadedFlags(List<ModInfo> mods)
    {
        if (!Directory.Exists(_profilePaths.CacheModsDirectory))
        {
            // No cache directory means no loaded mods
            foreach (var mod in mods)
            {
                mod.IsLoaded = false;
            }
            return;
        }

        var loadedDirectories = Directory.GetDirectories(_profilePaths.CacheModsDirectory)
            .Select(Path.GetFileName)
            .Where(d => !string.IsNullOrEmpty(d) && !d.StartsWith("DISABLED-"))
            .ToHashSet();

        foreach (var mod in mods)
        {
            mod.IsLoaded = loadedDirectories.Contains(mod.SHA);
        }
    }

    /// <summary>
    /// Update mod Type field if it's empty or different from detected type
    /// </summary>
    private async Task UpdateModTypeIfNeededAsync(string sha, string detectedType)
    {
        try
        {
            var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
            if (mod == null)
            {
                return;
            }

            // Normalize both types for comparison (remove dots, lowercase)
            var storedType = (mod.Type ?? "").TrimStart('.').ToLowerInvariant();
            var normalizedDetectedType = detectedType.TrimStart('.').ToLowerInvariant();

            // Update if type is missing or different
            if (string.IsNullOrEmpty(storedType) || storedType != normalizedDetectedType)
            {
                var oldType = mod.Type;
                mod.Type = normalizedDetectedType;
                await _repository.UpdateAsync(mod).ConfigureAwait(false);

                _logger.Info($"Updated mod type: {sha} ({oldType ?? "empty"} -> {normalizedDetectedType})", "ModLifecycleService");
            }
        }
        catch (Exception ex)
        {
            // Don't fail the load operation if type update fails
            _logger.Warn($"Failed to update mod type for {sha}: {ex.Message}", "ModLifecycleService");
        }
    }

    #endregion
}
