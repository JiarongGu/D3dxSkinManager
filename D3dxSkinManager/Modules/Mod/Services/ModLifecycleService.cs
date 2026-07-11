using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for mod lifecycle operations (load/unload)
/// Responsibility: Load/unload operations with business logic (category conflicts, preview imports, events)
/// </summary>
public interface IModLifecycleService
{
    Task<ModLoadResult> LoadAsync(string id);
    Task<bool> UnloadAsync(string id);
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
    private readonly IModOperationQueue _operationQueue;
    private readonly IProcessRegistry _processRegistry;

    public ModLifecycleService(
        IModRepository repository,
        IModArchiveService archiveService,
        IModCacheService cacheService,
        IImageService imageService,
        IProfilePathService profilePaths,
        ILogHelper logger,
        IProfileEventBus eventBus,
        IModOperationQueue operationQueue,
        IProcessRegistry processRegistry)
    {
        _repository = repository;
        _archiveService = archiveService;
        _cacheService = cacheService;
        _imageService = imageService;
        _profilePaths = profilePaths;
        _logger = logger;
        _eventBus = eventBus;
        _operationQueue = operationQueue;
        _processRegistry = processRegistry;
    }

    /// <summary>
    /// Load a mod by extracting its archive to cache directory
    /// Handles category conflict resolution: unloads all other mods in the same category
    /// If mod is already cached (disabled state), just rename it to enable
    /// Cache can be in loaded (active) or unloaded/disabled mode
    /// Detects and updates archive type if needed
    /// Emits LOADED and UNLOADED events for affected mods
    ///
    /// CONCURRENCY: Serialized per category via IModOperationQueue so two concurrent loads of
    /// different mods in the SAME category cannot both run the read->unload-others->enable-self
    /// sequence and both end up loaded. Unclassified mods (null/empty category) run without a
    /// category lock (co-load allowed). Raw file ops are additionally serialized by the planner.
    /// This lock lives here (not only in ModFacade) so EVERY entry point — facade, preset,
    /// metadata — is covered. Lock order is always mod-lock (facade) -> category-lock (here),
    /// so no deadlock.
    /// RETRY: Planner handles automatic retries for transient IOException
    /// </summary>
    /// <param name="id">Mod ID</param>
    public async Task<ModLoadResult> LoadAsync(string id)
    {
        // Get mod information for category checking (read before taking the category lock)
        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
        {
            throw new OperationException(
                ErrorCodes.MOD_NOT_FOUND,
                new Dictionary<string, string> { { "id", id } },
                $"Mod not found: {id}"
            );
        }

        // Convert to domain model
        var mod = ModMapper.ToDomain(entity);

        return await _operationQueue
            .EnqueueCategoryOperationAsync(mod.Category, () => LoadInternalAsync(id, mod))
            .ConfigureAwait(false);
    }

