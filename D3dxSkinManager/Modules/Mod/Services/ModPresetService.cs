using System.Text.Json;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod preset operations
/// </summary>
public interface IModPresetService
{
    Task<List<ModPresetInfo>> GetAllAsync();
    Task<ModPresetInfo> SaveAsync(string name);
    Task<ModPresetInfo> UpdateAsync(string id, string name);
    Task<bool> DeleteAsync(string id);
    Task<ModPresetApplyResult> ApplyAsync(string id);
    Task<bool> UnloadAllAsync();
}

/// <summary>
/// Service for mod preset business logic
/// Saves snapshots of currently active mods and applies them later
/// </summary>
public class ModPresetService : IModPresetService
{
    private readonly IModPresetRepository _presetRepository;
    private readonly IModRepository _modRepository;
    private readonly IModLifecycleService _lifecycleService;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    public ModPresetService(
        IModPresetRepository presetRepository,
        IModRepository modRepository,
        IModLifecycleService lifecycleService,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _presetRepository = presetRepository;
        _modRepository = modRepository;
        _lifecycleService = lifecycleService;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Get all presets with their mod counts
    /// </summary>
    public async Task<List<ModPresetInfo>> GetAllAsync()
    {
        var entities = await _presetRepository.GetAllAsync().ConfigureAwait(false);
        return entities.Select(ToInfo).ToList();
    }

    /// <summary>
    /// Save currently active (loaded) mods as a new preset
    /// </summary>
    public async Task<ModPresetInfo> SaveAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationException("PRESET_NAME_REQUIRED");

        // Check for duplicate name
        var existing = await _presetRepository.GetByNameAsync(name.Trim()).ConfigureAwait(false);
        if (existing != null)
            throw new OperationException("PRESET_NAME_DUPLICATE", new Dictionary<string, string> { { "name", name } });

        // Get currently loaded mod IDs from file system
        var loadedIds = await _modRepository.GetLoadedIdsAsync().ConfigureAwait(false);

        if (loadedIds.Count == 0)
            throw new OperationException("PRESET_NO_ACTIVE_MODS");

        var entity = new ModPresetEntity
        {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Name = name.Trim(),
            ModIds = JsonSerializer.Serialize(loadedIds)
        };

        await _presetRepository.InsertAsync(entity).ConfigureAwait(false);
        _logger.Info($"Saved preset '{name}' with {loadedIds.Count} mods", "ModPresetService");

        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_SAVED, new { id = entity.Id, name = entity.Name }).ConfigureAwait(false);

        return ToInfo(entity);
    }

    /// <summary>
    /// Update preset name
    /// </summary>
    public async Task<ModPresetInfo> UpdateAsync(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationException("PRESET_NAME_REQUIRED");

        var entity = await _presetRepository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
            throw new OperationException("PRESET_NOT_FOUND", new Dictionary<string, string> { { "id", id } });

        // Check for duplicate name (but allow same name for same preset)
        var existing = await _presetRepository.GetByNameAsync(name.Trim()).ConfigureAwait(false);
        if (existing != null && existing.Id != id)
            throw new OperationException("PRESET_NAME_DUPLICATE", new Dictionary<string, string> { { "name", name } });

        entity.Name = name.Trim();
        await _presetRepository.UpdateAsync(entity).ConfigureAwait(false);

        return ToInfo(entity);
    }

    /// <summary>
    /// Delete a preset
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _presetRepository.DeleteAsync(id).ConfigureAwait(false);

        if (result)
        {
            _logger.Info($"Deleted preset {id}", "ModPresetService");
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_DELETED, new { id }).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Apply a preset: unload all currently loaded mods, then load the preset's mods
    /// </summary>
    public async Task<ModPresetApplyResult> ApplyAsync(string id)
    {
        var entity = await _presetRepository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
            throw new OperationException("PRESET_NOT_FOUND", new Dictionary<string, string> { { "id", id } });

        var targetModIds = JsonSerializer.Deserialize<List<string>>(entity.ModIds) ?? new List<string>();

        // Step 1: Unload all currently loaded mods
        var currentlyLoaded = await _modRepository.GetLoadedIdsAsync().ConfigureAwait(false);
        var unloadedCount = 0;
        foreach (var modId in currentlyLoaded)
        {
            // Don't unload mods that are already in the target set
            if (targetModIds.Contains(modId))
                continue;

            try
            {
                await _lifecycleService.UnloadAsync(modId).ConfigureAwait(false);
                unloadedCount++;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to unload mod {modId} during preset apply: {ex.Message}", "ModPresetService");
            }
        }

        // Step 2: Load mods from preset
        var loadedCount = 0;
        var failedIds = new List<string>();
        foreach (var modId in targetModIds)
        {
            // Skip mods that are already loaded
            if (currentlyLoaded.Contains(modId))
            {
                loadedCount++;
                continue;
            }

            try
            {
                await _lifecycleService.LoadAsync(modId).ConfigureAwait(false);
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to load mod {modId} during preset apply: {ex.Message}", "ModPresetService");
                failedIds.Add(modId);
            }
        }

        _logger.Info($"Applied preset '{entity.Name}': {loadedCount} loaded, {failedIds.Count} failed, {unloadedCount} unloaded", "ModPresetService");

        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_APPLIED, new { id = entity.Id, name = entity.Name }).ConfigureAwait(false);

        return new ModPresetApplyResult
        {
            PresetName = entity.Name,
            LoadedCount = loadedCount,
            FailedCount = failedIds.Count,
            FailedModIds = failedIds
        };
    }

    /// <summary>
    /// Unload all currently loaded mods
    /// </summary>
    public async Task<bool> UnloadAllAsync()
    {
        var currentlyLoaded = await _modRepository.GetLoadedIdsAsync().ConfigureAwait(false);

        foreach (var modId in currentlyLoaded)
        {
            try
            {
                await _lifecycleService.UnloadAsync(modId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to unload mod {modId}: {ex.Message}", "ModPresetService");
            }
        }

        _logger.Info($"Unloaded all {currentlyLoaded.Count} mods", "ModPresetService");
        return true;
    }

    private static ModPresetInfo ToInfo(ModPresetEntity entity)
    {
        var modIds = JsonSerializer.Deserialize<List<string>>(entity.ModIds) ?? new List<string>();
        return new ModPresetInfo
        {
            Id = entity.Id,
            Name = entity.Name,
            ModCount = modIds.Count,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
