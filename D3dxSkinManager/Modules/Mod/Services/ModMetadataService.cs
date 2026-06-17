using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod metadata and management operations
/// Consolidated service for create, update, delete, and category operations
/// </summary>
public interface IModMetadataService
{
    // Create/Delete operations (from ModManagementService)
    Task<ModInfo> CreateAsync(CreateModRequest request);
    Task<bool> DeleteAsync(string id);
    Task<ModInfo?> GetOrCreateAsync(string id, CreateModRequest request);

    // Update metadata operations (from original ModMetadataService)
    Task<ModInfo> UpdateAsync(string id, UpdateModMetadataRequest request);
    Task<int> BatchUpdateAsync(Dictionary<string, UpdateModMetadataRequest> updates);

    // Update category operations (refactored - no callbacks)
    Task<ModInfo> UpdateCategoryAsync(string id, string category);
    Task<int> BatchUpdateCategoryAsync(Dictionary<string, string> updates);
}

/// <summary>
/// Request model for creating a new mod
/// </summary>
public class CreateModRequest
{
    public required string Id { get; set; }
    public required string? Category { get; set; }
    public required string Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = "zip";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    // Note: IsLoaded and IsAvailable are determined dynamically from file system
    // Note: Preview paths and thumbnails are dynamically scanned from previews/{Id}/ folder
}

/// <summary>
/// Consolidated service for mod metadata and management operations
/// Handles create, update, delete, and category change operations
/// Emits appropriate events for all operations (METADATA_UPDATED, CATEGORY_UPDATED)
/// </summary>
public class ModMetadataService : IModMetadataService
{
    private readonly IModRepository _repository;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IModLifecycleService _lifecycleService;
    private readonly IModDeletionService _deletionService;
    private readonly IModQueryService _queryService;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;
    private readonly Core.Services.IProcessRegistry _processRegistry;

    public ModMetadataService(
        IModRepository repository,
        IModEnrichmentService enrichmentService,
        IModLifecycleService lifecycleService,
        IModDeletionService deletionService,
        IModQueryService queryService,
        ILogHelper logger,
        IProfileEventBus eventBus,
        Core.Services.IProcessRegistry processRegistry)
    {
        _repository = repository;
        _enrichmentService = enrichmentService;
        _lifecycleService = lifecycleService;
        _deletionService = deletionService;
        _queryService = queryService;
        _logger = logger;
        _eventBus = eventBus;
        _processRegistry = processRegistry;
    }

    #region Create/Delete Operations

    /// <summary>
    /// Create a new mod with validation and default values
    /// </summary>
    public async Task<ModInfo> CreateAsync(CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Id is required", nameof(request.Id));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request.Name));

        // Check if already exists
        if (await _repository.ExistsAsync(request.Id))
        {
            throw new InvalidOperationException($"Mod with Id {request.Id} already exists");
        }

        var mod = new ModInfo
        {
            Id = request.Id,
            Category = request.Category ?? string.Empty,
            Name = request.Name,
            Author = request.Author ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Type = request.Type,
            Grading = request.Grading,
            Tags = request.Tags ?? new List<string>()
            // Note: IsLoaded, IsAvailable, preview paths, and thumbnails are populated dynamically from file system
        };

        // Convert to entity for database insertion
        var entity = ModMapper.ToEntity(mod);
        await _repository.InsertAsync(entity).ConfigureAwait(false);
        _logger.Verbose($"Created mod: {mod.Name} ({mod.Id})", "ModMetadataService");

        return mod;
    }