    private async Task<ModLoadResult> LoadInternalAsync(string id, ModInfo mod)
    {
        // Track unloaded mods for efficient frontend updates
        var unloadedModIds = new List<string>();
        var modName = mod.Name ?? $"Mod {id.Substring(0, 8)}";

        // Track this load as a process so it shows in the status bar / Activity panel (extraction of a
        // large archive can take a while). Abandoned legacy taskStore is replaced by this registry.
        var procId = _processRegistry.Start(ProcessType.ModLoad, $"Loading mod: {modName}",
            titleKey: "process.modLoad", titleArg: modName);

        try
        {
            // BUSINESS LOGIC: Unload all mods in the same category first to prevent conflicts
            // This ensures only one mod per category is loaded at a time
            // EXCEPTION: Unclassified mods (empty/null/whitespace category) can be co-loaded
            var isUnclassified = string.IsNullOrWhiteSpace(mod.Category);

            if (!isUnclassified)
            {
                var sameCategoryEntities = await _repository.GetByCategoryAsync(mod.Category).ConfigureAwait(false);
                var sameCategoryMods = ModMapper.ToDomainList(sameCategoryEntities);
                var loadedSameCategoryMods = sameCategoryMods.Where(m => m.Id != id).ToList();

                // Populate IsLoaded flags to check which mods need to be unloaded
                PopulateIsLoadedFlags(loadedSameCategoryMods);

                var modsToUnload = loadedSameCategoryMods.Where(m => m.IsLoaded).ToList();

                if (modsToUnload.Any())
                {
                    _logger.Info($"Unloading {modsToUnload.Count} mod(s) in category '{mod.Category}' before loading '{modName}'", "ModLifecycleService");

                    foreach (var modToUnload in modsToUnload)
                    {
                        // Unload the mod via cache service
                        var unloadSuccess = await _cacheService.DisableCacheAsync(modToUnload.Id).ConfigureAwait(false);

                        if (unloadSuccess)
                        {
                            // Track successfully unloaded mods for efficient frontend update
                            unloadedModIds.Add(modToUnload.Id);
                            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.UNLOADED, new { Id = modToUnload.Id }).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.Warn($"Failed to unload mod '{modToUnload.Name}' (ID: {modToUnload.Id})", "ModLifecycleService");
                        }
                    }
                }
            }
            else
            {
                _logger.Info($"Loading unclassified mod '{modName}' - skipping category-based unloading (unclassified mods can be co-loaded)", "ModLifecycleService");
            }

            // Load the requested mod.
            // Fast path: re-enable the disabled cache (rename) IF it's still fresh. A disabled cache
            // goes stale when the archive is updated after the cache was made (e.g. a hash-fix or
            // mod-update recompressed the archive) — enabling it then would deploy OLD content. So if
            // the archive is newer than the disabled cache, discard the cache and re-extract. (#9)
            var cacheEnabled = false;
            if (IsDisabledCacheStale(id))
            {
                _logger.Info($"Disabled cache for '{modName}' is stale (archive is newer) — re-extracting", "ModLifecycleService");
                _processRegistry.Report(procId, null, "Refreshing stale cache", detailKey: "process.stage.refreshingCache");
                await _cacheService.DeleteCacheAsync(id).ConfigureAwait(false);
            }
            else
            {
                _processRegistry.Report(procId, null, "Enabling cache", detailKey: "process.stage.enablingCache");
                cacheEnabled = await _cacheService.EnableCacheAsync(id).ConfigureAwait(false);
            }

            if (cacheEnabled)
            {
                _logger.Info($"Enabled mod from cache: {id}", "ModLifecycleService");
            }
            else
            {
                // Emit LOADING event before extraction (decompression takes time)
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADING, new { Id = id }).ConfigureAwait(false);
                _processRegistry.Report(procId, null, "Extracting archive", detailKey: "process.stage.extractingArchive");

                // No (usable) cache, extract from archive
                var cacheDir = Path.Combine(_profilePaths.CacheModsDirectory, id);
                var extractResult = await _archiveService.ExtractAsync(id, cacheDir).ConfigureAwait(false);

                if (!extractResult.Success)
                {
                    var errorMessage = extractResult.ErrorMessage ?? "Failed to extract mod archive";

                    if (extractResult.Exception != null)
                    {
                        throw new OperationException(
                            ErrorCodes.MOD_EXTRACTION_FAILED,
                            new Dictionary<string, string> { { "id", id } },
                            errorMessage,
                            extractResult.Exception
                        );
                    }
                    else
                    {
                        throw new OperationException(
                            ErrorCodes.MOD_EXTRACTION_FAILED,
                            new Dictionary<string, string> { { "id", id } },
                            errorMessage
                        );
                    }
                }

                // Update mod Type in database if detected
                if (!string.IsNullOrEmpty(extractResult.DetectedType))
                {
                    await UpdateModTypeIfNeededAsync(id, extractResult.DetectedType).ConfigureAwait(false);
                }

                _logger.Info($"Loaded mod: {id} ({extractResult.FileCount} files)", "ModLifecycleService");
            }

            // Try to auto-import preview images from cache folder after loading
            var importedPreviews = await _imageService.TryAutoImportPreviewsFromCacheAsync(id).ConfigureAwait(false);

