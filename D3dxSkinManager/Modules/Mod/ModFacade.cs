using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Modules.Mod;

/// <summary>
/// Interface for Mod Management facade
/// Handles IPC messages only: MOD_GET_ALL, MOD_LOAD, MOD_UNLOAD, etc.
/// Prefix: MOD_*
///
/// NOTE: This facade should ONLY be used for IPC communication.
/// Other services should call the underlying services directly:
/// - IModRepository for data access
/// - IModLifecycleService for load/unload
/// - IModMetadataService for metadata updates
/// - IModQueryService for queries
/// - etc.
/// </summary>
public interface IModFacade : IModuleFacade
{
    // No public methods - facade only handles IPC routing
}

/// <summary>
/// Facade for coordinating mod-related operations
/// Responsibility: Mod management and metadata operations
/// IPC Prefix: MOD_*
/// </summary>
public class ModFacade : BaseFacade, IModFacade
{
    protected override string ModuleName => "ModFacade";

    private readonly IModRepository _repository;
    private readonly IModLifecycleService _lifecycleService;
    private readonly IModCacheService _cacheService;
    private readonly IModDeletionService _deletionService;
    private readonly IModImportService _importService;
    private readonly IModQueryService _queryService;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IModMetadataService _metadataService;
    private readonly IModTagService _tagService;
    private readonly IModKeybindingService _keybindingService;
    private readonly IModIniService _iniService;
    private readonly IModMergeService _mergeService;
    private readonly IModPresetService _presetService;
    private readonly IModArchiveService _archiveService;
    private readonly IModOperationQueue _operationQueue;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IImageService _imageService;
    private readonly IModCacheWatcher _cacheWatcher;

    public ModFacade(
        IModRepository repository,
        IModLifecycleService lifecycleService,
        IModCacheService cacheService,
        IModDeletionService deletionService,
        IModImportService importService,
        IModQueryService queryService,
        IModEnrichmentService enrichmentService,
        IModMetadataService metadataService,
        IModTagService tagService,
        IModKeybindingService keybindingService,
        IModIniService iniService,
        IModMergeService mergeService,
        IModPresetService presetService,
        IModArchiveService archiveService,
        IModOperationQueue operationQueue,
        IPayloadHelper payloadHelper,
        IImageService imageService,
        IModCacheWatcher cacheWatcher,
        ILogHelper logger) : base(logger)
    {
        _repository = repository;
        _lifecycleService = lifecycleService;
        _cacheService = cacheService;
        _deletionService = deletionService;
        _importService = importService;
        _queryService = queryService;
        _enrichmentService = enrichmentService;
        _metadataService = metadataService;
        _tagService = tagService;
        _keybindingService = keybindingService;
        _iniService = iniService;
        _mergeService = mergeService;
        _presetService = presetService;
        _archiveService = archiveService;
        _operationQueue = operationQueue;
        _payloadHelper = payloadHelper;
        _imageService = imageService;
        _cacheWatcher = cacheWatcher;

        // Start watching cache directory for external changes
        _cacheWatcher.StartWatching();
    }

    /// <summary>
    /// Routes incoming IPC messages to appropriate handler methods
    /// </summary>
    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_ALL" => await GetAllModsAsync(),
            "GET_BY_ID" => await GetModByIdAsync(request),
            "LOAD" => await LoadModAsync(request),
            "UNLOAD" => await UnloadModAsync(request),
            "GET_LOADED" => await GetLoadedModIdsAsync(),
            "GET_ACTIVE_MODS" => await GetActiveModsAsync(),
            "IMPORT" => await ImportModAsync(request),
            "UPDATE_MOD" => await UpdateModFromFileAsync(request),
            "DELETE" => await DeleteModAsync(request),
            "DELETE_CACHE" => await DeleteCacheAsync(request),
            "UPDATE_ARCHIVE_FROM_CACHE" => await UpdateArchiveFromCacheAsync(request),
            "BATCH_DELETE" => await BatchDeleteModsAsync(request),
            "BATCH_DELETE_CACHES" => await BatchDeleteCachesAsync(request),
            "GET_AUTHORS" => await GetAuthorsAsync(),
            "GET_TAGS" => await GetTagsAsync(),
            "GET_STATISTICS" => await GetStatisticsAsync(),

