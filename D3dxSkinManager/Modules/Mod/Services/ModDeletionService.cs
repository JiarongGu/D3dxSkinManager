using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Context.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service interface for mod deletion operations
/// </summary>
public interface IModDeletionService
{
    Task<bool> DeleteAsync(string id);
    Task<BatchDeleteResult> BatchDeleteAsync(List<string> ids);
}

/// <summary>
/// Orchestrates mod deletion across multiple services
/// Handles the complete deletion workflow: Cache -> Preview -> Archive -> Database
/// Uses enrichment service to accurately detect what needs to be deleted
/// Each step throws OperationException with specific error codes if it fails
/// </summary>
public class ModDeletionService : IModDeletionService
{
    private readonly IModRepository _repository;
    private readonly IModCacheService _cacheService;
    private readonly IModArchiveService _archiveService;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IProfilePathService _profilePaths;
    private readonly IFileOperationPlanner _operationPlanner;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModDeletionService(
        IModRepository repository,
        IModCacheService cacheService,
        IModArchiveService archiveService,
        IModEnrichmentService enrichmentService,
        IProfilePathService profilePaths,
        IFileOperationPlanner operationPlanner,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _repository = repository;
        _cacheService = cacheService;
        _archiveService = archiveService;
        _enrichmentService = enrichmentService;
        _profilePaths = profilePaths;
        _operationPlanner = operationPlanner;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Delete a mod by Id
    /// Deletion order: Cache -> Preview -> Archive -> Database
    /// If any step fails, the entire operation fails with OperationException
    /// Uses enrichment service to accurately determine what needs to be deleted
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id is required", nameof(id));

        // Check if mod exists
        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
        {
            _logger.Warn($"Mod not found for deletion: {id}", "ModDeletionService");
            throw new OperationException(
                ModErrorCodes.DELETE_MOD_NOT_FOUND,
                new Dictionary<string, string> { { "id", id } },
                $"Mod not found: {id}"
            );
        }

        // Convert to domain model
        var mod = ModMapper.ToDomain(entity);

        // Use enrichment service to populate status flags from filesystem
        // This ensures we accurately detect what needs to be deleted
        _enrichmentService.PopulateStatusFlags(new List<ModInfo> { mod });

        _logger.Info($"Starting deletion process for mod: {id} (Name: {mod.Name})", "ModDeletionService");
        _logger.Debug($"Deletion flags - HasCache: {mod.HasCache}, HasPreviewFolder: {mod.HasPreviewFolder}, IsAvailable: {mod.IsAvailable}", "ModDeletionService");

        try
        {
            // Step 1: Delete cache (if exists)
            await DeleteCacheAsync(mod).ConfigureAwait(false);

            // Step 2: Delete preview folder (if exists)
            await DeletePreviewFolderAsync(mod).ConfigureAwait(false);

            // Step 3: Delete archive file (if exists)
            await DeleteArchiveAsync(mod).ConfigureAwait(false);

            // Step 4: Delete from database
            await DeleteFromDatabaseAsync(mod).ConfigureAwait(false);

            // Emit DELETED event to notify frontend
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { Id = id }).ConfigureAwait(false);

            _logger.Info($"Mod deletion completed successfully: {id} (Name: {mod.Name})", "ModDeletionService");
            return true;
        }
        catch (OperationException)
        {
            // Re-throw OperationException as-is (already has proper error code)
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unexpected exceptions in OperationException
            _logger.Error($"Unexpected error during mod deletion {id}: {ex.Message}", "ModDeletionService", ex);
            throw new OperationException(
                ModErrorCodes.DELETE_FAILED,
                new Dictionary<string, string> { { "id", id }, { "name", mod.Name } },
                $"Unexpected error deleting mod {id}: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Step 1: Delete cache directory (if exists)
    /// </summary>
    private async Task DeleteCacheAsync(ModInfo mod)
    {
        if (!mod.HasCache)
        {
            _logger.Debug($"Step 1/4: No cache to delete for mod: {mod.Id}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 1/4: Deleting cache for mod: {mod.Id}", "ModDeletionService");
        var cacheDeleted = await _cacheService.DeleteCacheAsync(mod.Id).ConfigureAwait(false);
        if (!cacheDeleted)
        {
            // Verify if cache still exists - if it does, deletion failed (not just "not found")
            var cachePath = _cacheService.GetCachePath(mod.Id);
            if (Directory.Exists(cachePath))
            {
                // Cache exists but deletion failed - this is a real error
                throw new OperationException(
                    ModErrorCodes.DELETE_CACHE_FAILED,
                    new Dictionary<string, string> { { "id", mod.Id }, { "name", mod.Name } },
                    $"Failed to delete cache for mod {mod.Id} - cache still exists at {cachePath}"
                );
            }
            else
            {
                // Cache doesn't exist - it was deleted externally or between enrichment and deletion
                _logger.Debug($"Cache not found or already deleted externally for mod: {mod.Id}", "ModDeletionService");
            }
        }
        else
        {
            _logger.Info($"Successfully deleted cache for mod: {mod.Id}", "ModDeletionService");
        }
    }

    /// <summary>
    /// Step 2: Delete preview folder (if exists)
    /// </summary>
    private async Task DeletePreviewFolderAsync(ModInfo mod)
    {
        if (!mod.HasPreviewFolder)
        {
            _logger.Debug($"Step 2/4: No preview folder to delete for mod: {mod.Id}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 2/4: Deleting preview folder for mod: {mod.Id}", "ModDeletionService");

        var previewFolderPath = _profilePaths.GetPreviewDirectoryPath(mod.Id);
        if (!Directory.Exists(previewFolderPath))
        {
            _logger.Debug($"Preview folder already deleted or not found for mod: {mod.Id}", "ModDeletionService");
            return;
        }

        // Route through the planner (like cache/archive deletion) so the preview directory — a protected
        // mod-data path — is serialized with every other FS op and never raced. A raw Directory.Delete
        // here ran concurrently with the planner worker. See filesystem-operation-serialization.md.
        var deleteOp = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.DeleteDirectory,
            SourcePath = previewFolderPath
        };
        var result = await _operationPlanner.SubmitOperationAsync(deleteOp).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.Info($"Successfully deleted preview folder for mod: {mod.Id}", "ModDeletionService");
            return;
        }

        // Deletion failed but the folder is gone (deleted externally/in-flight) — treat as success.
        if (!Directory.Exists(previewFolderPath))
        {
            _logger.Debug($"Preview folder not found after delete for mod {mod.Id} — already gone", "ModDeletionService");
            return;
        }

        _logger.Error($"Error deleting preview folder for mod {mod.Id}: {result.ErrorMessage}", "ModDeletionService", result.Exception);
        throw new OperationException(
            ModErrorCodes.DELETE_PREVIEW_FAILED,
            new Dictionary<string, string> { { "id", mod.Id }, { "name", mod.Name } },
            $"Failed to delete preview folder for mod {mod.Id}: {result.ErrorMessage}",
            result.Exception
        );
    }

    /// <summary>
    /// Step 3: Delete archive file (if exists)
    /// </summary>
    private async Task DeleteArchiveAsync(ModInfo mod)
    {
        if (!mod.IsAvailable)
        {
            _logger.Debug($"Step 3/4: No archive to delete for mod: {mod.Id}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 3/4: Deleting archive for mod: {mod.Id}", "ModDeletionService");
        var archiveDeleted = await _archiveService.DeleteArchiveAsync(mod.Id).ConfigureAwait(false);
        if (!archiveDeleted)
        {
            // Verify if archive still exists - if it does, deletion failed (not just "not found")
            var archivePath = _profilePaths.GetModArchivePath(mod.Id, "");
            if (File.Exists(archivePath))
            {
                // Archive exists but deletion failed - this is a real error
                throw new OperationException(
                    ModErrorCodes.DELETE_ARCHIVE_FAILED,
                    new Dictionary<string, string> { { "id", mod.Id }, { "name", mod.Name } },
                    $"Failed to delete archive for mod {mod.Id} - archive still exists at {archivePath}"
                );
            }
            else
            {
                // Archive doesn't exist - it was deleted externally or imported as metadata-only
                _logger.Debug($"Archive not found or already deleted externally for mod: {mod.Id}", "ModDeletionService");
            }
        }
        else
        {
            _logger.Info($"Successfully deleted archive for mod: {mod.Id}", "ModDeletionService");
        }
    }

    /// <summary>
    /// Step 4: Delete from database
    /// </summary>
    private async Task DeleteFromDatabaseAsync(ModInfo mod)
    {
        _logger.Info($"Step 4/4: Deleting from database for mod: {mod.Id}", "ModDeletionService");
        var success = await _repository.DeleteAsync(mod.Id).ConfigureAwait(false);
        if (!success)
        {
            throw new OperationException(
                ModErrorCodes.DELETE_DATABASE_FAILED,
                new Dictionary<string, string> { { "id", mod.Id }, { "name", mod.Name } },
                $"Failed to delete mod from database: {mod.Id}"
            );
        }

        _logger.Info($"Successfully deleted mod from database: {mod.Id}", "ModDeletionService");
    }

    /// <summary>
    /// Batch delete multiple mods
    /// Processes all deletions and returns summary of results
    /// Emits events for each successful deletion
    /// </summary>
    public async Task<BatchDeleteResult> BatchDeleteAsync(List<string> ids)
    {
        var result = new BatchDeleteResult();

        if (ids == null || ids.Count == 0)
        {
            return result;
        }

        _logger.Info($"Starting batch deletion for {ids.Count} mods", "ModDeletionService");

        foreach (var id in ids)
        {
            try
            {
                var success = await DeleteAsync(id).ConfigureAwait(false);
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailedCount++;
                    result.FailedIds.Add(id);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting mod {id}: {ex.Message}", "ModDeletionService", ex);
                result.FailedCount++;
                result.FailedIds.Add(id);
            }
        }

        _logger.Info($"Batch deletion completed: {result.SuccessCount} succeeded, {result.FailedCount} failed", "ModDeletionService");

        return result;
    }
}
