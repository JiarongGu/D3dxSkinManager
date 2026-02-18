using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Facade for coordinating mod-related operations
/// Responsibility: Mod management and metadata operations
/// IPC Prefix: MOD_*
/// </summary>
public class ModFacade : IModFacade
{
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModImportService _importService;
    private readonly IModQueryService _queryService;
    private readonly IClassificationService _classificationService;
    private readonly ImageServerService _imageServer;
    private readonly PluginEventBus? _eventBus;

    public ModFacade(
        IModRepository repository,
        IModArchiveService archiveService,
        IModImportService importService,
        IModQueryService queryService,
        IClassificationService classificationService,
        ImageServerService imageServer,
        PluginEventBus? eventBus = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
        _imageServer = imageServer ?? throw new ArgumentNullException(nameof(imageServer));
        _eventBus = eventBus;
    }

    /// <summary>
    /// Routes incoming IPC messages to appropriate handler methods
    /// </summary>
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[ModFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
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
                "BATCH_UPDATE_METADATA" => await BatchUpdateMetadataAsync(request),
                "IMPORT_PREVIEW_IMAGE" => await ImportPreviewImageAsync(request),
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

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    // ============= Public API Methods =============

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        var mods = await _repository.GetAllAsync();
        // Convert file paths to data URIs for web rendering
        foreach (var mod in mods)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }
        return mods;
    }

    public async Task<ModInfo?> GetModByIdAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod != null)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }
        return mod;
    }

    /// <summary>
    /// Converts file paths to HTTP URLs for web rendering
    /// This solves browser security restrictions on loading local files
    /// </summary>
    private Task ConvertImagePathsToDataUrisAsync(ModInfo mod)
    {
        // Convert thumbnail path to HTTP URL using centralized method
        mod.ThumbnailPath = _imageServer.ConvertPathToUrl(mod.ThumbnailPath);

        // Convert preview path to HTTP URL using centralized method
        mod.PreviewPath = _imageServer.ConvertPathToUrl(mod.PreviewPath);

        return Task.CompletedTask;
    }

    public async Task<bool> LoadModAsync(string sha)
    {
        var success = await _archiveService.LoadAsync(sha);
        if (!success) return false;

        await _repository.SetLoadedStateAsync(sha, true);

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ModLoaded,
                Data = new { Sha = sha }
            });
        }

        return true;
    }

    public async Task<bool> UnloadModAsync(string sha)
    {
        var success = await _archiveService.UnloadAsync(sha);
        if (!success) return false;

        await _repository.SetLoadedStateAsync(sha, false);

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ModUnloaded,
                Data = new { Sha = sha }
            });
        }

        return true;
    }

    public async Task<List<string>> GetLoadedModIdsAsync()
    {
        return await _repository.GetLoadedIdsAsync();
    }

    public async Task<ModInfo?> ImportModAsync(string filePath)
    {
        var mod = await _importService.ImportAsync(filePath);

        if (mod != null && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ModImported,
                Data = mod
            });
        }

        return mod;
    }

    public async Task<bool> DeleteModAsync(string sha)
    {
        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null) return false;

        await _archiveService.DeleteAsync(sha, mod.ThumbnailPath, mod.PreviewPath);
        var success = await _repository.DeleteAsync(sha);

        if (success && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ModDeleted,
                Data = new { Sha = sha, Mod = mod }
            });
        }

        return success;
    }

    public async Task<List<ModInfo>> GetModsByObjectAsync(string category)
    {
        var mods = await _repository.GetByCategoryAsync(category);
        foreach (var mod in mods)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }
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
        foreach (var mod in mods)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }
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

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "mod.metadata.updated",
                Data = new { sha, mod }
            });
        }

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

                if (_eventBus != null)
                {
                    await _eventBus.EmitAsync(new PluginEventArgs
                    {
                        EventType = PluginEventType.CustomEvent,
                        EventName = "mod.metadata.updated",
                        Data = new { sha, mod }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating mod {sha}: {ex.Message}");
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

        mod.PreviewPath = targetPath;
        if (string.IsNullOrEmpty(mod.ThumbnailPath))
        {
            mod.ThumbnailPath = targetPath;
        }

        await _repository.UpdateAsync(mod);

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "mod.preview.imported",
                Data = new { sha, imagePath = targetPath }
            });
        }

        return true;
    }

    // ============= Message Handler Methods =============

    private async Task<ModInfo?> GetModByIdAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await GetModByIdAsync(sha);
    }

    private async Task<bool> LoadModAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await LoadModAsync(sha);
    }

    private async Task<bool> UnloadModAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await UnloadModAsync(sha);
    }

    private async Task<ModInfo?> ImportModAsync(MessageRequest request)
    {
        var filePath = PayloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        return await ImportModAsync(filePath);
    }

    private async Task<bool> DeleteModAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await DeleteModAsync(sha);
    }

    private async Task<List<ModInfo>> GetModsByObjectAsync(MessageRequest request)
    {
        var category = PayloadHelper.GetRequiredValue<string>(request.Payload, "category");
        return await GetModsByObjectAsync(category);
    }

    private async Task<List<ModInfo>> SearchModsAsync(MessageRequest request)
    {
        var searchTerm = PayloadHelper.GetRequiredValue<string>(request.Payload, "searchTerm");
        return await SearchModsAsync(searchTerm);
    }

    private async Task<bool> UpdateMetadataAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var name = PayloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = PayloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = PayloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = PayloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = PayloadHelper.GetOptionalValue<string>(request.Payload, "description");

        return await UpdateMetadataAsync(sha, name, author, tags, grading, description);
    }

    private async Task<object> BatchUpdateMetadataAsync(MessageRequest request)
    {
        var shas = PayloadHelper.GetRequiredValue<List<string>>(request.Payload, "shas");
        var name = PayloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var author = PayloadHelper.GetOptionalValue<string>(request.Payload, "author");
        var tags = PayloadHelper.GetOptionalValue<List<string>>(request.Payload, "tags");
        var grading = PayloadHelper.GetOptionalValue<string>(request.Payload, "grading");
        var description = PayloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var fieldMask = PayloadHelper.GetRequiredValue<List<string>>(request.Payload, "fieldMask");

        var updatedCount = await BatchUpdateMetadataAsync(shas, name, author, tags, grading, description, fieldMask);

        return new { updatedCount, totalRequested = shas.Count };
    }

    private async Task<object> ImportPreviewImageAsync(MessageRequest request)
    {
        var sha = PayloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        var imagePath = PayloadHelper.GetRequiredValue<string>(request.Payload, "imagePath");

        var success = await ImportPreviewImageAsync(sha, imagePath);

        return new { success, message = $"Preview image imported for mod: {sha}" };
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
        var classificationNodeId = PayloadHelper.GetRequiredValue<string>(request.Payload, "classificationNodeId");
        var mods = await _queryService.GetModsByClassificationAsync(classificationNodeId);

        // Convert file paths to HTTP URLs for web rendering
        foreach (var mod in mods)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }

        return mods;
    }

    /// <summary>
    /// Get all mods that don't have any classification tags
    /// </summary>
    private async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var mods = await _queryService.GetUnclassifiedModsAsync();

        // Convert file paths to HTTP URLs for web rendering
        foreach (var mod in mods)
        {
            await ConvertImagePathsToDataUrisAsync(mod);
        }

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
        var nodeId = PayloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newParentId = PayloadHelper.GetOptionalValue<string>(request.Payload, "newParentId");
        var dropPosition = PayloadHelper.GetOptionalValue<int?>(request.Payload, "dropPosition");

        var success = await _classificationService.MoveNodeAsync(nodeId, newParentId, dropPosition);

        if (success && _eventBus != null)
        {
            // Emit event to notify frontend that tree changed
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ClassificationTreeChanged,
                Data = new { nodeId, newParentId, dropPosition }
            });
        }

        return success;
    }

    /// <summary>
    /// Reorder a classification node within its siblings
    /// </summary>
    private async Task<bool> ReorderClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = PayloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var newPosition = PayloadHelper.GetRequiredValue<int>(request.Payload, "newPosition");

        var success = await _classificationService.ReorderNodeAsync(nodeId, newPosition);

        if (success && _eventBus != null)
        {
            // Emit event to notify frontend that tree changed
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ClassificationTreeChanged,
                Data = new { nodeId, newPosition }
            });
        }

        return success;
    }

    /// <summary>
    /// Update a classification node's name and icon
    /// </summary>
    private async Task<bool> UpdateClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = PayloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");
        var name = PayloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var icon = PayloadHelper.GetOptionalValue<string>(request.Payload, "icon");

        var success = await _classificationService.UpdateNodeAsync(nodeId, name, icon);

        if (success && _eventBus != null)
        {
            // Emit event to notify frontend that tree changed
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ClassificationTreeChanged,
                Data = new { nodeId, name, icon }
            });
        }

        return success;
    }

    /// <summary>
    /// Delete a classification node and all its children
    /// </summary>
    private async Task<bool> DeleteClassificationNodeAsync(MessageRequest request)
    {
        var nodeId = PayloadHelper.GetRequiredValue<string>(request.Payload, "nodeId");

        var success = await _classificationService.DeleteNodeAsync(nodeId);

        if (success && _eventBus != null)
        {
            // Emit event to notify frontend that tree changed
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ClassificationTreeChanged,
                Data = new { nodeId, deleted = true }
            });
        }

        return success;
    }
}
