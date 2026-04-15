using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Service for migrating mod IDs from legacy hash format to GUID format.
/// Handles database updates, file renames (archives, caches, previews),
/// and mod preset reference updates.
/// </summary>
public interface IModIdMigrationService
{
    /// <summary>
    /// Scan all mods and identify those with non-GUID IDs
    /// </summary>
    Task<ModIdMigrationScanResult> ScanAsync();

    /// <summary>
    /// Migrate all non-GUID mod IDs to GUID format.
    /// Updates database, renames files/folders, and updates preset references.
    /// </summary>
    Task<ModIdMigrationResult> MigrateAsync();
}

public class ModIdMigrationService : IModIdMigrationService
{
    private readonly string _connectionString;
    private readonly IProfilePathService _profilePaths;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    public ModIdMigrationService(
        IProfilePathService profilePaths,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _eventBus = eventBus;
        _logger = logger;

        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    public async Task<ModIdMigrationScanResult> ScanAsync()
    {
        _logger.Info("[ModIdMigration] Starting scan for non-GUID mod IDs");

        var allMods = await GetAllModsAsync();
        var needsMigration = allMods.Where(m => !IsGuidFormat(m.Id)).ToList();

        var items = new List<ModIdMigrationItem>();
        foreach (var mod in needsMigration)
        {
            items.Add(new ModIdMigrationItem
            {
                OldId = mod.Id,
                NewId = ModInfo.NewId(),
                ModName = mod.Name,
                HasArchive = HasArchiveFile(mod.Id),
                HasCache = HasCacheDirectory(mod.Id),
                HasPreview = HasPreviewDirectory(mod.Id),
            });
        }

        _logger.Info($"[ModIdMigration] Scan complete: {allMods.Count} total, {needsMigration.Count} need migration");

        return new ModIdMigrationScanResult
        {
            TotalMods = allMods.Count,
            ModsNeedingMigration = needsMigration.Count,
            Items = items,
        };
    }

    public async Task<ModIdMigrationResult> MigrateAsync()
    {
        _logger.Info("[ModIdMigration] Starting migration");

        var scanResult = await ScanAsync();
        var result = new ModIdMigrationResult
        {
            Total = scanResult.ModsNeedingMigration,
        };

        if (scanResult.ModsNeedingMigration == 0)
        {
            _logger.Info("[ModIdMigration] No mods need migration");
            return result;
        }

        // Build old→new ID mapping for preset updates
        var idMapping = new Dictionary<string, string>();

        for (var i = 0; i < scanResult.Items.Count; i++)
        {
            var item = scanResult.Items[i];
            var itemResult = new ModIdMigrationItemResult
            {
                OldId = item.OldId,
                NewId = item.NewId,
                ModName = item.ModName,
            };

            try
            {
                await MigrateModAsync(item);
                itemResult.Success = true;
                result.Succeeded++;
                idMapping[item.OldId] = item.NewId;
                _logger.Info($"[ModIdMigration] Migrated '{item.ModName}': {item.OldId} → {item.NewId}");
            }
            catch (Exception ex)
            {
                itemResult.Success = false;
                itemResult.Error = ex.Message;
                result.Failed++;
                _logger.Error($"[ModIdMigration] Failed to migrate '{item.ModName}' ({item.OldId}): {ex.Message}");
            }

            result.Results.Add(itemResult);

            // Emit progress
            await _eventBus.EmitAsync(
                ModuleNames.TOOL,
                ToolEvents.MOD_ID_MIGRATION_PROGRESS,
                new { current = i + 1, total = scanResult.Items.Count, modName = item.ModName });
        }

        // Update mod presets that reference migrated IDs
        if (idMapping.Count > 0)
        {
            await UpdateModPresetsAsync(idMapping);
        }

        _logger.Info($"[ModIdMigration] Migration complete: {result.Succeeded} succeeded, {result.Failed} failed");

        return result;
    }

    private async Task MigrateModAsync(ModIdMigrationItem item)
    {
        var renamedFiles = new List<(string from, string to)>();

        try
        {
            // 1. Rename file system artifacts first (reversible)
            RenameArchiveFiles(item.OldId, item.NewId, renamedFiles);
            RenameCacheDirectory(item.OldId, item.NewId, renamedFiles);
            RenamePreviewDirectory(item.OldId, item.NewId, renamedFiles);

            // 2. Update database (atomic)
            await UpdateModIdInDatabaseAsync(item.OldId, item.NewId);
        }
        catch
        {
            // Roll back file renames on failure
            foreach (var (from, to) in renamedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (Directory.Exists(to))
                        Directory.Move(to, from);
                    else if (File.Exists(to))
                        File.Move(to, from);
                }
                catch (Exception rollbackEx)
                {
                    _logger.Error($"[ModIdMigration] Rollback failed for {to} → {from}: {rollbackEx.Message}");
                }
            }

            throw;
        }
    }

