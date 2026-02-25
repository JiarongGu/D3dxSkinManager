using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Interface for Mod Management facade
/// Handles: MOD_GET_ALL, MOD_LOAD, MOD_UNLOAD, etc.
/// Prefix: MOD_*
/// </summary>
public interface IModFacade : IModuleFacade
{

    // Core Mod Operations
    Task<List<ModInfo>> GetAllModsAsync();
    Task<ModInfo?> GetModByIdAsync(string sha);
    Task<ModLoadResult> LoadModAsync(string sha);
    Task<bool> UnloadModAsync(string sha);
    Task<List<string>> GetLoadedModIdsAsync();
    Task<ModInfo?> ImportModAsync(string filePath);
    Task<bool> DeleteModAsync(string sha);

    // Query Operations
    Task<List<string>> GetAuthorsAsync();
    Task<List<string>> GetTagsAsync();
    Task<List<ModInfo>> SearchModsAsync(string searchTerm);
    Task<ModStatistics> GetStatisticsAsync();

    // Metadata Operations
    Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description);
    Task<bool> UpdateCategoryAsync(string sha, string category);
    Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask);
    Task<bool> ImportPreviewImageAsync(string sha, string imagePath);
    Task<bool> CheckClipboardHasImageAsync();
    Task<bool> ImportPreviewFromClipboardAsync(string sha);
    Task<List<string>> GetPreviewPathsAsync(string sha);
    Task<bool> SetThumbnailAsync(string sha, string previewPath);
    Task<bool> DeletePreviewAsync(string sha, string previewPath);

    // Classification Operations
    Task<List<ClassificationNode>> GetClassificationTreeAsync();

    // Tag Management Operations (Tags table - master tag definitions)
    Task<List<Tag>> GetAllTagsAsync();
    Task<Tag?> GetTagByNameAsync(string name);
    Task<bool> UpsertTagAsync(string name, string color);
    Task<bool> DeleteTagAsync(string name);
    Task<List<string>> GetUsedTagNamesAsync();
    Task<int> GetTagUsageCountAsync(string tag);
    Task<List<Tag>> SearchTagsAsync(string searchTerm);
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
    private readonly IModFileService _fileService;
    private readonly IModImportService _importService;
    private readonly IModQueryService _queryService;
    private readonly IModMetadataService _metadataService;
    private readonly ITagService _tagService;
    private readonly IClassificationService _classificationService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitter _eventEmitter;
    private readonly IImageService _imageService;

    public ModFacade(
        IModRepository repository,
        IModFileService fileService,
        IModImportService importService,
        IModQueryService queryService,
        IModMetadataService metadataService,
        ITagService tagService,
        IClassificationService classificationService,
        IPayloadHelper payloadHelper,
        IEventEmitter eventEmitter,
        IImageService imageService,
        ILogHelper logger) : base(logger)
    {
        _repository = repository;
        _fileService = fileService;
        _importService = importService;
        _queryService = queryService;
        _metadataService = metadataService;
        _tagService = tagService;
        _classificationService = classificationService;
        _payloadHelper = payloadHelper;
        _eventEmitter = eventEmitter;
        _imageService = imageService;
    }

    /// <summary>
    /// Routes incoming IPC messages to appropriate handler methods
    /// </summary>
    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_ALL" => await GetAllModsAsync(),
            "GET_BY_SHA" => await GetModByIdAsync(request),
            "LOAD" => await LoadModAsync(request),
            "UNLOAD" => await UnloadModAsync(request),
            "GET_LOADED" => await GetLoadedModIdsAsync(),
            "IMPORT" => await ImportModAsync(request),
            "DELETE" => await DeleteModAsync(request),
            "GET_AUTHORS" => await GetAuthorsAsync(),
            "GET_TAGS" => await GetTagsAsync(),
            "SEARCH" => await SearchModsAsync(request),
            "UPDATE_METADATA" => await UpdateMetadataAsync(request),
            "UPDATE_CATEGORY" => await UpdateCategoryAsync(request),
            "BATCH_UPDATE_METADATA" => await BatchUpdateMetadataAsync(request),
            "IMPORT_PREVIEW_IMAGE" => await ImportPreviewImageAsync(request),
            "CHECK_CLIPBOARD_HAS_IMAGE" => await CheckClipboardHasImageAsync(),
            "IMPORT_PREVIEW_FROM_CLIPBOARD" => await ImportPreviewFromClipboardAsync(request),
            "GET_PREVIEW_PATHS" => await GetPreviewPathsAsync(request),
            "SET_THUMBNAIL" => await SetThumbnailAsync(request),
            "DELETE_PREVIEW" => await DeletePreviewAsync(request),
            "GET_CLASSIFICATION_TREE" => await GetClassificationTreeAsync(),
            "GET_MODS_BY_CLASSIFICATION" => await GetModsByClassificationAsync(request),
            "GET_UNCLASSIFIED_MODS" => await GetUnclassifiedModsAsync(),
            "GET_UNCLASSIFIED_COUNT" => await GetUnclassifiedCountAsync(),
            "MOVE_CLASSIFICATION_NODE" => await MoveClassificationNodeAsync(request),
            "CREATE_CLASSIFICATION_NODE" => await CreateClassificationNodeAsync(request),
            "UPDATE_CLASSIFICATION_NODE" => await UpdateClassificationNodeAsync(request),
            "DELETE_CLASSIFICATION_NODE" => await DeleteClassificationNodeAsync(request),
            "CHECK_CLASSIFICATION_NODE_EXISTS" => await CheckClassificationNodeExistsAsync(request),
            "CHECK_CLASSIFICATION_NAME_EXISTS" => await CheckClassificationNameExistsAsync(request),
            "CHECK_FILE_PATHS" => await CheckFilePathsAsync(request),
            "GET_ALL_TAGS" => await GetAllTagsAsync(),
            "GET_TAG_BY_NAME" => await GetTagByNameAsync(request),
            "UPSERT_TAG" => await UpsertTagAsync(request),
            "DELETE_TAG" => await DeleteTagAsync(request),
            "GET_USED_TAG_NAMES" => await GetUsedTagNamesAsync(),
            "GET_TAG_USAGE_COUNT" => await GetTagUsageCountAsync(request),
            "SEARCH_TAGS" => await SearchTagsAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var mods = await _repository.GetAllAsync().ConfigureAwait(false);

        // Populate status flags from file system (bulk operation for better performance)
        _queryService.PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await _queryService.PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        // Populate tag metadata with colors (bulk operation to avoid N+1 queries)
        await _queryService.PopulateTagMetadataBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        // Populate status flags from file system for single mod
        if (mod != null)
        {
            var modList = new List<ModInfo> { mod };
            _queryService.PopulateStatusFlagsBulk(modList);

            // Populate human-readable category name from classification service
            await _queryService.PopulateCategoryNamesBulkAsync(modList).ConfigureAwait(false);

            // Populate tag metadata with colors
            await _queryService.PopulateTagMetadataBulkAsync(modList).ConfigureAwait(false);
        }

        return mod;
    }

    public async Task<ModLoadResult> LoadModAsync(string sha)
    {
        // Track unloaded mods for efficient frontend updates (avoids full mod list refresh)
        var unloadedModShas = new List<string>();

        // Get mod information for operation display and category checking
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new ModException(ErrorCodes.MOD_NOT_FOUND, $"Mod not found: {sha}", new { sha });
        }

        var modName = mod.Name ?? $"Mod {sha.Substring(0, 8)}";

        try
        {
            // CRITICAL: Unload all mods in the same category first to prevent conflicts
            // This ensures only one mod per category is loaded at a time
            if (!string.IsNullOrEmpty(mod.Category))
            {
                var sameCategoryMods = await _repository.GetByCategoryAsync(mod.Category).ConfigureAwait(false);
                var loadedSameCategoryMods = sameCategoryMods.Where(m => m.SHA != sha).ToList();

                // Populate IsLoaded flags to check which mods need to be unloaded
                _queryService.PopulateStatusFlagsBulk(loadedSameCategoryMods);

                var modsToUnload = loadedSameCategoryMods.Where(m => m.IsLoaded).ToList();

                if (modsToUnload.Any())
                {
                    _logger.Info($"Unloading {modsToUnload.Count} mod(s) in category '{mod.Category}' before loading '{modName}'", "ModFacade");

                    foreach (var modToUnload in modsToUnload)
                    {
                        var unloadSuccess = await _fileService.UnloadAsync(modToUnload.SHA).ConfigureAwait(false);

                        if (unloadSuccess)
                        {
                            // Track successfully unloaded mods for efficient frontend update
                            unloadedModShas.Add(modToUnload.SHA);
                            await _eventEmitter.EmitAsync(ModEvents.MOD_UNLOADED, data: new { Sha = modToUnload.SHA }).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.Warn($"Failed to unload mod '{modToUnload.Name}' (SHA: {modToUnload.SHA})", "ModFacade");
                        }
                    }
                }
            }

            // Load the requested mod
            var success = await _fileService.LoadAsync(sha).ConfigureAwait(false);

            if (!success)
            {
                return new ModLoadResult
                {
                    LoadedModSha = sha,
                    UnloadedModShas = unloadedModShas,
                    Success = false
                };
            }

            // Try to auto-import preview images from cache folder after loading
            await _imageService.TryAutoImportPreviewsFromCacheAsync(sha).ConfigureAwait(false);

            // Note: IsLoaded is determined dynamically from file system, not stored in database
            // No need to call SetLoadedStateAsync (it's a no-op)
            await _eventEmitter.EmitAsync(ModEvents.MOD_LOADED, data: new { Sha = sha }).ConfigureAwait(false);

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
            _logger.Error($"Error loading mod {sha}: {ex.Message}", "ModFacade", ex);
            throw new ModException(ErrorCodes.UNKNOWN_ERROR, $"Failed to load mod: {ex.Message}", ex, new { sha, modName });
        }
    }

    public async Task<bool> UnloadModAsync(string sha)
    {
        var success = await _fileService.UnloadAsync(sha).ConfigureAwait(false);
        if (!success) return false;

        // Note: IsLoaded is determined dynamically from file system, not stored in database
        // No need to call SetLoadedStateAsync (it's a no-op)
        await _eventEmitter.EmitAsync(ModEvents.MOD_UNLOADED, data: new { Sha = sha }).ConfigureAwait(false);

        return true;
    }

    public async Task<List<string>> GetLoadedModIdsAsync()
    {
        return await _repository.GetLoadedIdsAsync().ConfigureAwait(false);
    }

    public async Task<ModInfo?> ImportModAsync(string filePath)
    {
        var mod = await _importService.ImportAsync(filePath).ConfigureAwait(false);

        if (mod != null)
        {
            await _eventEmitter.EmitAsync(ModEvents.MOD_IMPORTED, data: mod).ConfigureAwait(false);
        }

        return mod;
    }

    public async Task<bool> DeleteModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null) return false;

        // Preview folder (previews/{sha}/) is handled by ClearModCacheAsync in DeleteAsync
        await _fileService.DeleteAsync(sha, null).ConfigureAwait(false);
        var success = await _repository.DeleteAsync(sha).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModEvents.MOD_DELETED, data: new { Sha = sha, Mod = mod }).ConfigureAwait(false);
        }

        return success;
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

        // Populate status flags from file system
        _queryService.PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await _queryService.PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        // Populate tag metadata with colors
        await _queryService.PopulateTagMetadataBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModStatistics> GetStatisticsAsync()
    {
        return await _queryService.GetStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description)
    {
        var success = await _metadataService.UpdateMetadataAsync(sha, name, author, tags, grading, description).ConfigureAwait(false);

        if (success)
        {
            var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
            await _eventEmitter.EmitAsync(ModEvents.METADATA_UPDATED, "mod.metadata.updated", new { sha, mod }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> UpdateCategoryAsync(string sha, string category)
    {
        var success = await _metadataService.UpdateCategoryAsync(sha, category, UnloadModAsync, GetModByIdAsync).ConfigureAwait(false);

        if (success)
        {
            // Re-fetch the mod to get the updated IsLoaded state from file system
            var updatedMod = await GetModByIdAsync(sha).ConfigureAwait(false);

            await _eventEmitter.EmitAsync(ModEvents.CATEGORY_UPDATED, "mod.category.updated", new { sha, category, mod = updatedMod }).ConfigureAwait(false);

            // Refresh the classification tree cache to update counts
            await _classificationService.RefreshTreeAsync().ConfigureAwait(false);
        }

        return success;
    }

    public async Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask)
    {
        var updatedCount = await _metadataService.BatchUpdateMetadataAsync(shas, name, author, tags, grading, description, fieldMask).ConfigureAwait(false);

        // Emit events for each successfully updated mod
        foreach (var sha in shas.Take(updatedCount))
        {
            var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
            if (mod != null)
            {
                await _eventEmitter.EmitAsync(ModEvents.METADATA_UPDATED, "mod.metadata.updated", new { sha, mod }).ConfigureAwait(false);
            }
        }

        return updatedCount;
    }

    public async Task<bool> ImportPreviewImageAsync(string sha, string imagePath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService for preview import
        var success = await _imageService.ImportPreviewImageAsync(sha, imagePath).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModEvents.PREVIEW_IMPORTED, "mod.preview.imported",
                new { sha, imagePath }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> CheckClipboardHasImageAsync()
    {
        return await _imageService.CheckClipboardHasImageAsync().ConfigureAwait(false);
    }

    public async Task<bool> ImportPreviewFromClipboardAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService for clipboard handling
        var success = await _imageService.ImportPreviewFromClipboardAsync(sha).ConfigureAwait(false);

        if (success)
        {
            // Get the newly imported preview path for the event
            var previews = await _imageService.GetPreviewPathsAsync(sha).ConfigureAwait(false);
            var latestPreview = previews.LastOrDefault();

            await _eventEmitter.EmitAsync(ModEvents.PREVIEW_IMPORTED, "mod.preview.imported",
                new { sha, imagePath = latestPreview }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<List<string>> GetPreviewPathsAsync(string sha)
    {
        // Delegate auto-import to ImageService
        await _imageService.TryAutoImportPreviewsFromCacheAsync(sha).ConfigureAwait(false);
        return await _imageService.GetPreviewPathsAsync(sha).ConfigureAwait(false);
    }

    public async Task<bool> SetThumbnailAsync(string sha, string previewPath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService for thumbnail reordering
        var success = await _imageService.SetThumbnailAsync(sha, previewPath).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModEvents.THUMBNAIL_UPDATED, "mod.thumbnail.updated",
                new { sha, previewPath }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> DeletePreviewAsync(string sha, string previewPath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService for preview deletion
        var success = await _imageService.DeletePreviewAsync(sha, previewPath).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModEvents.PREVIEW_DELETED, "mod.preview.deleted",
                new { sha, previewPath }).ConfigureAwait(false);
        }

        return success;
    }

    // ============= Message Handler Methods =============

    private async Task<ModInfo?> GetModByIdAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await GetModByIdAsync(sha).ConfigureAwait(false);
    }

    private async Task<Models.ModLoadResult> LoadModAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await LoadModAsync(sha).ConfigureAwait(false);
    }

    private async Task<bool> UnloadModAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await UnloadModAsync(sha).ConfigureAwait(false);
    }

    private async Task<ModInfo?> ImportModAsync(IpcRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        return await ImportModAsync(filePath).ConfigureAwait(false);
    }

    private async Task<bool> DeleteModAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await DeleteModAsync(sha).ConfigureAwait(false);
    }

    private async Task<List<ModInfo>> SearchModsAsync(IpcRequest request)
    {
        var searchTerm = _payloadHelper.GetRequiredValue<string>(request.Payload, "searchTerm");
        return await SearchModsAsync(searchTerm).ConfigureAwait(false);
    }

    private async Task<bool> UpdateMetadataAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");

        return await UpdateMetadataAsync(sha, name, author, tags, grading, description).ConfigureAwait(false);
    }

    private async Task<bool> UpdateCategoryAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        return await UpdateCategoryAsync(sha, category).ConfigureAwait(false);
    }

    private async Task<object> BatchUpdateMetadataAsync(IpcRequest request)
    {
        var shas = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "shas");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var fieldMask = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "fieldMask");

        var updatedCount = await BatchUpdateMetadataAsync(shas, name, author, tags, grading, description, fieldMask).ConfigureAwait(false);

        return new { updatedCount, totalRequested = shas.Count };
    }

    private async Task<object> ImportPreviewImageAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var imagePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "imagePath");

        var success = await ImportPreviewImageAsync(sha, imagePath).ConfigureAwait(false);

        return new { success, message = $"Preview image imported for mod: {sha}" };
    }

    private async Task<object> ImportPreviewFromClipboardAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");

        var success = await ImportPreviewFromClipboardAsync(sha).ConfigureAwait(false);

        return new { success, message = $"Preview image imported from clipboard for mod: {sha}" };
    }

    private async Task<List<string>> GetPreviewPathsAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await GetPreviewPathsAsync(sha).ConfigureAwait(false);
    }

    private async Task<object> SetThumbnailAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var previewPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "previewPath");

        var success = await SetThumbnailAsync(sha, previewPath).ConfigureAwait(false);

        return new { success, message = $"Thumbnail updated for mod: {sha}" };
    }

    private async Task<object> DeletePreviewAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var previewPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "previewPath");

        var success = await DeletePreviewAsync(sha, previewPath).ConfigureAwait(false);

        return new { success, message = $"Preview image deleted: {previewPath}" };
    }

    /// <summary>
    /// Get the classification tree from SQLite database
    /// Returns hierarchical tree structure with thumbnails
    /// </summary>
    public async Task<List<ClassificationNode>> GetClassificationTreeAsync()
    {
        return await _classificationService.GetClassificationTreeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get all mods that belong to a specific classification node
    /// </summary>
    private async Task<List<ModInfo>> GetModsByClassificationAsync(IpcRequest request)
    {
        var classificationNodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "classificationNodeId");
        var mods = await _queryService.GetModsByClassificationAsync(classificationNodeId).ConfigureAwait(false);

        // Populate status flags from file system
        _queryService.PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await _queryService.PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        // Populate tag metadata with colors
        await _queryService.PopulateTagMetadataBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    /// <summary>
    /// Get all mods that don't have any classification tags
    /// </summary>
    private async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var mods = await _queryService.GetUnclassifiedModsAsync().ConfigureAwait(false);

        // Populate status flags from file system
        _queryService.PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await _queryService.PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        // Populate tag metadata with colors
        await _queryService.PopulateTagMetadataBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    /// <summary>
    /// Get count of mods that don't have any classification tags
    /// </summary>
    private async Task<int> GetUnclassifiedCountAsync()
    {
        return await _queryService.GetUnclassifiedCountAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Move a classification node to a new parent
    /// </summary>
    private async Task<bool> MoveClassificationNodeAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newParentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "newParentId");
        var dropPosition = _payloadHelper.GetOptionalValue<int?>(request.Payload, "dropPosition");

        var success = await _classificationService.MoveNodeAsync(nodeId, newParentId, dropPosition).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                ModEvents.CLASSIFICATION_TREE_CHANGED,
                data: new { nodeId, newParentId, dropPosition }).ConfigureAwait(false);
        }

        return success;
    }

    /// <summary>
    /// Create a new classification node with auto-generated GUID
    /// </summary>
    private async Task<ClassificationNode?> CreateClassificationNodeAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetOptionalValue<string>(request.Payload, "nodeId"); // Deprecated, will be ignored
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var parentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "parentId");
        var priorityValue = _payloadHelper.GetOptionalValue<int?>(request.Payload, "priority");
        var priority = priorityValue ?? 100;
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var thumbnail = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnail");

        // The service will check for name uniqueness at the same level and auto-generate a GUID
        // nodeId parameter is deprecated and will be ignored
        var node = await _classificationService.CreateNodeAsync(string.Empty, name, parentId, priority, description, thumbnail).ConfigureAwait(false);

        if (node == null)
        {
            throw new InvalidOperationException($"Classification with name '{name}' already exists at this level. Please use a different name.");
        }

        if (node != null)
        {
            await _eventEmitter.EmitAsync(
                ModEvents.CLASSIFICATION_TREE_CHANGED,
                data: new { nodeId = node.Id, name, parentId, created = true }).ConfigureAwait(false);
        }

        return node;
    }

    /// <summary>
    /// Update a classification node's name, description, and thumbnail
    /// </summary>
    private async Task<bool> UpdateClassificationNodeAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var icon = _payloadHelper.GetOptionalValue<string>(request.Payload, "icon");

        var success = await _classificationService.UpdateNodeAsync(nodeId, name, description, icon).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                ModEvents.CLASSIFICATION_TREE_CHANGED,
                data: new { nodeId, name, description, icon }).ConfigureAwait(false);
        }

        return success;
    }

    /// <summary>
    /// Delete a classification node and all its children
    /// </summary>
    private async Task<bool> DeleteClassificationNodeAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");

        var success = await _classificationService.DeleteNodeAsync(nodeId).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                ModEvents.CLASSIFICATION_TREE_CHANGED,
                data: new { nodeId, deleted = true }).ConfigureAwait(false);
        }

        return success;
    }

    /// <summary>
    /// Check if a classification node exists by nodeId (GUID)
    /// </summary>
    private async Task<bool> CheckClassificationNodeExistsAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        return await _classificationService.NodeExistsAsync(nodeId).ConfigureAwait(false);
    }

    /// <summary>
    /// Check if a classification name already exists in the database
    /// Used for form validation to prevent duplicate names
    /// </summary>
    private async Task<bool> CheckClassificationNameExistsAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var excludeNodeId = _payloadHelper.GetOptionalValue<string>(request.Payload, "excludeNodeId");

        var node = await _classificationService.GetNodeByNameAsync(name).ConfigureAwait(false);

        // If no node found with this name, it doesn't exist
        if (node == null) return false;

        // If we're editing a node, exclude it from the check
        if (!string.IsNullOrEmpty(excludeNodeId) && node.Id == excludeNodeId)
        {
            return false; // Same node, not a duplicate
        }

        return true; // Name exists
    }

    /// <summary>
    /// Checks if file paths exist for a mod (on-demand for context menu)
    /// Returns paths only if they exist on the file system
    /// </summary>
    private async Task<object> CheckFilePathsAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        if (mod == null)
        {
            throw new InvalidOperationException($"Mod with SHA {sha} not found");
        }

        var result = await _fileService.CheckFilePathsAsync(sha).ConfigureAwait(false);

        return new
        {
            originalPath = result.OriginalPath,
            cachePath = result.CachePath,
            thumbnailPath = result.ThumbnailPath
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
}
