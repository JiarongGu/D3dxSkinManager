using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Plugins.Services;

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
    Task<bool> LoadModAsync(string sha);
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
    private readonly IEventEmitterHelper _eventEmitter;
    private readonly Core.Services.IImageService _imageService;

    public ModFacade(
        IModRepository repository,
        IModFileService fileService,
        IModImportService importService,
        IModQueryService queryService,
        IClassificationService classificationService,
        IPayloadHelper payloadHelper,
        IEventEmitterHelper eventEmitter,
        Core.Services.IImageService imageService,
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
    }

    /// <summary>
    /// Routes incoming IPC messages to appropriate handler methods
    /// </summary>
    protected override async Task<object?> RouteMessageAsync(MessageRequest request)
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
            "GET_CLASSIFICATION_TREE" => await GetClassificationTreeAsync(),
            "REFRESH_CLASSIFICATION_TREE" => await RefreshClassificationTreeAsync(),
            "GET_MODS_BY_CLASSIFICATION" => await GetModsByClassificationAsync(request),
            "GET_UNCLASSIFIED_MODS" => await GetUnclassifiedModsAsync(),
            "GET_UNCLASSIFIED_COUNT" => await GetUnclassifiedCountAsync(),
            "MOVE_CLASSIFICATION_NODE" => await MoveClassificationNodeAsync(request),
            "REORDER_CLASSIFICATION_NODE" => await ReorderClassificationNodeAsync(request),
            "UPDATE_CLASSIFICATION_NODE" => await UpdateClassificationNodeAsync(request),
            "DELETE_CLASSIFICATION_NODE" => await DeleteClassificationNodeAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var mods = await _repository.GetAllAsync();
        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        return mod;
    }

    public async Task<bool> LoadModAsync(string sha)
    {
        var success = await _fileService.LoadAsync(sha);
        if (!success) return false;

        await _repository.SetLoadedStateAsync(sha, true);
        await _eventEmitter.EmitAsync(PluginEventType.ModLoaded, data: new { Sha = sha });

        return true;
    }

    public async Task<bool> UnloadModAsync(string sha)
    {
        var success = await _fileService.UnloadAsync(sha);
        if (!success) return false;

        await _repository.SetLoadedStateAsync(sha, false);
        await _eventEmitter.EmitAsync(PluginEventType.ModUnloaded, data: new { Sha = sha });

        return true;
    }

    public async Task<List<string>> GetLoadedModIdsAsync()
    {
        return await _repository.GetLoadedIdsAsync();
    }

    public async Task<ModInfo?> ImportModAsync(string filePath)
    {
        var mod = await _importService.ImportAsync(filePath);

        if (mod != null)
        {
            await _eventEmitter.EmitAsync(PluginEventType.ModImported, data: mod);
        }

        return mod;
    }

    public async Task<bool> DeleteModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null) return false;

        // Preview folder (previews/{sha}/) is handled by ClearModCacheAsync in DeleteAsync
        await _fileService.DeleteAsync(sha, mod.ThumbnailPath, null);
        var success = await _repository.DeleteAsync(sha);

        if (success)
        {
            await _eventEmitter.EmitAsync(PluginEventType.ModDeleted, data: new { Sha = sha, Mod = mod });
        }

        return success;
    }

    public async Task<List<ModInfo>> GetModsByObjectAsync(string category)
    {
        var mods = await _repository.GetByCategoryAsync(category);
        return mods;
    }

    public async Task<List<string>> GetObjectNamesAsync()
    {
        return await _repository.GetDistinctCategoriesAsync();
    }

    public async Task<List<string>> GetAuthorsAsync()
    {
        return await _repository.GetDistinctAuthorsAsync();
    }

    public async Task<List<string>> GetTagsAsync()
    {
        return await _repository.GetAllTagsAsync();
    }

    public async Task<List<ModInfo>> SearchModsAsync(string searchTerm)
    {
        var mods = await _queryService.SearchAsync(searchTerm);
        return mods;
    }

    public async Task<ModStatistics> GetStatisticsAsync()
    {
        return await _queryService.GetStatisticsAsync();
    }

    public async Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        if (name != null) mod.Name = name;
        if (author != null) mod.Author = author;
        if (tags != null) mod.Tags = tags;
        if (grading != null) mod.Grading = grading;
        if (description != null) mod.Description = description;

        await _repository.UpdateAsync(mod);
        await _eventEmitter.EmitAsync(PluginEventType.CustomEvent, "mod.metadata.updated", new { sha, mod });

        return true;
    }

    public async Task<bool> UpdateCategoryAsync(string sha, string category)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // If the mod is currently loaded, unload it since category determines which object it applies to
        if (mod.IsLoaded)
        {
            await UnloadModAsync(sha);
        }

        mod.Category = category;

        await _repository.UpdateAsync(mod);
        await _eventEmitter.EmitAsync(PluginEventType.CustomEvent, "mod.category.updated", new { sha, category, mod });

        // Refresh the classification tree cache to update counts
        await _classificationService.RefreshTreeAsync();

        return true;
    }

    public async Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask)
    {
        int updatedCount = 0;

        foreach (var sha in shas)
        {
            try
            {
                var mod = await _repository.GetByIdAsync(sha);
                if (mod == null) continue;

                if (fieldMask.Contains("name") && name != null) mod.Name = name;
                if (fieldMask.Contains("author") && author != null) mod.Author = author;
                if (fieldMask.Contains("tags") && tags != null) mod.Tags = tags;
                if (fieldMask.Contains("grading") && grading != null) mod.Grading = grading;
                if (fieldMask.Contains("description") && description != null) mod.Description = description;

                await _repository.UpdateAsync(mod);
                updatedCount++;

                await _eventEmitter.EmitAsync(PluginEventType.CustomEvent, "mod.metadata.updated", new { sha, mod });
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

        var mod = await _repository.GetByIdAsync(sha);
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

        string? targetDirectory = mod.WorkPath ?? mod.CachePath;
        if (string.IsNullOrEmpty(targetDirectory))
        {
            throw new InvalidOperationException($"Mod has no work or cache directory: {sha}");
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var targetFileName = $"preview{extension}";
        var targetPath = Path.Combine(targetDirectory, targetFileName);

        File.Copy(imagePath, targetPath, overwrite: true);

        // Note: Previews are now stored in previews/{SHA}/ folder and scanned dynamically
        // If no thumbnail exists, use this as thumbnail
        if (string.IsNullOrEmpty(mod.ThumbnailPath))
        {
            mod.ThumbnailPath = targetPath;
        }

        await _repository.UpdateAsync(mod);
        await _eventEmitter.EmitAsync(PluginEventType.CustomEvent, "mod.preview.imported", new { sha, imagePath = targetPath });

        return true;
    }

    public async Task<List<string>> GetPreviewPathsAsync(string sha)
    {
        return await _imageService.GetPreviewPathsAsync(sha);
    }

    // ============= Message Handler Methods =============

    private async Task<ModInfo?> GetModByIdAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await GetModByIdAsync(sha);
    }

    private async Task<bool> LoadModAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await LoadModAsync(sha);
    }

    private async Task<bool> UnloadModAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await UnloadModAsync(sha);
    }

    private async Task<ModInfo?> ImportModAsync(MessageRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        return await ImportModAsync(filePath);
    }

    private async Task<bool> DeleteModAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await DeleteModAsync(sha);
    }

    private async Task<List<ModInfo>> GetModsByObjectAsync(MessageRequest request)
    {
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");
        return await GetModsByObjectAsync(category);
    }

    private async Task<List<ModInfo>> SearchModsAsync(MessageRequest request)
    {
        var searchTerm = _payloadHelper.GetRequiredValue<string>(request.Payload, "searchTerm");
        return await SearchModsAsync(searchTerm);
    }

    private async Task<bool> UpdateMetadataAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");

        return await UpdateMetadataAsync(sha, name, author, tags, grading, description);
    }

    private async Task<bool> UpdateCategoryAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var category = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        return await UpdateCategoryAsync(sha, category);
    }

    private async Task<object> BatchUpdateMetadataAsync(MessageRequest request)
    {
        var shas = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "shas");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = _payloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = _payloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var fieldMask = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "fieldMask");

        var updatedCount = await BatchUpdateMetadataAsync(shas, name, author, tags, grading, description, fieldMask);

        return new { updatedCount, totalRequested = shas.Count };
    }

    private async Task<object> ImportPreviewImageAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var imagePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "imagePath");

        var success = await ImportPreviewImageAsync(sha, imagePath);

        return new { success, message = $"Preview image imported for mod: {sha}" };
    }

    private async Task<List<string>> GetPreviewPathsAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await GetPreviewPathsAsync(sha);
    }

    /// <summary>
    /// Get the classification tree from SQLite database
    /// Returns hierarchical tree structure with thumbnails
    /// </summary>
    public async Task<List<ClassificationNode>> GetClassificationTreeAsync()
    {
        return await _classificationService.GetClassificationTreeAsync();
    }

    /// <summary>
    /// Refresh the classification tree cache
    /// Forces a reload of the classification tree from the database
    /// </summary>
    public async Task<bool> RefreshClassificationTreeAsync()
    {
        return await _classificationService.RefreshTreeAsync();
    }

    /// <summary>
    /// Get all mods that belong to a specific classification node
    /// </summary>
    private async Task<List<ModInfo>> GetModsByClassificationAsync(MessageRequest request)
    {
        var classificationNodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "classificationNodeId");
        var mods = await _queryService.GetModsByClassificationAsync(classificationNodeId);
        return mods;
    }

    /// <summary>
    /// Get all mods that don't have any classification tags
    /// </summary>
    private async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var mods = await _queryService.GetUnclassifiedModsAsync();
        return mods;
    }

    /// <summary>
    /// Get count of mods that don't have any classification tags
    /// </summary>
    private async Task<int> GetUnclassifiedCountAsync()
    {
        return await _queryService.GetUnclassifiedCountAsync();
    }

    /// <summary>
    /// Move a classification node to a new parent
    /// </summary>
    private async Task<bool> MoveClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newParentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "newParentId");
        var dropPosition = _payloadHelper.GetOptionalValue<int?>(request.Payload, "dropPosition");

        var success = await _classificationService.MoveNodeAsync(nodeId, newParentId, dropPosition);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                PluginEventType.ClassificationTreeChanged,
                data: new { nodeId, newParentId, dropPosition });
        }

        return success;
    }

    /// <summary>
    /// Reorder a classification node within its siblings
    /// </summary>
    private async Task<bool> ReorderClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newPosition = _payloadHelper.GetRequiredValue<int>(request.Payload, "newPosition");

        var success = await _classificationService.ReorderNodeAsync(nodeId, newPosition);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                PluginEventType.ClassificationTreeChanged,
                data: new { nodeId, newPosition });
        }

        return success;
    }

    /// <summary>
    /// Update a classification node's name and icon
    /// </summary>
    private async Task<bool> UpdateClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var icon = _payloadHelper.GetOptionalValue<string>(request.Payload, "icon");

        var success = await _classificationService.UpdateNodeAsync(nodeId, name, icon);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                PluginEventType.ClassificationTreeChanged,
                data: new { nodeId, name, icon });
        }

        return success;
    }

    /// <summary>
    /// Delete a classification node and all its children
    /// </summary>
    private async Task<bool> DeleteClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = _payloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");

        var success = await _classificationService.DeleteNodeAsync(nodeId);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                PluginEventType.ClassificationTreeChanged,
                data: new { nodeId, deleted = true });
        }

        return success;
    }
}