    private void RenameArchiveFiles(string oldId, string newId, List<(string, string)> renamedFiles)
    {
        var modsDir = _profilePaths.ModsDirectory;
        if (!Directory.Exists(modsDir)) return;

        // Find all files starting with the old ID (handles any extension: .7z, .zip, etc.)
        var files = Directory.GetFiles(modsDir)
            .Where(f => Path.GetFileName(f).StartsWith(oldId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var newFileName = newId + fileName.Substring(oldId.Length);
            var newPath = Path.Combine(modsDir, newFileName);
            File.Move(file, newPath);
            renamedFiles.Add((file, newPath));
            _logger.Verbose($"[ModIdMigration] Renamed archive: {fileName} → {newFileName}");
        }
    }

    private void RenameCacheDirectory(string oldId, string newId, List<(string, string)> renamedFiles)
    {
        var cacheDir = _profilePaths.CacheModsDirectory;
        if (!Directory.Exists(cacheDir)) return;

        // Active cache: {oldId}/
        var activeOld = Path.Combine(cacheDir, oldId);
        if (Directory.Exists(activeOld))
        {
            var activeNew = Path.Combine(cacheDir, newId);
            Directory.Move(activeOld, activeNew);
            renamedFiles.Add((activeOld, activeNew));
            _logger.Verbose($"[ModIdMigration] Renamed active cache: {oldId} → {newId}");
        }

        // Disabled cache: DISABLED-{oldId}/
        var disabledOld = Path.Combine(cacheDir, $"DISABLED-{oldId}");
        if (Directory.Exists(disabledOld))
        {
            var disabledNew = Path.Combine(cacheDir, $"DISABLED-{newId}");
            Directory.Move(disabledOld, disabledNew);
            renamedFiles.Add((disabledOld, disabledNew));
            _logger.Verbose($"[ModIdMigration] Renamed disabled cache: DISABLED-{oldId} → DISABLED-{newId}");
        }
    }

    private void RenamePreviewDirectory(string oldId, string newId, List<(string, string)> renamedFiles)
    {
        var previewDir = _profilePaths.GetPreviewDirectoryPath(oldId);
        if (!Directory.Exists(previewDir)) return;

        var newPreviewDir = _profilePaths.GetPreviewDirectoryPath(newId);
        Directory.Move(previewDir, newPreviewDir);
        renamedFiles.Add((previewDir, newPreviewDir));
        _logger.Verbose($"[ModIdMigration] Renamed preview dir: {oldId} → {newId}");
    }

    private async Task UpdateModIdInDatabaseAsync(string oldId, string newId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var rowsAffected = await connection.ExecuteAsync(
                "UPDATE Mods SET Id = @newId WHERE Id = @oldId",
                new { oldId, newId },
                (global::System.Data.IDbTransaction)transaction);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Mod with ID '{oldId}' not found in database");
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpdateModPresetsAsync(Dictionary<string, string> idMapping)
    {
        _logger.Info($"[ModIdMigration] Updating mod presets with {idMapping.Count} ID mappings");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check if ModPresets table exists (it might not in older databases)
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ModPresets'");
        if (tableExists == 0)
        {
            _logger.Info("[ModIdMigration] ModPresets table not found, skipping preset updates");
            return;
        }

        var presets = (await connection.QueryAsync<ModPresetEntity>(
            "SELECT * FROM ModPresets")).ToList();

        foreach (var preset in presets)
        {
            if (string.IsNullOrEmpty(preset.ModIds)) continue;

            var modIds = JsonHelper.Deserialize<List<string>>(preset.ModIds);
            if (modIds == null) continue;

            var updated = false;
            for (var i = 0; i < modIds.Count; i++)
            {
                if (idMapping.TryGetValue(modIds[i], out var newId))
                {
                    modIds[i] = newId;
                    updated = true;
                }
            }

            if (updated)
            {
                var newModIdsJson = JsonHelper.Serialize(modIds);
                await connection.ExecuteAsync(
                    "UPDATE ModPresets SET ModIds = @modIds, UpdatedAt = @updatedAt WHERE Id = @id",
                    new { modIds = newModIdsJson, updatedAt = DateTime.UtcNow, id = preset.Id });

                _logger.Info($"[ModIdMigration] Updated preset '{preset.Name}' with new mod IDs");
            }
        }
    }

    private async Task<List<ModEntity>> GetAllModsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<ModEntity>("SELECT * FROM Mods");
        return entities.ToList();
    }

    private bool HasArchiveFile(string id)
    {
        var modsDir = _profilePaths.ModsDirectory;
        if (!Directory.Exists(modsDir)) return false;
        return Directory.GetFiles(modsDir)
            .Any(f => Path.GetFileName(f).StartsWith(id, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasCacheDirectory(string id)
    {
        var cacheDir = _profilePaths.CacheModsDirectory;
        if (!Directory.Exists(cacheDir)) return false;
        return Directory.Exists(Path.Combine(cacheDir, id))
            || Directory.Exists(Path.Combine(cacheDir, $"DISABLED-{id}"));
    }

    private bool HasPreviewDirectory(string id)
    {
        return Directory.Exists(_profilePaths.GetPreviewDirectoryPath(id));
    }

    /// <summary>
    /// Check if a mod ID is in the GUID "N" format (32 uppercase hex characters)
    /// </summary>
    private static bool IsGuidFormat(string id)
    {
        if (id.Length != 32) return false;
        foreach (var c in id)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }
}