    /// <summary>
    /// Delete a mod by Id
    /// Delegates to ModDeletionService which orchestrates the complete deletion workflow
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        return await _deletionService.DeleteAsync(id).ConfigureAwait(false);
    }

    /// <summary>
    /// Get existing mod or create if it doesn't exist
    /// Useful for idempotent operations like migration
    /// </summary>
    public async Task<ModInfo?> GetOrCreateAsync(string id, CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id is required", nameof(id));

        // Try to get existing mod
        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity != null)
        {
            var existing = ModMapper.ToDomain(entity);
            _logger.Debug($"Mod already exists: {existing.Name} ({id})", "ModMetadataService");
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
    public async Task<ModInfo> UpdateAsync(string id, UpdateModMetadataRequest request)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id is required", nameof(id));

        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Convert to domain model
        var mod = ModMapper.ToDomain(entity);

        // Apply updates only for non-null values
        if (request.Name != null) mod.Name = request.Name;
        if (request.Author != null) mod.Author = request.Author;
        if (request.Tags != null) mod.Tags = request.Tags;
        if (request.Grading != null) mod.Grading = request.Grading;
        if (request.Description != null) mod.Description = request.Description;
        if (request.DisablePreview != null) mod.DisablePreview = request.DisablePreview.Value;

        // Convert back to entity and update
        var updatedEntity = ModMapper.ToEntity(mod);
        await _repository.UpdateAsync(updatedEntity).ConfigureAwait(false);

        // Emit METADATA_UPDATED event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.METADATA_UPDATED, new { id, mod }).ConfigureAwait(false);

        _logger.Info($"Updated metadata for mod: {mod.Name} ({id})", "ModMetadataService");

        return mod;
    }

    /// <summary>
    /// Batch update metadata for multiple mods with individual values for each mod
    /// Each mod can have its own specific metadata values
    /// Emits METADATA_UPDATED event for each successfully updated mod
    /// </summary>
    public async Task<int> BatchUpdateAsync(Dictionary<string, UpdateModMetadataRequest> updates)
    {
        int updatedCount = 0;

        foreach (var (id, request) in updates)
        {
            // Yield to prevent UI blocking during long operations
            await Task.Yield();

            try
            {
                var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null) continue;

                // Convert to domain model
                var mod = ModMapper.ToDomain(entity);

                // Apply updates - only update non-null fields
                if (request.Name != null) mod.Name = request.Name;
                if (request.Author != null) mod.Author = request.Author;
                if (request.Tags != null) mod.Tags = request.Tags;
                if (request.Grading != null) mod.Grading = request.Grading;
                if (request.Description != null) mod.Description = request.Description;
                if (request.DisablePreview != null) mod.DisablePreview = request.DisablePreview.Value;

                // Convert back to entity and update
                var updatedEntity = ModMapper.ToEntity(mod);
                await _repository.UpdateAsync(updatedEntity).ConfigureAwait(false);

                // Emit METADATA_UPDATED event for each mod
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.METADATA_UPDATED, new { id, mod }).ConfigureAwait(false);

                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating mod {id}: {ex.Message}", "ModMetadataService", ex);
            }
        }

        _logger.Info($"Batch updated metadata for {updatedCount} out of {updates.Count} mods", "ModMetadataService");

        return updatedCount;
    }

    #endregion

    #region Update Category Operations

    /// <summary>
    /// Update mod category
    /// If mod is loaded, unloads it first since category determines which object it applies to
    /// Emits CATEGORY_UPDATED event on success
    /// </summary>
    public async Task<ModInfo> UpdateCategoryAsync(string id, string category)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id is required", nameof(id));

        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
        {
            throw new InvalidOperationException($"Mod not found: {id}");
        }

        // Convert to domain model and enrich to populate IsLoaded flag
        var mod = ModMapper.ToDomain(entity);
        _enrichmentService.PopulateStatusFlags(new List<ModInfo> { mod });

        _logger.Info($"Mod {id} current state: IsLoaded={mod.IsLoaded}", "ModMetadataService");

        // If the mod is currently loaded, unload it since category determines which object it applies to
        if (mod.IsLoaded)
        {
            _logger.Info($"Mod {id} is loaded, unloading before category change", "ModMetadataService");
            await _lifecycleService.UnloadAsync(id).ConfigureAwait(false);
            _logger.Info($"Mod {id} unloaded", "ModMetadataService");
        }
        else
        {
            _logger.Info($"Mod {id} is not loaded, skipping unload", "ModMetadataService");
        }

        mod.Category = category;

        // Convert to entity and update
        var updatedEntity = ModMapper.ToEntity(mod);
        await _repository.UpdateAsync(updatedEntity).ConfigureAwait(false);

        // Re-fetch the mod to get the updated IsLoaded state from file system
        var refetchedEntity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        var updatedMod = ModMapper.ToDomain(refetchedEntity!);
        _enrichmentService.PopulateStatusFlags(new List<ModInfo> { updatedMod });

        // Emit CATEGORY_UPDATED event
        // Note: CategoryEventHandler subscribes to this event and invalidates the category tree cache
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CATEGORY_UPDATED, new { id, category, mod = updatedMod }).ConfigureAwait(false);

        return updatedMod;
    }

    /// <summary>
    /// Batch update category for multiple mods with individual values for each mod
    /// Unloads any loaded mods before category change
    /// Emits single CATEGORY_UPDATED event for the batch operation
    /// </summary>
    public async Task<int> BatchUpdateCategoryAsync(Dictionary<string, string> updates)
    {
        int updatedCount = 0;
        _logger.Info($"Batch updating category for {updates.Count} mods with individual categories", "ModMetadataService");

        var procId = _processRegistry.Start(Core.Models.ProcessType.BatchUpdate, $"Updating category for {updates.Count} mods");
        var total = updates.Count;
        var processed = 0;
        foreach (var (id, category) in updates)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    _logger.Verbose($"Mod {id} not found, skipping", "ModMetadataService");
                    continue;
                }

                // Convert to domain model and enrich to populate IsLoaded flag
                var mod = ModMapper.ToDomain(entity);
                _enrichmentService.PopulateStatusFlags(new List<ModInfo> { mod });

                // If the mod is currently loaded, unload it since category determines which object it applies to
                if (mod.IsLoaded)
                {
                    _logger.Verbose($"Unloading mod {id} before category change", "ModMetadataService");
                    await _lifecycleService.UnloadAsync(id).ConfigureAwait(false);
                }

                mod.Category = category;

                // Convert to entity and update
                var updatedEntity = ModMapper.ToEntity(mod);
                await _repository.UpdateAsync(updatedEntity).ConfigureAwait(false);
                updatedCount++;
                _logger.Verbose($"Updated category for mod {id} to '{category}'", "ModMetadataService");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating category for mod {id}: {ex.Message}", "ModMetadataService", ex);
            }
            processed++;
            _processRegistry.Report(procId, total > 0 ? (int)(processed * 100.0 / total) : null);
        }

        _logger.Info($"Successfully updated category for {updatedCount} out of {updates.Count} mods", "ModMetadataService");
        _processRegistry.Complete(procId);

        // Emit single CATEGORY_UPDATED event for batch operation
        if (updatedCount > 0)
        {
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CATEGORY_UPDATED, new { updates, count = updatedCount }).ConfigureAwait(false);
        }

        return updatedCount;
    }

    #endregion
}
