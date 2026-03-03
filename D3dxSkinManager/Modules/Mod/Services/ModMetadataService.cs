using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for mod metadata operations
/// Handles updating mod metadata fields
/// </summary>
public interface IModMetadataService
{
    Task<bool> UpdateMetadataAsync(string sha, UpdateModMetadataRequest request);
    Task<bool> UpdateCategoryAsync(string sha, string category, Func<string, Task<bool>> unloadModFunc, Func<string, Task<ModInfo?>> getModFunc);
    Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask);
}

public class ModMetadataService : IModMetadataService
{
    private readonly IModRepository _repository;
    private readonly ILogHelper _logger;

    public ModMetadataService(IModRepository repository, ILogHelper logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> UpdateMetadataAsync(string sha, UpdateModMetadataRequest request)
    {
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        if (request.Name != null) mod.Name = request.Name;
        if (request.Author != null) mod.Author = request.Author;
        if (request.Tags != null) mod.Tags = request.Tags;
        if (request.Grading != null) mod.Grading = request.Grading;
        if (request.Description != null) mod.Description = request.Description;
        if (request.DisablePreview != null) mod.DisablePreview = request.DisablePreview.Value;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> UpdateCategoryAsync(string sha, string category, Func<string, Task<bool>> unloadModFunc, Func<string, Task<ModInfo?>> getModFunc)
    {
        var mod = await getModFunc(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        _logger.Info($"Mod {sha} current state: IsLoaded={mod.IsLoaded}", "ModMetadataService");

        // If the mod is currently loaded, unload it since category determines which object it applies to
        if (mod.IsLoaded)
        {
            _logger.Info($"Mod {sha} is loaded, unloading before category change", "ModMetadataService");
            await unloadModFunc(sha).ConfigureAwait(false);
            _logger.Info($"Mod {sha} unloaded", "ModMetadataService");
        }
        else
        {
            _logger.Info($"Mod {sha} is not loaded, skipping unload", "ModMetadataService");
        }

        mod.Category = category;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);

        // Note: CategoryEventHandler subscribes to MOD_CATEGORY_UPDATED event
        // and will invalidate the category tree cache automatically

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
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating mod {sha}: {ex.Message}", "ModMetadataService", ex);
            }
        }

        return updatedCount;
    }
}
