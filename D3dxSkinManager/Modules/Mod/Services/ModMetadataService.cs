using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod metadata and management operations
/// Consolidated service for create, update, delete, and category operations
/// </summary>
public interface IModMetadataService
{
    // Create/Delete operations (from ModManagementService)
    Task<ModInfo> CreateAsync(CreateModRequest request);
    Task<bool> DeleteAsync(string sha);
    Task<ModInfo?> GetOrCreateAsync(string sha, CreateModRequest request);

    // Update metadata operations (from original ModMetadataService)
    Task<ModInfo> UpdateAsync(string sha, UpdateModMetadataRequest request);
    Task<int> BatchUpdateAsync(List<string> shas, UpdateModMetadataRequest request, List<string> fieldMask);

    // Update category operations (refactored - no callbacks)
    Task<ModInfo> UpdateCategoryAsync(string sha, string category);
    Task<int> BatchUpdateCategoryAsync(List<string> shas, string category);
}

/// <summary>
/// Request model for creating a new mod
/// </summary>
public class CreateModRequest
{
    public required string SHA { get; set; }
    public required string? Category { get; set; }
    public required string Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = "zip";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    // Note: IsLoaded and IsAvailable are determined dynamically from file system
    // Note: Preview paths and thumbnails are dynamically scanned from previews/{SHA}/ folder
}

/// <summary>
/// Consolidated service for mod metadata and management operations
/// Handles create, update, delete, and category change operations
/// Emits appropriate events for all operations (METADATA_UPDATED, CATEGORY_UPDATED)
/// </summary>
public class ModMetadataService : IModMetadataService
{
    private readonly IModRepository _repository;
    private readonly IModLifecycleService _lifecycleService;
    private readonly IModQueryService _queryService;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModMetadataService(
        IModRepository repository,
        IModLifecycleService lifecycleService,
        IModQueryService queryService,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _repository = repository;
        _lifecycleService = lifecycleService;
        _queryService = queryService;
        _logger = logger;
        _eventBus = eventBus;
    }

    #region Create/Delete Operations