            "SEARCH" => await SearchModsAsync(request),
            "UPDATE_METADATA" => await UpdateMetadataAsync(request),
            "UPDATE_CATEGORY" => await UpdateCategoryAsync(request),
            "BATCH_UPDATE_CATEGORY" => await BatchUpdateCategoryAsync(request),
            "BATCH_UPDATE_METADATA" => await BatchUpdateMetadataAsync(request),
            "IMPORT_PREVIEW_IMAGE" => await ImportPreviewImageAsync(request),
            "CHECK_CLIPBOARD_HAS_IMAGE" => await CheckClipboardHasImageAsync(),
            "IMPORT_PREVIEW_FROM_CLIPBOARD" => await ImportPreviewFromClipboardAsync(request),
            "COPY_PREVIEW_TO_CLIPBOARD" => await CopyPreviewToClipboardAsync(request),
            "GET_PREVIEW_PATHS" => await GetPreviewPathsAsync(request),
            "SET_THUMBNAIL" => await SetThumbnailAsync(request),
            "DELETE_PREVIEW" => await DeletePreviewAsync(request),
            "GET_MODS_BY_CATEGORY" => await GetModsByCategoryAsync(request),
            "GET_UNCLASSIFIED_MODS" => await GetUnclassifiedModsAsync(),
            "GET_UNCLASSIFIED_COUNT" => await GetUnclassifiedCountAsync(),
            "CHECK_FILE_PATHS" => await CheckFilePathsAsync(request),
            "GET_ALL_TAGS" => await GetAllTagsAsync(),
            "GET_TAG_BY_NAME" => await GetTagByNameAsync(request),
            "UPSERT_TAG" => await UpsertTagAsync(request),
            "DELETE_TAG" => await DeleteTagAsync(request),
            "GET_USED_TAG_NAMES" => await GetUsedTagNamesAsync(),
            "GET_TAG_USAGE_COUNT" => await GetTagUsageCountAsync(request),
            "SEARCH_TAGS" => await SearchTagsAsync(request),
            "GET_KEYBINDINGS" => await GetKeybindingsAsync(request),
            "UPDATE_KEYBINDING" => await UpdateKeybindingAsync(request),
            "REORDER_KEYBINDINGS" => await ReorderKeybindingsAsync(request),
            "GET_INI_FILES" => await GetIniFilesAsync(request),
            "UPDATE_INI_ENTRY" => await UpdateIniEntryAsync(request),
            "MERGE_MODS" => await MergeModsAsync(request),

            // Preset operations
            "GET_PRESETS" => await GetPresetsAsync(),
            "SAVE_PRESET" => await SavePresetAsync(request),
            "UPDATE_PRESET" => await UpdatePresetAsync(request),
            "DELETE_PRESET" => await DeletePresetAsync(request),
            "APPLY_PRESET" => await ApplyPresetAsync(request),
            "UNLOAD_ALL_MODS" => await UnloadAllModsAsync(),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var entities = await _repository.GetAllAsync().ConfigureAwait(false);

        // Convert to domain models
        var mods = ModMapper.ToDomainList(entities);

