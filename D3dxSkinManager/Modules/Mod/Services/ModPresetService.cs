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
    private readonly IModArchiveService _archiveService;
    private readonly IModCacheService _cacheService;

    public ModPresetService(
        IModPresetRepository presetRepository,
        IModRepository modRepository,
        IModLifecycleService lifecycleService,
        IProfileEventBus eventBus,
        ILogHelper logger,
        IProcessRegistry processRegistry,
        ID3dmigotoUserConfigService userConfig,
        IModArchiveService archiveService,
        IModCacheService cacheService)
    {
        _presetRepository = presetRepository;
        _modRepository = modRepository;
        _lifecycleService = lifecycleService;
        _eventBus = eventBus;
        _logger = logger;
        _processRegistry = processRegistry;
        _userConfig = userConfig;
        _archiveService = archiveService;
        _cacheService = cacheService;
    }

    /// <summary>Keep only MANAGED mod ids (those with a DB row). `GetLoadedIdsAsync` scans the DEPLOY
    /// folder, which also lists unmanaged/anonymous mods the app shows but can't redeploy from a managed
    /// archive — a preset must not capture, store, or re-apply those (applying an unmanaged member fails
    /// EVERY time, #36). Reference the DB (`ExistsAsync`) to exclude them.</summary>
    private async Task<List<string>> FilterManagedAsync(IEnumerable<string> ids)
    {
        var managed = new List<string>();
        foreach (var id in ids)
            if (await _modRepository.ExistsAsync(id).ConfigureAwait(false))
                managed.Add(id);
        return managed;
    }

    /// <summary>Snapshot the given MANAGED mods' 3DMigoto $var state from d3dx_user.ini as a JSON blob
    /// for the preset. Null when nothing was captured (no mods, or an internal profile with no
    /// d3dx_user.ini). Callers pass ids already filtered by <see cref="FilterManagedAsync"/>.</summary>
    private string? CaptureModState(IReadOnlyCollection<string> managedModIds)
    {
        if (managedModIds.Count == 0) return null;
        var lines = _userConfig.CaptureVarLines(managedModIds);
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

        // Store only MANAGED mods — an unmanaged/anonymous deployed mod can't be redeployed from a
        // managed archive, so applying it later fails every time (#36). Filter at the source.
        var managedIds = await FilterManagedAsync(loadedIds).ConfigureAwait(false);
        if (managedIds.Count == 0)
            throw new OperationException("PRESET_NO_ACTIVE_MODS");

        var entity = new ModPresetEntity
        {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Name = name.Trim(),
            ModIds = JsonSerializer.Serialize(managedIds),
            ModState = captureModState ? CaptureModState(managedIds) : null
        };

        await _presetRepository.InsertAsync(entity).ConfigureAwait(false);
        _logger.Info($"Saved preset '{name}' with {managedIds.Count} mods", "ModPresetService");

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

        // Managed only — see SaveAsync / #36 (an unmanaged member can't be re-applied).
        var managedIds = await FilterManagedAsync(loadedIds).ConfigureAwait(false);
        if (managedIds.Count == 0)
            throw new OperationException("PRESET_NO_ACTIVE_MODS");

        entity.ModIds = JsonSerializer.Serialize(managedIds);
        // If this preset captured mod state, refresh it from the current d3dx_user.ini too (managed only).
        if (entity.ModState != null)
            entity.ModState = CaptureModState(managedIds);
        await _presetRepository.UpdateAsync(entity).ConfigureAwait(false);
        _logger.Info($"Overwrote preset '{entity.Name}' with {managedIds.Count} currently loaded mods", "ModPresetService");

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

        // Self-heal a stale preset. Skip members that can never load, so they don't count as "failed" on
        // every apply:
        //  (a) no DB row — a deleted mod or a legacy unmanaged entry that can't be redeployed (#36); and
        //  (b) no archive AND no retained cache — nothing to decompress/enable from, so LoadAsync throws
        //      MOD_EXTRACTION_FAILED ("load mod from decompress failed") every time. Skipping it is the fix.
        var requestedCount = targetModIds.Count;
        var managedIds = await FilterManagedAsync(targetModIds).ConfigureAwait(false);
        targetModIds = managedIds
            .Where(mid => _archiveService.ArchiveExists(mid) || _cacheService.HasCache(mid))
            .ToList();
        var skippedCount = requestedCount - targetModIds.Count;
        if (skippedCount > 0)
            _logger.Warn($"Preset '{entity.Name}': skipping {skippedCount} member(s) that can't apply (deleted/unmanaged, or no archive/cache to deploy from)", "ModPresetService");

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
            // Pass the target ids so ApplyVarLines can drift-match a var whose inner namespace path changed
            // since capture (re-fix/merge/rename) onto the current line 3DMigoto emits.
            var varsApplied = 0;
            if (!string.IsNullOrEmpty(entity.ModState))
            {
                try
                {
                    var varLines = JsonSerializer.Deserialize<List<string>>(entity.ModState) ?? new List<string>();
                    varsApplied = _userConfig.ApplyVarLines(varLines, targetModIds);
                    if (varsApplied > 0)
                        _logger.Info($"Restored {varsApplied} var(s) for preset '{entity.Name}'", "ModPresetService");
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
                FailedModIds = failedIds,
                SkippedCount = skippedCount,
                VarsApplied = varsApplied
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