            if (importedPreviews > 0)
            {
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new {
                    Id = id
                }).ConfigureAwait(false);
            }

            // Emit LOADED event
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Id = id }).ConfigureAwait(false);

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

            _processRegistry.Complete(procId);

            return new ModLoadResult
            {
                LoadedModId = id,
                UnloadedModIds = unloadedModIds,
                Success = true
            };
        }
        catch (OperationException ex)
        {
            // Re-throw OperationException as-is for proper error handling
            _processRegistry.Fail(procId, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading mod {id}: {ex.Message}", "ModLifecycleService", ex);
            _processRegistry.Fail(procId, ex.Message);
            throw new OperationException(
                ErrorCodes.UNKNOWN_ERROR,
                new Dictionary<string, string> { { "id", id }, { "name", modName } },
                $"Failed to load mod: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Unload a mod by renaming its cache directory to DISABLED-{ID} (disables cache)
    /// Emits UNLOADED event on success
    ///
    /// CONCURRENCY: Uses atomic file operation planner via cache service - no locks needed
    /// RETRY: Planner handles automatic retries for transient IOException
    /// </summary>
    public async Task<bool> UnloadAsync(string id)
    {
        // Get mod info to retrieve category before unloading (read before taking the category lock)
        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        var modCategory = entity != null ? ModMapper.ToDomain(entity).Category : null;

        // Serialize per category alongside loads (a load unloads same-category mods)
        return await _operationQueue
            .EnqueueCategoryOperationAsync(modCategory, () => UnloadInternalAsync(id, modCategory))
            .ConfigureAwait(false);
    }

    private async Task<bool> UnloadInternalAsync(string id, string? modCategory)
    {
        var success = await _cacheService.DisableCacheAsync(id).ConfigureAwait(false);

        if (success)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.UNLOADED, new { Id = id }).ConfigureAwait(false);

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
    /// <summary>
    /// A disabled cache (DISABLED-{id}) is stale when the mod archive was modified after the cache was
    /// created — e.g. a hash-fix or mod-update recompressed the archive while the mod was unloaded.
    /// Re-enabling such a cache would deploy outdated content, so the caller re-extracts instead. (#9)
    /// Returns false when there's no disabled cache or no archive to compare against (nothing to invalidate).
    /// </summary>
    private bool IsDisabledCacheStale(string id)
    {
        var disabledCacheDir = Path.Combine(_profilePaths.CacheModsDirectory, $"{ModConventions.DisabledCachePrefix}{id}");
        if (!Directory.Exists(disabledCacheDir)) return false;

        try
        {
            var archivePath = _archiveService.GetArchivePath(id);
            if (!File.Exists(archivePath)) return false;
            return File.GetLastWriteTimeUtc(archivePath) > Directory.GetLastWriteTimeUtc(disabledCacheDir);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to check cache staleness for {id}: {ex.Message}", "ModLifecycleService");
            return false; // on doubt, keep the cache (existing behavior)
        }
    }

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
            .Where(d => !string.IsNullOrEmpty(d) && !ModConventions.IsDisabledCacheName(d))
            .ToHashSet();

        foreach (var mod in mods)
        {
            mod.IsLoaded = loadedDirectories.Contains(mod.Id);
        }
    }

    /// <summary>
    /// Update mod Type field if it's empty or different from detected type
    /// </summary>
    private async Task UpdateModTypeIfNeededAsync(string id, string detectedType)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
            if (entity == null)
            {
                return;
            }

            // Convert to domain model
            var mod = ModMapper.ToDomain(entity);

            // Normalize both types for comparison (remove dots, lowercase)
            var storedType = (mod.Type ?? "").TrimStart('.').ToLowerInvariant();
            var normalizedDetectedType = detectedType.TrimStart('.').ToLowerInvariant();

            // Update if type is missing or different
            if (string.IsNullOrEmpty(storedType) || storedType != normalizedDetectedType)
            {
                var oldType = mod.Type;
                mod.Type = normalizedDetectedType;

                // Convert to entity and update
                var updatedEntity = ModMapper.ToEntity(mod);
                await _repository.UpdateAsync(updatedEntity).ConfigureAwait(false);

                _logger.Info($"Updated mod type: {id} ({oldType ?? "empty"} -> {normalizedDetectedType})", "ModLifecycleService");
            }
        }
        catch (Exception ex)
        {
            // Don't fail the load operation if type update fails
            _logger.Warn($"Failed to update mod type for {id}: {ex.Message}", "ModLifecycleService");
        }
    }

    #endregion
}
