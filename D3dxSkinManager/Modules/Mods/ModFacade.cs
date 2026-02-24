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
    Task<List<ModInfo>> GetModsByObjectAsync(string category);
    Task<List<string>> GetObjectNamesAsync();
    Task<List<string>> GetAuthorsAsync();
    Task<List<string>> GetTagsAsync();
    Task<List<ModInfo>> SearchModsAsync(string searchTerm);
    Task<ModStatistics> GetStatisticsAsync();

    // Metadata Operations
    Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description);
    Task<bool> UpdateCategoryAsync(string sha, string category);
    Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask);
    Task<bool> ImportPreviewImageAsync(string sha, string imagePath);
    Task<List<string>> GetPreviewPathsAsync(string sha);
    Task<bool> SetThumbnailAsync(string sha, string previewPath);
    Task<bool> DeletePreviewAsync(string sha, string previewPath);

    // Classification Operations
    Task<List<ClassificationNode>> GetClassificationTreeAsync();
    Task<bool> RefreshClassificationTreeAsync();
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
    private readonly IClassificationService _classificationService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitter _eventEmitter;
    private readonly IImageService _imageService;
    private readonly IProfilePathService _profilePaths;
    private readonly IPathHelper _pathHelper;

    public ModFacade(
        IModRepository repository,
        IModFileService fileService,
        IModImportService importService,
        IModQueryService queryService,
        IClassificationService classificationService,
        IPayloadHelper payloadHelper,
        IEventEmitter eventEmitter,
        IImageService imageService,
        IProfilePathService profilePaths,
        IPathHelper pathHelper,
        ILogHelper logger) : base(logger)
    {
        _repository = repository;
        _fileService = fileService;
        _importService = importService;
        _queryService = queryService;
        _classificationService = classificationService;
        _payloadHelper = payloadHelper;
        _eventEmitter = eventEmitter;
        _imageService = imageService;
        _profilePaths = profilePaths;
        _pathHelper = pathHelper;
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
            "GET_BY_OBJECT" => await GetModsByObjectAsync(request),
            "GET_OBJECT_NAMES" => await GetObjectNamesAsync(),
            "GET_AUTHORS" => await GetAuthorsAsync(),
            "GET_TAGS" => await GetTagsAsync(),
            "SEARCH" => await SearchModsAsync(request),
            "UPDATE_METADATA" => await UpdateMetadataAsync(request),
            "UPDATE_CATEGORY" => await UpdateCategoryAsync(request),
            "BATCH_UPDATE_METADATA" => await BatchUpdateMetadataAsync(request),
            "IMPORT_PREVIEW_IMAGE" => await ImportPreviewImageAsync(request),
            "GET_PREVIEW_PATHS" => await GetPreviewPathsAsync(request),
            "SET_THUMBNAIL" => await SetThumbnailAsync(request),
            "DELETE_PREVIEW" => await DeletePreviewAsync(request),
            "GET_CLASSIFICATION_TREE" => await GetClassificationTreeAsync(),
            "REFRESH_CLASSIFICATION_TREE" => await RefreshClassificationTreeAsync(),
            "GET_MODS_BY_CLASSIFICATION" => await GetModsByClassificationAsync(request),
            "GET_UNCLASSIFIED_MODS" => await GetUnclassifiedModsAsync(),
            "GET_UNCLASSIFIED_COUNT" => await GetUnclassifiedCountAsync(),
            "MOVE_CLASSIFICATION_NODE" => await MoveClassificationNodeAsync(request),
            "REORDER_CLASSIFICATION_NODE" => await ReorderClassificationNodeAsync(request),
            "CREATE_CLASSIFICATION_NODE" => await CreateClassificationNodeAsync(request),
            "UPDATE_CLASSIFICATION_NODE" => await UpdateClassificationNodeAsync(request),
            "DELETE_CLASSIFICATION_NODE" => await DeleteClassificationNodeAsync(request),
            "CHECK_CLASSIFICATION_NODE_EXISTS" => await CheckClassificationNodeExistsAsync(request),
            "CHECK_CLASSIFICATION_NAME_EXISTS" => await CheckClassificationNameExistsAsync(request),
            "CHECK_FILE_PATHS" => await CheckFilePathsAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var mods = await _repository.GetAllAsync().ConfigureAwait(false);

        // Populate status flags from file system (bulk operation for better performance)
        PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        // Populate status flags from file system for single mod
        if (mod != null)
        {
            var modList = new List<ModInfo> { mod };
            PopulateStatusFlagsBulk(modList);

            // Populate human-readable category name from classification service
            await PopulateCategoryNamesBulkAsync(modList).ConfigureAwait(false);
        }

        return mod;
    }

    public async Task<Models.ModLoadResult> LoadModAsync(string sha)
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
                PopulateStatusFlagsBulk(loadedSameCategoryMods);

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

    public async Task<List<ModInfo>> GetModsByObjectAsync(string category)
    {
        var mods = await _repository.GetByCategoryAsync(category).ConfigureAwait(false);

        // Populate status flags from file system
        PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<List<string>> GetObjectNamesAsync()
    {
        return await _repository.GetDistinctCategoriesAsync().ConfigureAwait(false);
    }

    public async Task<List<string>> GetAuthorsAsync()
    {
        return await _repository.GetDistinctAuthorsAsync().ConfigureAwait(false);
    }

    public async Task<List<string>> GetTagsAsync()
    {
        return await _repository.GetAllTagsAsync().ConfigureAwait(false);
    }

    public async Task<List<ModInfo>> SearchModsAsync(string searchTerm)
    {
        var mods = await _queryService.SearchAsync(searchTerm).ConfigureAwait(false);

        // Populate status flags from file system
        PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    public async Task<ModStatistics> GetStatisticsAsync()
    {
        return await _queryService.GetStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        if (name != null) mod.Name = name;
        if (author != null) mod.Author = author;
        if (tags != null) mod.Tags = tags;
        if (grading != null) mod.Grading = grading;
        if (description != null) mod.Description = description;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModEvents.METADATA_UPDATED, "mod.metadata.updated", new { sha, mod }).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> UpdateCategoryAsync(string sha, string category)
    {
        var mod = await GetModByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        _logger.Info($"Mod {sha} current state: IsLoaded={mod.IsLoaded}", "ModFacade");

        // If the mod is currently loaded, unload it since category determines which object it applies to
        if (mod.IsLoaded)
        {
            _logger.Info($"Mod {sha} is loaded, unloading before category change", "ModFacade");
            await UnloadModAsync(sha).ConfigureAwait(false);
            _logger.Info($"Mod {sha} unloaded", "ModFacade");
        }
        else
        {
            _logger.Info($"Mod {sha} is not loaded, skipping unload", "ModFacade");
        }

        mod.Category = category;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);

        // Re-fetch the mod to get the updated IsLoaded state from file system
        var updatedMod = await GetModByIdAsync(sha).ConfigureAwait(false);

        await _eventEmitter.EmitAsync(ModEvents.CATEGORY_UPDATED, "mod.category.updated", new { sha, category, mod = updatedMod ?? mod }).ConfigureAwait(false);

        // Refresh the classification tree cache to update counts
        await _classificationService.RefreshTreeAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask)
    {
        int updatedCount = 0;

        foreach (var sha in shas)
        {
            try
            {
                var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
                if (mod == null) continue;

                if (fieldMask.Contains("name") && name != null) mod.Name = name;
                if (fieldMask.Contains("author") && author != null) mod.Author = author;
                if (fieldMask.Contains("tags") && tags != null) mod.Tags = tags;
                if (fieldMask.Contains("grading") && grading != null) mod.Grading = grading;
                if (fieldMask.Contains("description") && description != null) mod.Description = description;

                await _repository.UpdateAsync(mod).ConfigureAwait(false);
                updatedCount++;

                await _eventEmitter.EmitAsync(ModEvents.METADATA_UPDATED, "mod.metadata.updated", new { sha, mod }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating mod {sha}: {ex.Message}", "ModFacade", ex);
            }
        }

        return updatedCount;
    }

    public async Task<bool> ImportPreviewImageAsync(string sha, string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file not found: {imagePath}");
        }

        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        if (!validExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Invalid image format: {extension}. Supported: {string.Join(", ", validExtensions)}");
        }

        // Use ImageService to get the preview paths and determine the next filename
        var existingPreviews = await _imageService.GetPreviewPathsAsync(sha).ConfigureAwait(false);

        // Generate next preview filename (preview1.png, preview2.png, etc.)
        int nextIndex = existingPreviews.Count + 1;
        var targetFileName = $"preview{nextIndex}{extension}";

        // Use ImageService's path resolution (previews/{SHA}/ folder)
        var targetPath = await CopyToPreviewDirectoryAsync(sha, imagePath, targetFileName).ConfigureAwait(false);

        // Convert to relative path if under data folder for portability
        var relativeTargetPath = _pathHelper.ToRelativePath(targetPath) ?? targetPath;

        await _eventEmitter.EmitAsync(ModEvents.PREVIEW_IMPORTED, "mod.preview.imported", new { sha, imagePath = targetPath }).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Copy an image to the mod's preview directory
    /// Helper method to centralize preview directory logic
    /// </summary>
    private Task<string> CopyToPreviewDirectoryAsync(string sha, string sourcePath, string targetFileName)
    {
        // Use ProfilePathService to get the correct preview directory (previews/{SHA}/)
        var targetDirectory = _profilePaths.GetPreviewDirectoryPath(sha);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var targetPath = Path.Combine(targetDirectory, targetFileName);
        File.Copy(sourcePath, targetPath, overwrite: true);

        return Task.FromResult(targetPath);
    }

    public async Task<List<string>> GetPreviewPathsAsync(string sha)
    {
        return await _imageService.GetPreviewPathsAsync(sha).ConfigureAwait(false);
    }

    public async Task<bool> SetThumbnailAsync(string sha, string previewPath)
    {
        // This method is kept for backward compatibility but no longer stores thumbnail selection
        // The first preview image (sorted alphabetically) is automatically used as thumbnail
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Convert to absolute path if needed for file existence check
        var absolutePreviewPath = _pathHelper.ToAbsolutePath(previewPath) ?? previewPath;

        if (!File.Exists(absolutePreviewPath))
        {
            throw new FileNotFoundException($"Preview image not found: {previewPath}");
        }

        // Emit event for UI update (thumbnail selection is now automatic based on file order)
        await _eventEmitter.EmitAsync(ModEvents.THUMBNAIL_UPDATED, "mod.thumbnail.updated", new { sha, previewPath }).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeletePreviewAsync(string sha, string previewPath)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Convert to absolute path for file operations
        var absolutePreviewPath = _pathHelper.ToAbsolutePath(previewPath) ?? previewPath;

        if (!File.Exists(absolutePreviewPath))
        {
            throw new FileNotFoundException($"Preview image not found: {previewPath}");
        }

        // Delete the file using absolute path
        File.Delete(absolutePreviewPath);
        await _eventEmitter.EmitAsync(ModEvents.PREVIEW_DELETED, "mod.preview.deleted", new { sha, previewPath }).ConfigureAwait(false);

        return true;
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

    private async Task<List<ModInfo>> GetModsByObjectAsync(IpcRequest request)
    {
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");
        return await GetModsByObjectAsync(category).ConfigureAwait(false);
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
    /// Refresh the classification tree cache
    /// Forces a reload of the classification tree from the database
    /// </summary>
    public async Task<bool> RefreshClassificationTreeAsync()
    {
        return await _classificationService.RefreshTreeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get all mods that belong to a specific classification node
    /// </summary>
    private async Task<List<ModInfo>> GetModsByClassificationAsync(IpcRequest request)
    {
        var classificationNodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "classificationNodeId");
        var mods = await _queryService.GetModsByClassificationAsync(classificationNodeId).ConfigureAwait(false);

        // Populate status flags from file system
        PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

        return mods;
    }

    /// <summary>
    /// Get all mods that don't have any classification tags
    /// </summary>
    private async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var mods = await _queryService.GetUnclassifiedModsAsync().ConfigureAwait(false);

        // Populate status flags from file system
        PopulateStatusFlagsBulk(mods);

        // Populate human-readable category names from classification service
        await PopulateCategoryNamesBulkAsync(mods).ConfigureAwait(false);

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
    /// Reorder a classification node within its siblings
    /// </summary>
    private async Task<bool> ReorderClassificationNodeAsync(IpcRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newPosition = _payloadHelper.GetRequiredValue<int>(request.Payload, "newPosition");

        var success = await _classificationService.ReorderNodeAsync(nodeId, newPosition).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                ModEvents.CLASSIFICATION_TREE_CHANGED,
                data: new { nodeId, newPosition }).ConfigureAwait(false);
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

        var result = new
        {
            originalPath = CheckOriginalPath(mod.SHA),
            workPath = CheckWorkPath(mod.SHA),
            thumbnailPath = CheckPreviewFolderPath(mod.SHA)
        };

        return result;
    }

    /// <summary>
    /// Checks if original archive file exists (file without extension)
    /// </summary>
    private string? CheckOriginalPath(string sha)
    {
        var originalArchivePath = Path.Combine(_profilePaths.ModsDirectory, sha);
        if (File.Exists(originalArchivePath))
        {
            return _pathHelper.ToRelativePath(originalArchivePath) ?? originalArchivePath;
        }
        return null;
    }

    /// <summary>
    /// Checks if work directory exists (extracted files)
    /// </summary>
    private string? CheckWorkPath(string sha)
    {
        var workPath = Path.Combine(_profilePaths.WorkModsDirectory, sha);
        if (Directory.Exists(workPath))
        {
            return _pathHelper.ToRelativePath(workPath) ?? workPath;
        }
        return null;
    }

    /// <summary>
    /// Checks if preview folder exists and contains preview images
    /// Returns the preview directory path if it exists, regardless of thumbnailPath
    /// </summary>
    private string? CheckPreviewFolderPath(string sha)
    {
        // Check for the preview directory (previews/{SHA}/)
        var previewDirectory = _profilePaths.GetPreviewDirectoryPath(sha);

        if (Directory.Exists(previewDirectory))
        {
            // Check if there are any preview images in the directory
            var hasPreviewImages = Directory.GetFiles(previewDirectory, "*.*")
                .Any(f => _imageService.GetSupportedImageExtensions()
                    .Contains(Path.GetExtension(f).ToLowerInvariant()));

            if (hasPreviewImages)
            {
                return _pathHelper.ToRelativePath(previewDirectory) ?? previewDirectory;
            }
        }

        return null;
    }

    /// <summary>
    /// Populates status flags (IsLoaded, IsAvailable) for all mods in bulk by scanning directories once
    /// IsLoaded: True if work directory exists without DISABLED- prefix
    /// IsAvailable: True if original archive file exists
    /// This is much faster than checking each mod individually
    /// </summary>
    private void PopulateStatusFlagsBulk(List<ModInfo> mods)
    {
        // Get all files in mods directory (for IsAvailable check)
        var availableFiles = Directory.Exists(_profilePaths.ModsDirectory)
            ? Directory.GetFiles(_profilePaths.ModsDirectory)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f!)
                .ToHashSet()
            : new HashSet<string>();

        // Get all directories in work/Mods directory (for IsLoaded check)
        var loadedDirectories = Directory.Exists(_profilePaths.WorkModsDirectory)
            ? Directory.GetDirectories(_profilePaths.WorkModsDirectory)
                .Select(Path.GetFileName)
                .Where(d => !string.IsNullOrEmpty(d) && !d.StartsWith("DISABLED-"))
                .Select(d => d!)
                .ToHashSet()
            : new HashSet<string>();

        // Populate flags for each mod using the cached directory listings
        foreach (var mod in mods)
        {
            mod.IsAvailable = availableFiles.Contains(mod.SHA);
            mod.IsLoaded = loadedDirectories.Contains(mod.SHA);
        }

        // Note: Disabled mods have their work directory renamed to DISABLED-{SHA}
        // So they're automatically excluded from the loadedDirectories set
    }

    /// <summary>
    /// Populates CategoryName field for all mods based on their Category (classification ID)
    /// Maps classification IDs (which may be GUIDs) to human-readable names
    /// </summary>
    private async Task PopulateCategoryNamesBulkAsync(List<ModInfo> mods)
    {
        // Get unique category IDs from all mods
        var categoryIds = mods
            .Where(m => !string.IsNullOrEmpty(m.Category))
            .Select(m => m.Category)
            .Distinct()
            .ToList();

        if (!categoryIds.Any())
        {
            // No categories to look up
            return;
        }

        // Get the classification tree once
        var classificationTree = await _classificationService.GetClassificationTreeAsync().ConfigureAwait(false);

        // Build a flat dictionary for quick lookups
        var nodeMap = new Dictionary<string, string>();
        BuildNodeMap(classificationTree, nodeMap);

        // Populate CategoryName for each mod
        foreach (var mod in mods)
        {
            if (string.IsNullOrEmpty(mod.Category))
            {
                mod.CategoryName = string.Empty;
            }
            else if (nodeMap.TryGetValue(mod.Category, out var categoryName))
            {
                mod.CategoryName = categoryName;
            }
            else
            {
                // If we can't find the category in the tree, use the ID as fallback
                // This handles legacy paths or deleted categories
                mod.CategoryName = mod.Category;
            }
        }
    }

    /// <summary>
    /// Recursively builds a flat dictionary of node ID to node name
    /// </summary>
    private void BuildNodeMap(List<ClassificationNode> nodes, Dictionary<string, string> nodeMap)
    {
        foreach (var node in nodes)
        {
            nodeMap[node.Id] = node.Name;
            if (node.Children != null && node.Children.Any())
            {
                BuildNodeMap(node.Children, nodeMap);
            }
        }
    }
}