        // Enrich all mods with status flags, category names, and tag metadata
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string id)
    {
        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);

        if (entity == null)
        {
            return null;
        }

        // Convert to domain model
        var mod = ModMapper.ToDomain(entity);

        // Enrich single mod with status flags, category name, and tag metadata
        await _enrichmentService.EnrichAsync(mod).ConfigureAwait(false);

        return mod;
    }

    public async Task<ModLoadResult> LoadModAsync(string id)
    {
        // Delegate to service - it handles all business logic and event emissions
        return await _lifecycleService.LoadAsync(id).ConfigureAwait(false);
    }

    public async Task<bool> UnloadModAsync(string id)
    {
        // Delegate to service - it handles event emission
        return await _lifecycleService.UnloadAsync(id).ConfigureAwait(false);
    }

    public async Task<List<string>> GetLoadedModIdsAsync()
    {
        return await _repository.GetLoadedIdsAsync().ConfigureAwait(false);
    }

    public async Task<List<ModInfo>> GetActiveModsAsync()
    {
        return await _queryService.GetActiveModsAsync().ConfigureAwait(false);
    }

    public async Task<ModInfo?> ImportModAsync(string filePath)
    {
        // Delegate to service - it handles event emission
        return await _importService.ImportAsync(filePath).ConfigureAwait(false);
    }

    public async Task<bool> DeleteModAsync(string id)
    {
        // Full deletion: cache → preview → archive → database
        return await _deletionService.DeleteAsync(id).ConfigureAwait(false);
    }

    public async Task<bool> DeleteCacheAsync(string id)
    {
        // Delegate to service - it handles event emission
        return await _cacheService.DeleteCacheAsync(id).ConfigureAwait(false);
    }

    public async Task<BatchDeleteResult> BatchDeleteModsAsync(List<string> ids)
    {
        // Delegate to deletion service - it handles all deletion steps and events
        return await _deletionService.BatchDeleteAsync(ids).ConfigureAwait(false);
    }

    public async Task<BatchDeleteResult> BatchDeleteCachesAsync(List<string> ids)
    {
        // Delegate to cache service - it handles event emission
        return await _cacheService.BatchDeleteCachesAsync(ids).ConfigureAwait(false);
    }

    public async Task<List<string>> GetAuthorsAsync()
    {
        return await _queryService.GetDistinctAuthorsAsync().ConfigureAwait(false);
    }

    public async Task<List<string>> GetTagsAsync()
    {
        // This method returns tag names used in mods (for backward compatibility)
        // Use GetAllTagsAsync() for full Tag objects with colors
        return await _tagService.GetUsedTagNamesAsync().ConfigureAwait(false);
    }

    public async Task<List<ModInfo>> SearchModsAsync(string searchTerm)
    {
        var mods = await _queryService.SearchAsync(searchTerm).ConfigureAwait(false);

        // Enrich all search results with status flags, category names, and tag metadata
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModStatistics> GetStatisticsAsync()
    {
        return await _queryService.GetStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<bool> UpdateMetadataAsync(string id, UpdateModMetadataRequest request)
    {
        // Delegate to service - it handles event emission
        // New merged ModMetadataService.UpdateAsync returns Task<ModInfo>, so we check if result is not null
        var result = await _metadataService.UpdateAsync(id, request).ConfigureAwait(false);
        return result != null;
    }

    public async Task<bool> UpdateCategoryAsync(string id, string category)
    {
        // Delegate to service - it handles event emission
        // New merged ModMetadataService doesn't need callbacks anymore
        var result = await _metadataService.UpdateCategoryAsync(id, category).ConfigureAwait(false);
        return result != null;
    }

    public async Task<int> BatchUpdateCategoryAsync(Dictionary<string, string> updates)
    {
        _logger.Info($"Starting batch category update for {updates.Count} mods with individual categories");

        // Delegate to service - it handles event emission
        var updatedCount = await _metadataService.BatchUpdateCategoryAsync(updates).ConfigureAwait(false);

        _logger.Info($"Completed batch category update: {updatedCount} mods successfully updated");
        return updatedCount;
    }

    public async Task<int> BatchUpdateMetadataAsync(Dictionary<string, UpdateModMetadataRequest> updates)
    {
        // Delegate to service - it handles event emissions for each mod
        return await _metadataService.BatchUpdateAsync(updates).ConfigureAwait(false);
    }

    public async Task<bool> ImportPreviewImageAsync(string id, string imagePath)
    {
        var exists = await _repository.ExistsAsync(id).ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.ImportPreviewImageAsync(id, imagePath).ConfigureAwait(false);
    }

    public async Task<bool> CheckClipboardHasImageAsync()
    {
        return await _imageService.CheckClipboardHasImageAsync().ConfigureAwait(false);
    }

    public async Task<bool> ImportPreviewFromClipboardAsync(string id)
    {
        var exists = await _repository.ExistsAsync(id).ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.ImportPreviewFromClipboardAsync(id).ConfigureAwait(false);
    }

    public async Task<bool> CopyPreviewToClipboardAsync(string previewPath)
    {
        // Delegate to ImageService
        return await _imageService.CopyPreviewToClipboardAsync(previewPath).ConfigureAwait(false);
    }

    public async Task<List<string>> GetPreviewPathsAsync(string id)
    {
        // Delegate auto-import to ImageService
        await _imageService.TryAutoImportPreviewsFromCacheAsync(id).ConfigureAwait(false);
        return await _imageService.GetPreviewPathsAsync(id).ConfigureAwait(false);
    }

    public async Task<bool> SetThumbnailAsync(string id, string previewPath)
    {
        var exists = await _repository.ExistsAsync(id).ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.SetThumbnailAsync(id, previewPath).ConfigureAwait(false);
    }

    public async Task<bool> DeletePreviewAsync(string id, string previewPath)
    {
        var exists = await _repository.ExistsAsync(id).ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.DeletePreviewAsync(id, previewPath).ConfigureAwait(false);
    }

    // ============= Message Handler Methods =============

    private async Task<ModInfo?> GetModByIdAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await GetModByIdAsync(id).ConfigureAwait(false);
    }

    private async Task<Models.ModLoadResult> LoadModAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _operationQueue.EnqueueAsync(id, () => LoadModAsync(id)).ConfigureAwait(false);
    }

    private async Task<bool> UnloadModAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _operationQueue.EnqueueAsync(id, () => UnloadModAsync(id)).ConfigureAwait(false);
    }

    private async Task<ModInfo?> ImportModAsync(IpcRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        return await ImportModAsync(filePath).ConfigureAwait(false);
    }

    private async Task<ModInfo?> UpdateModFromFileAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        // Per-mod lock: serialize against load/unload/delete/fix on the same mod.
        return await _operationQueue.EnqueueAsync(id, () => _importService.UpdateModAsync(id, filePath)).ConfigureAwait(false);
    }

    /// <summary>
    /// IPC: DELETE — fire-and-forget. Deleting spans cache + preview + archive + DB through the
    /// planner queue, which can block behind slower ops — awaiting it here froze the confirm dialog
    /// (and the UI) for the duration. The deletion service registers a ModDelete process (Activity
    /// panel) and the row refreshes via DELETED → MOD_LIST_UPDATED; failures emit REFRESHED to roll
    /// back the frontend's optimistic removal. See background-task-tracking.md.
    /// </summary>
    private Task<object?> DeleteModAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        _ = Task.Run(async () =>
        {
            try { await _operationQueue.EnqueueAsync(id, () => DeleteModAsync(id)).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"[ModFacade] Delete failed for {id}: {ex.Message}", ModuleName, ex); }
        });
        return Task.FromResult<object?>(new { started = true });
    }

    private async Task<bool> DeleteCacheAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _operationQueue.EnqueueAsync(id, () => DeleteCacheAsync(id)).ConfigureAwait(false);
    }

    private async Task<bool> UpdateArchiveFromCacheAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _operationQueue.EnqueueAsync(id, async () =>
        {
            // Get cache path — check both active and disabled cache
            var cachePath = _cacheService.GetCachePath(id);
            if (string.IsNullOrEmpty(cachePath))
            {
                throw new OperationException(
                    Core.Constants.ErrorCodes.MOD_NO_CACHE,
                    new Dictionary<string, string> { { "id", id } });
            }

            var success = await _archiveService.CompressCacheToArchiveAsync(id, cachePath).ConfigureAwait(false);
            if (!success)
            {
                throw new OperationException(
                    Core.Constants.ErrorCodes.MOD_ARCHIVE_UPDATE_FAILED,
                    new Dictionary<string, string> { { "id", id } });
            }

            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// IPC: BATCH_DELETE — fire-and-forget (see DELETE above). The deletion service owns ONE
    /// cancellable ModDelete process for the whole batch and reports per-item progress; the mod
    /// list updates incrementally via each DELETED event.
    /// </summary>
    private Task<object?> BatchDeleteModsAsync(IpcRequest request)
    {
        var ids = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "ids");
        _ = Task.Run(async () =>
        {
            try { await BatchDeleteModsAsync(ids).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"[ModFacade] Batch delete failed: {ex.Message}", ModuleName, ex); }
        });
        return Task.FromResult<object?>(new { started = true });
    }

    private async Task<BatchDeleteResult> BatchDeleteCachesAsync(IpcRequest request)
    {
        var ids = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "ids");
        return await BatchDeleteCachesAsync(ids).ConfigureAwait(false);
    }

    private async Task<List<ModInfo>> SearchModsAsync(IpcRequest request)
    {
        var searchTerm = _payloadHelper.GetRequiredValue<string>(request.Payload, "searchTerm");
        return await SearchModsAsync(searchTerm).ConfigureAwait(false);
    }

    private async Task<bool> UpdateMetadataAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var metadataRequest = new UpdateModMetadataRequest
        {
            Name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name"),
            Author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author"),
            Tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags"),
            Grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading"),
            Description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description"),
            DisablePreview = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "disablePreview")
        };

        return await UpdateMetadataAsync(id, metadataRequest).ConfigureAwait(false);
    }

    private async Task<bool> UpdateCategoryAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        return await UpdateCategoryAsync(id, category).ConfigureAwait(false);
    }

    private async Task<int> BatchUpdateCategoryAsync(IpcRequest request)
    {
        var updates = _payloadHelper.GetRequiredValue<Dictionary<string, string>>(request.Payload, "updates");

        return await BatchUpdateCategoryAsync(updates).ConfigureAwait(false);
    }

    private async Task<object> BatchUpdateMetadataAsync(IpcRequest request)
    {
        var updates = _payloadHelper.GetRequiredValue<Dictionary<string, UpdateModMetadataRequest>>(request.Payload, "updates");

        var updatedCount = await BatchUpdateMetadataAsync(updates).ConfigureAwait(false);

        return new { updatedCount, totalRequested = updates.Count };
    }

    private async Task<object> ImportPreviewImageAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var imagePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "imagePath");

        var success = await ImportPreviewImageAsync(id, imagePath).ConfigureAwait(false);

        return new { success, message = $"Preview image imported for mod: {id}" };
    }

    private async Task<object> ImportPreviewFromClipboardAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");

        var success = await ImportPreviewFromClipboardAsync(id).ConfigureAwait(false);

        return new { success, message = $"Preview image imported from clipboard for mod: {id}" };
    }

    private async Task<object> CopyPreviewToClipboardAsync(IpcRequest request)
    {
        var previewPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "previewPath");

        var success = await CopyPreviewToClipboardAsync(previewPath).ConfigureAwait(false);

        return new { success, message = $"Preview image copied to clipboard: {previewPath}" };
    }

    private async Task<List<string>> GetPreviewPathsAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await GetPreviewPathsAsync(id).ConfigureAwait(false);
    }

    private async Task<object> SetThumbnailAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var previewPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "previewPath");

        var success = await SetThumbnailAsync(id, previewPath).ConfigureAwait(false);

        return new { success, message = $"Thumbnail updated for mod: {id}" };
    }

    private async Task<object> DeletePreviewAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var previewPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "previewPath");

        var success = await DeletePreviewAsync(id, previewPath).ConfigureAwait(false);

        return new { success, message = $"Preview image deleted: {previewPath}" };
    }

    /// <summary>
    /// Get all mods that belong to a specific Category node
    /// </summary>
    private async Task<List<ModInfo>> GetModsByCategoryAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetRequiredValue<string>(request.Payload, "categoryId");
        var mods = await _queryService.GetModsByCategoryAsync(categoryId).ConfigureAwait(false);

        // Enrich all mods with status flags, category names, and tag metadata
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

        return mods;
    }

    /// <summary>
    /// Get all mods that don't have any Category tags
    /// </summary>
    private async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var mods = await _queryService.GetUnclassifiedModsAsync().ConfigureAwait(false);

        // Enrich all mods with status flags, category names, and tag metadata
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

        return mods;
    }

    /// <summary>
    /// Get count of mods that don't have any Category tags
    /// </summary>
    private async Task<int> GetUnclassifiedCountAsync()
    {
        return await _queryService.GetUnclassifiedCountAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if file paths exist for a mod (on-demand for context menu)
    /// Returns paths only if they exist on the file system
    /// </summary>
    private async Task<object> CheckFilePathsAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var exists = await _repository.ExistsAsync(id).ConfigureAwait(false);

        if (!exists)
        {
            throw new InvalidOperationException($"Mod with ID {id} not found");
        }

        // Check cache path using cache service
        var cachePath = _cacheService.GetCachePath(id);

        // Get preview directory path (for "Open Preview Folder" context menu)
        // Get any preview path and extract the directory
        var previewPaths = await _imageService.GetPreviewPathsAsync(id).ConfigureAwait(false);
        string? previewFolderPath = null;
        if (previewPaths.Count > 0)
        {
            // Extract directory from first preview file path
            var absolutePath = Path.GetFullPath(previewPaths[0]);
            previewFolderPath = Path.GetDirectoryName(absolutePath);
        }

        return new
        {
            originalPath = (string?)null,  // Archive path checking removed - no longer needed
            cachePath = cachePath,
            thumbnailPath = previewFolderPath  // Actually returns preview folder path, keeping name for compatibility
        };
    }


    // ============= Tag Management Methods =============

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        return await _tagService.GetAllTagsAsync();
    }

    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        return await _tagService.GetTagByNameAsync(name);
    }

    private async Task<Tag?> GetTagByNameAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");

        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Tag name is required");

        return await GetTagByNameAsync(name);
    }

    public async Task<bool> UpsertTagAsync(string name, string color)
    {
        return await _tagService.UpsertTagAsync(name, color);
    }

    private async Task<bool> UpsertTagAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var color = _payloadHelper.GetRequiredValue<string>(request.Payload, "color");

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(color))
            throw new ArgumentException("Tag name and color are required");

        return await UpsertTagAsync(name, color);
    }

    public async Task<bool> DeleteTagAsync(string name)
    {
        return await _tagService.DeleteTagAsync(name);
    }

    private async Task<bool> DeleteTagAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");

        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Tag name is required");

        return await DeleteTagAsync(name);
    }

    public async Task<List<string>> GetUsedTagNamesAsync()
    {
        return await _tagService.GetUsedTagNamesAsync();
    }

    public async Task<int> GetTagUsageCountAsync(string tag)
    {
        return await _tagService.GetTagUsageCountAsync(tag);
    }

    private async Task<int> GetTagUsageCountAsync(IpcRequest request)
    {
        var tag = _payloadHelper.GetRequiredValue<string>(request.Payload, "tag");

        if (string.IsNullOrEmpty(tag))
            throw new ArgumentException("Tag is required");

        return await GetTagUsageCountAsync(tag);
    }

    public async Task<List<Tag>> SearchTagsAsync(string searchTerm)
    {
        return await _tagService.SearchTagsAsync(searchTerm);
    }

    private async Task<List<Tag>> SearchTagsAsync(IpcRequest request)
    {
        var searchTerm = _payloadHelper.GetOptionalValue<string>(request.Payload, "searchTerm") ?? string.Empty;
        return await SearchTagsAsync(searchTerm);
    }

    // ============= Keybinding Methods =============

    public async Task<List<ModKeybinding>> GetKeybindingsAsync(string id)
    {
        return await _keybindingService.ParseKeybindingsAsync(id);
    }

    private async Task<List<ModKeybinding>> GetKeybindingsAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");

        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Mod ID is required");

        return await GetKeybindingsAsync(id);
    }

    private async Task<object> UpdateKeybindingAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var oldKey = _payloadHelper.GetRequiredValue<string>(request.Payload, "oldKey");
        var newKey = _payloadHelper.GetRequiredValue<string>(request.Payload, "newKey");
        var changed = await _keybindingService.UpdateKeybindingAsync(id, oldKey, newKey).ConfigureAwait(false);
        return new { changed };
    }

    /// <summary>IPC: REORDER_KEYBINDINGS — permute the [Key*] section blocks to match the given key order.</summary>
    private async Task<object> ReorderKeybindingsAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var keys = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "keys");
        await _keybindingService.ReorderKeybindingsAsync(id, keys).ConfigureAwait(false);
        return new { ok = true };
    }

    /// <summary>IPC: GET_INI_FILES — parse the mod's extracted .ini files into the editable model.</summary>
    private async Task<List<ModIniFile>> GetIniFilesAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _iniService.GetIniFilesAsync(id).ConfigureAwait(false);
    }

    /// <summary>IPC: UPDATE_INI_ENTRY — change one entry's value and patch just that .ini into the archive.</summary>
    private async Task<object> UpdateIniEntryAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var relativePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "relativePath");
        var lineIndex = _payloadHelper.GetRequiredValue<int>(request.Payload, "lineIndex");
        var newValue = _payloadHelper.GetRequiredValue<string>(request.Payload, "newValue");
        var line = await _iniService.UpdateEntryAsync(id, relativePath, lineIndex, newValue).ConfigureAwait(false);
        return new { line };
    }

    /// <summary>
    /// IPC: MERGE_MODS — combine several mods into a new cycle-merged mod (GIMI-style). Fire-and-forget:
    /// merging extracts/copies/compresses (slow), so it runs in the background and reports via the
    /// ProcessRegistry (Activity panel) — the IPC returns immediately so the UI is never blocked. The
    /// created mod appears via the IMPORTED → MOD_LIST_UPDATED event when done.
    /// </summary>
    private Task<object?> MergeModsAsync(IpcRequest request)
    {
        var ids = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "ids");
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var key = _payloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var activeOnly = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "activeOnly") ?? true;

        _ = Task.Run(async () =>
        {
            try { await _mergeService.MergeAsync(ids, name, key, activeOnly).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"[ModFacade] Merge failed: {ex.Message}", ModuleName, ex); }
        });

        return Task.FromResult<object?>(new { started = true });
    }

    // ============= Preset Methods =============

    private async Task<List<ModPresetInfo>> GetPresetsAsync()
    {
        return await _presetService.GetAllAsync().ConfigureAwait(false);
    }

    private async Task<ModPresetInfo> SavePresetAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        return await _presetService.SaveAsync(name).ConfigureAwait(false);
    }

    private async Task<ModPresetInfo> UpdatePresetAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        return await _presetService.UpdateAsync(id, name).ConfigureAwait(false);
    }

    private async Task<bool> DeletePresetAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _presetService.DeleteAsync(id).ConfigureAwait(false);
    }

    private async Task<ModPresetApplyResult> ApplyPresetAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await _presetService.ApplyAsync(id).ConfigureAwait(false);
    }

    private async Task<bool> UnloadAllModsAsync()
    {
        return await _presetService.UnloadAllAsync().ConfigureAwait(false);
    }
}
