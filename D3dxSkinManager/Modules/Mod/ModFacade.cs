using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod.Models;
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
    private readonly IModImportService _importService;
    private readonly IModQueryService _queryService;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IModMetadataService _metadataService;
    private readonly IModTagService _tagService;
    private readonly IModKeybindingService _keybindingService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IImageService _imageService;
    private readonly IModCacheWatcher _cacheWatcher;

    public ModFacade(
        IModRepository repository,
        IModLifecycleService lifecycleService,
        IModCacheService cacheService,
        IModImportService importService,
        IModQueryService queryService,
        IModEnrichmentService enrichmentService,
        IModMetadataService metadataService,
        IModTagService tagService,
        IModKeybindingService keybindingService,
        IPayloadHelper payloadHelper,
        IImageService imageService,
        IModCacheWatcher cacheWatcher,
        ILogHelper logger) : base(logger)
    {
        _repository = repository;
        _lifecycleService = lifecycleService;
        _cacheService = cacheService;
        _importService = importService;
        _queryService = queryService;
        _enrichmentService = enrichmentService;
        _metadataService = metadataService;
        _tagService = tagService;
        _keybindingService = keybindingService;
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
            "GET_BY_SHA" => await GetModByIdAsync(request),
            "LOAD" => await LoadModAsync(request),
            "UNLOAD" => await UnloadModAsync(request),
            "GET_LOADED" => await GetLoadedModIdsAsync(),
            "IMPORT" => await ImportModAsync(request),
            "DELETE" => await DeleteModAsync(request),
            "DELETE_CACHE" => await DeleteCacheAsync(request),
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
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var mods = await _repository.GetAllAsync().ConfigureAwait(false);

        // Enrich all mods with status flags, category names, and tag metadata
        await _enrichmentService.EnrichAllAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        // Enrich single mod with status flags, category name, and tag metadata
        if (mod != null)
        {
            await _enrichmentService.EnrichAsync(mod).ConfigureAwait(false);
        }

        return mod;
    }

    public async Task<ModLoadResult> LoadModAsync(string sha)
    {
        // Delegate to service - it handles all business logic and event emissions
        return await _lifecycleService.LoadAsync(sha).ConfigureAwait(false);
    }

    public async Task<bool> UnloadModAsync(string sha)
    {
        // Delegate to service - it handles event emission
        return await _lifecycleService.UnloadAsync(sha).ConfigureAwait(false);
    }

    public async Task<List<string>> GetLoadedModIdsAsync()
    {
        return await _repository.GetLoadedIdsAsync().ConfigureAwait(false);
    }

    public async Task<ModInfo?> ImportModAsync(string filePath)
    {
        // Delegate to service - it handles event emission
        return await _importService.ImportAsync(filePath).ConfigureAwait(false);
    }

    public async Task<bool> DeleteModAsync(string sha)
    {
        // Delete from database using metadata service
        return await _metadataService.DeleteAsync(sha).ConfigureAwait(false);
    }

    public async Task<bool> DeleteCacheAsync(string sha)
    {
        // Delegate to service - it handles event emission
        return await _cacheService.DeleteCacheAsync(sha).ConfigureAwait(false);
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

    public async Task<bool> UpdateMetadataAsync(string sha, UpdateModMetadataRequest request)
    {
        // Delegate to service - it handles event emission
        // New merged ModMetadataService.UpdateAsync returns Task<ModInfo>, so we check if result is not null
        var result = await _metadataService.UpdateAsync(sha, request).ConfigureAwait(false);
        return result != null;
    }

    public async Task<bool> UpdateCategoryAsync(string sha, string category)
    {
        // Delegate to service - it handles event emission
        // New merged ModMetadataService doesn't need callbacks anymore
        var result = await _metadataService.UpdateCategoryAsync(sha, category).ConfigureAwait(false);
        return result != null;
    }

    public async Task<int> BatchUpdateCategoryAsync(List<string> shas, string category)
    {
        _logger.Info($"Starting batch category update for {shas.Count} mods to category '{category}'");

        // Delegate to service - it handles event emission
        // New merged ModMetadataService doesn't need callbacks anymore
        var updatedCount = await _metadataService.BatchUpdateCategoryAsync(shas, category).ConfigureAwait(false);

        _logger.Info($"Completed batch category update: {updatedCount} mods successfully updated");
        return updatedCount;
    }

    public async Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask)
    {
        // Create update request
        var request = new UpdateModMetadataRequest
        {
            Name = name,
            Author = author,
            Tags = tags,
            Grading = grading,
            Description = description
        };

        // Delegate to service - it handles event emissions for each mod
        return await _metadataService.BatchUpdateAsync(shas, request, fieldMask).ConfigureAwait(false);
    }

    public async Task<bool> ImportPreviewImageAsync(string sha, string imagePath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.ImportPreviewImageAsync(sha, imagePath).ConfigureAwait(false);
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

        // Delegate to ImageService - it handles event emission
        return await _imageService.ImportPreviewFromClipboardAsync(sha).ConfigureAwait(false);
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

        // Delegate to ImageService - it handles event emission
        return await _imageService.SetThumbnailAsync(sha, previewPath).ConfigureAwait(false);
    }

    public async Task<bool> DeletePreviewAsync(string sha, string previewPath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Delegate to ImageService - it handles event emission
        return await _imageService.DeletePreviewAsync(sha, previewPath).ConfigureAwait(false);
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

    private async Task<bool> DeleteCacheAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await DeleteCacheAsync(sha).ConfigureAwait(false);
    }

    private async Task<List<ModInfo>> SearchModsAsync(IpcRequest request)
    {
        var searchTerm = _payloadHelper.GetRequiredValue<string>(request.Payload, "searchTerm");
        return await SearchModsAsync(searchTerm).ConfigureAwait(false);
    }

    private async Task<bool> UpdateMetadataAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");

        var metadataRequest = new UpdateModMetadataRequest
        {
            Name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name"),
            Author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author"),
            Tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags"),
            Grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading"),
            Description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description"),
            DisablePreview = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "disablePreview")
        };

        return await UpdateMetadataAsync(sha, metadataRequest).ConfigureAwait(false);
    }

    private async Task<bool> UpdateCategoryAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        return await UpdateCategoryAsync(sha, category).ConfigureAwait(false);
    }

    private async Task<int> BatchUpdateCategoryAsync(IpcRequest request)
    {
        var shas = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "shas");
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        return await BatchUpdateCategoryAsync(shas, category).ConfigureAwait(false);
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
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        if (mod == null)
        {
            throw new InvalidOperationException($"Mod with SHA {sha} not found");
        }

        // Check cache path using cache service
        var cachePath = _cacheService.GetCachePath(sha);

        // Get preview directory path (for "Open Preview Folder" context menu)
        // Get any preview path and extract the directory
        var previewPaths = await _imageService.GetPreviewPathsAsync(sha).ConfigureAwait(false);
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

    public async Task<List<ModKeybinding>> GetKeybindingsAsync(string sha)
    {
        return await _keybindingService.ParseKeybindingsAsync(sha);
    }

    private async Task<List<ModKeybinding>> GetKeybindingsAsync(IpcRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");

        if (string.IsNullOrEmpty(sha))
            throw new ArgumentException("Mod SHA is required");

        return await GetKeybindingsAsync(sha);
    }
}