    /// <summary>
    /// Create a new mod with validation and default values
    /// </summary>
    public async Task<ModInfo> CreateAsync(CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SHA))
            throw new ArgumentException("SHA is required", nameof(request.SHA));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request.Name));

        // Check if already exists
        if (await _repository.ExistsAsync(request.SHA))
        {
            throw new InvalidOperationException($"Mod with SHA {request.SHA} already exists");
        }

        var mod = new ModInfo
        {
            SHA = request.SHA,
            Category = request.Category ?? string.Empty,
            Name = request.Name,
            Author = request.Author ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Type = request.Type,
            Grading = request.Grading,
            Tags = request.Tags ?? new List<string>()
            // Note: IsLoaded, IsAvailable, preview paths, and thumbnails are populated dynamically from file system
        };

        await _repository.InsertAsync(mod).ConfigureAwait(false);
        _logger.Verbose($"Created mod: {mod.Name} ({mod.SHA})", "ModMetadataService");

        return mod;
    }

    /// <summary>
    /// Delete a mod by SHA
    /// </summary>
    public async Task<bool> DeleteAsync(string sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        var exists = await _repository.ExistsAsync(sha).ConfigureAwait(false);
        if (!exists)
        {
            _logger.Warn($"Mod not found for deletion: {sha}", "ModMetadataService");
            return false;
        }

        var success = await _repository.DeleteAsync(sha).ConfigureAwait(false);
        if (success)
        {
            _logger.Info($"Deleted mod: {sha}", "ModMetadataService");

            // Emit DELETED event to notify frontend
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { Sha = sha }).ConfigureAwait(false);
        }

        return success;
    }

    /// <summary>
    /// Get existing mod or create if it doesn't exist
    /// Useful for idempotent operations like migration
    /// </summary>
    public async Task<ModInfo?> GetOrCreateAsync(string sha, CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        // Try to get existing mod
        var existing = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (existing != null)
        {
            _logger.Debug($"Mod already exists: {existing.Name} ({sha})", "ModMetadataService");
            return existing;
        }

        // Create new mod
        return await CreateAsync(request).ConfigureAwait(false);
    }

    #endregion

    #region Update Metadata Operations

    /// <summary>
    /// Update mod metadata with partial updates (only specified fields)
    /// Emits METADATA_UPDATED event on success
    /// </summary>
    public async Task<ModInfo> UpdateAsync(string sha, UpdateModMetadataRequest request)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        // Apply updates only for non-null values
        if (request.Name != null) mod.Name = request.Name;
        if (request.Author != null) mod.Author = request.Author;
        if (request.Tags != null) mod.Tags = request.Tags;
        if (request.Grading != null) mod.Grading = request.Grading;
        if (request.Description != null) mod.Description = request.Description;
        if (request.DisablePreview != null) mod.DisablePreview = request.DisablePreview.Value;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);

        // Emit METADATA_UPDATED event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.METADATA_UPDATED, new { sha, mod }).ConfigureAwait(false);

        _logger.Info($"Updated metadata for mod: {mod.Name} ({sha})", "ModMetadataService");

        return mod;
    }

    /// <summary>
    /// Batch update metadata for multiple mods
    /// Uses fieldMask to determine which fields to update
    /// Emits METADATA_UPDATED event for each successfully updated mod
    /// </summary>
    public async Task<int> BatchUpdateAsync(List<string> shas, UpdateModMetadataRequest request, List<string> fieldMask)
    {
        int updatedCount = 0;

        foreach (var sha in shas)
        {
            try
            {
                var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
                if (mod == null) continue;

                // Apply updates based on fieldMask
                if (fieldMask.Contains("name") && request.Name != null) mod.Name = request.Name;
                if (fieldMask.Contains("author") && request.Author != null) mod.Author = request.Author;
                if (fieldMask.Contains("tags") && request.Tags != null) mod.Tags = request.Tags;
                if (fieldMask.Contains("grading") && request.Grading != null) mod.Grading = request.Grading;
                if (fieldMask.Contains("description") && request.Description != null) mod.Description = request.Description;
                if (fieldMask.Contains("disablePreview") && request.DisablePreview != null) mod.DisablePreview = request.DisablePreview.Value;

                await _repository.UpdateAsync(mod).ConfigureAwait(false);

                // Emit METADATA_UPDATED event for each mod
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.METADATA_UPDATED, new { sha, mod }).ConfigureAwait(false);

                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating mod {sha}: {ex.Message}", "ModMetadataService", ex);
            }
        }

        _logger.Info($"Batch updated metadata for {updatedCount} out of {shas.Count} mods", "ModMetadataService");

        return updatedCount;
    }

    #endregion

    #region Update Category Operations

    /// <summary>
    /// Update mod category
    /// If mod is loaded, unloads it first since category determines which object it applies to
    /// Emits CATEGORY_UPDATED event on success
    /// </summary>
    public async Task<ModInfo> UpdateCategoryAsync(string sha, string category)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod not found: {sha}");
        }

        _logger.Info($"Mod {sha} current state: IsLoaded={mod.IsLoaded}", "ModMetadataService");

        // If the mod is currently loaded, unload it since category determines which object it applies to
        if (mod.IsLoaded)
        {
            _logger.Info($"Mod {sha} is loaded, unloading before category change", "ModMetadataService");
            await _lifecycleService.UnloadAsync(sha).ConfigureAwait(false);
            _logger.Info($"Mod {sha} unloaded", "ModMetadataService");
        }
        else
        {
            _logger.Info($"Mod {sha} is not loaded, skipping unload", "ModMetadataService");
        }

        mod.Category = category;

        await _repository.UpdateAsync(mod).ConfigureAwait(false);

        // Re-fetch the mod to get the updated IsLoaded state from file system
        var updatedMod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);

        // Emit CATEGORY_UPDATED event
        // Note: CategoryEventHandler subscribes to this event and invalidates the category tree cache
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CATEGORY_UPDATED, new { sha, category, mod = updatedMod }).ConfigureAwait(false);

        return updatedMod!;
    }

    /// <summary>
    /// Batch update category for multiple mods
    /// Unloads any loaded mods before category change
    /// Emits single CATEGORY_UPDATED event for the batch operation
    /// </summary>
    public async Task<int> BatchUpdateCategoryAsync(List<string> shas, string category)
    {
        int updatedCount = 0;
        _logger.Info($"Batch updating category for {shas.Count} mods to category '{category}'", "ModMetadataService");

        foreach (var sha in shas)
        {
            try
            {
                var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
                if (mod == null)
                {
                    _logger.Verbose($"Mod {sha} not found, skipping", "ModMetadataService");
                    continue;
                }

                // If the mod is currently loaded, unload it since category determines which object it applies to
                if (mod.IsLoaded)
                {
                    _logger.Verbose($"Unloading mod {sha} before category change", "ModMetadataService");
                    await _lifecycleService.UnloadAsync(sha).ConfigureAwait(false);
                }

                mod.Category = category;
                await _repository.UpdateAsync(mod).ConfigureAwait(false);
                updatedCount++;
                _logger.Verbose($"Updated category for mod {sha}", "ModMetadataService");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating category for mod {sha}: {ex.Message}", "ModMetadataService", ex);
            }
        }

        _logger.Info($"Successfully updated category for {updatedCount} out of {shas.Count} mods", "ModMetadataService");

        // Emit single CATEGORY_UPDATED event for batch operation
        if (updatedCount > 0)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CATEGORY_UPDATED, new { shas, category, count = updatedCount }).ConfigureAwait(false);
        }

        return updatedCount;
    }

    #endregion
}
