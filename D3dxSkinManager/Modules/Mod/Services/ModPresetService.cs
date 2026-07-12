using System.Text.Json;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
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
    /// <summary><paramref name="captureModState"/> also snapshots the active mods' 3DMigoto $var state from
    /// d3dx_user.ini so applying the preset restores it (see D3dmigotoUserConfigService).</summary>
    Task<ModPresetInfo> SaveAsync(string name, bool captureModState);
    Task<ModPresetInfo> OverwriteAsync(string id);
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
    private readonly IProcessRegistry _processRegistry;
    private readonly ID3dmigotoUserConfigService _userConfig;

    public ModPresetService(
        IModPresetRepository presetRepository,
        IModRepository modRepository,
        IModLifecycleService lifecycleService,
        IProfileEventBus eventBus,
        ILogHelper logger,
        IProcessRegistry processRegistry,
        ID3dmigotoUserConfigService userConfig)
    {
        _presetRepository = presetRepository;
        _modRepository = modRepository;
        _lifecycleService = lifecycleService;
        _eventBus = eventBus;
        _logger = logger;
        _processRegistry = processRegistry;
        _userConfig = userConfig;
    }

    /// <summary>Snapshot the given mods' 3DMigoto $var state from d3dx_user.ini as a JSON blob for the
    /// preset — ONLY for MANAGED mods (those with a DB row). `GetLoadedIdsAsync` scans the DEPLOY folder,
    /// which also contains unmanaged/anonymous mods the app shows but can't redeploy from a managed archive
    /// (so their state could never be restored) — so reference the DB (`ExistsAsync`) to exclude them.
    /// Null when nothing was captured (no managed mods, or an internal profile with no d3dx_user.ini).</summary>
    private async Task<string?> CaptureModStateAsync(IReadOnlyCollection<string> modIds)
    {
        var managedIds = new List<string>();
        foreach (var id in modIds)
            if (await _modRepository.ExistsAsync(id).ConfigureAwait(false))
                managedIds.Add(id);
        if (managedIds.Count == 0) return null;
        var lines = _userConfig.CaptureVarLines(managedIds);
        return lines.Count > 0 ? JsonSerializer.Serialize(lines) : null;
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
    public async Task<ModPresetInfo> SaveAsync(string name, bool captureModState)
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
            ModIds = JsonSerializer.Serialize(loadedIds),
            ModState = captureModState ? await CaptureModStateAsync(loadedIds) : null
        };

        await _presetRepository.InsertAsync(entity).ConfigureAwait(false);
        _logger.Info($"Saved preset '{name}' with {loadedIds.Count} mods", "ModPresetService");

        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_SAVED, new { id = entity.Id, name = entity.Name }).ConfigureAwait(false);

        return ToInfo(entity);
    }

    /// <summary>
    /// Overwrite a preset's mod list with the currently loaded mods (keeps its name).
    /// The "update preset with current setting" the user asked for — Save/Update only
    /// created new presets or renamed.
    /// </summary>
    public async Task<ModPresetInfo> OverwriteAsync(string id)
    {
        var entity = await _presetRepository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
            throw new OperationException("PRESET_NOT_FOUND", new Dictionary<string, string> { { "id", id } });

        var loadedIds = await _modRepository.GetLoadedIdsAsync().ConfigureAwait(false);
        if (loadedIds.Count == 0)
            throw new OperationException("PRESET_NO_ACTIVE_MODS");

        entity.ModIds = JsonSerializer.Serialize(loadedIds);
        // If this preset captured mod state, refresh it from the current d3dx_user.ini too (managed only).
        if (entity.ModState != null)
            entity.ModState = await CaptureModStateAsync(loadedIds);
        await _presetRepository.UpdateAsync(entity).ConfigureAwait(false);
        _logger.Info($"Overwrote preset '{entity.Name}' with {loadedIds.Count} currently loaded mods", "ModPresetService");

        // PRESET_SAVED refreshes the preset menu (same consumer as a new save).
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_SAVED, new { id = entity.Id, name = entity.Name }).ConfigureAwait(false);

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

        // Track the whole preset apply as one process (the headline progress); the individual
        // load/unload steps register their own short-lived processes too.
        var procId = _processRegistry.Start(ProcessType.PresetApply, $"Applying preset: {entity.Name}",
            titleKey: "process.presetApply", titleArg: entity.Name);
        try
        {
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
            var total = targetModIds.Count;
            foreach (var modId in targetModIds)
            {
                // Skip mods that are already loaded
                if (currentlyLoaded.Contains(modId))
                {
                    loadedCount++;
                }
                else
                {
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
                _processRegistry.Report(procId, total > 0 ? (int)((loadedCount + failedIds.Count) * 100.0 / total) : null);
            }

            // Restore each mod's persisted $var state into d3dx_user.ini (if captured) so the mods load
            // carrying it — 3DMigoto reads d3dx_user.ini on next load. Best-effort; never fails the apply.
            if (!string.IsNullOrEmpty(entity.ModState))
            {
                try
                {
                    var varLines = JsonSerializer.Deserialize<List<string>>(entity.ModState) ?? new List<string>();
                    if (_userConfig.ApplyVarLines(varLines))
                        _logger.Info($"Restored {varLines.Count} var(s) for preset '{entity.Name}'", "ModPresetService");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to restore var state for preset '{entity.Name}': {ex.Message}", "ModPresetService");
                }
            }

            _logger.Info($"Applied preset '{entity.Name}': {loadedCount} loaded, {failedIds.Count} failed, {unloadedCount} unloaded", "ModPresetService");

            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_APPLIED, new { id = entity.Id, name = entity.Name }).ConfigureAwait(false);

            _processRegistry.Complete(procId);

            return new ModPresetApplyResult
            {
                PresetName = entity.Name,
                LoadedCount = loadedCount,
                FailedCount = failedIds.Count,
                FailedModIds = failedIds
            };
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Unload all currently loaded mods
    /// </summary>
    public async Task<bool> UnloadAllAsync()
    {
        var currentlyLoaded = await _modRepository.GetLoadedIdsAsync().ConfigureAwait(false);

        var procId = _processRegistry.Start(ProcessType.PresetApply, "Unloading all mods",
            titleKey: "process.unloadAll");
        try
        {
            var total = currentlyLoaded.Count;
            var done = 0;
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
                done++;
                _processRegistry.Report(procId, total > 0 ? (int)(done * 100.0 / total) : null);
            }
            _processRegistry.Complete(procId);
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            throw;
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
            HasModState = !string.IsNullOrEmpty(entity.ModState),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
