using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service interface for mod deletion operations
/// </summary>
public interface IModDeletionService
{
    Task<bool> DeleteAsync(string sha);
    Task<BatchDeleteResult> BatchDeleteAsync(List<string> shas);
}

/// <summary>
/// Orchestrates mod deletion across multiple services
/// Handles the complete deletion workflow: Cache → Preview → Archive → Database
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
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModDeletionService(
        IModRepository repository,
        IModCacheService cacheService,
        IModArchiveService archiveService,
        IModEnrichmentService enrichmentService,
        IProfilePathService profilePaths,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _repository = repository;
        _cacheService = cacheService;
        _archiveService = archiveService;
        _enrichmentService = enrichmentService;
        _profilePaths = profilePaths;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Delete a mod by SHA
    /// Deletion order: Cache → Preview → Archive → Database
    /// If any step fails, the entire operation fails with OperationException
    /// Uses enrichment service to accurately determine what needs to be deleted
    /// </summary>
    public async Task<bool> DeleteAsync(string sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        // Check if mod exists
        var mod = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null)
        {
            _logger.Warn($"Mod not found for deletion: {sha}", "ModDeletionService");
            throw new OperationException(
                ModErrorCodes.DELETE_MOD_NOT_FOUND,
                new Dictionary<string, string> { { "sha", sha } },
                $"Mod not found: {sha}"
            );
        }

        // Use enrichment service to populate status flags from filesystem
        // This ensures we accurately detect what needs to be deleted
        _enrichmentService.PopulateStatusFlags(new List<ModInfo> { mod });

        _logger.Info($"Starting deletion process for mod: {sha} (Name: {mod.Name})", "ModDeletionService");
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
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { Sha = sha }).ConfigureAwait(false);

            _logger.Info($"Mod deletion completed successfully: {sha} (Name: {mod.Name})", "ModDeletionService");
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
            _logger.Error($"Unexpected error during mod deletion {sha}: {ex.Message}", "ModDeletionService", ex);
            throw new OperationException(
                ModErrorCodes.DELETE_FAILED,
                new Dictionary<string, string> { { "sha", sha }, { "name", mod.Name } },
                $"Unexpected error deleting mod {sha}: {ex.Message}",
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
            _logger.Debug($"Step 1/4: No cache to delete for mod: {mod.SHA}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 1/4: Deleting cache for mod: {mod.SHA}", "ModDeletionService");
        var cacheDeleted = await _cacheService.DeleteCacheAsync(mod.SHA).ConfigureAwait(false);
        if (!cacheDeleted)
        {
            throw new OperationException(
                ModErrorCodes.DELETE_CACHE_FAILED,
                new Dictionary<string, string> { { "sha", mod.SHA }, { "name", mod.Name } },
                $"Failed to delete cache for mod {mod.SHA}"
            );
        }

        _logger.Info($"Successfully deleted cache for mod: {mod.SHA}", "ModDeletionService");
    }

    /// <summary>
    /// Step 2: Delete preview folder (if exists)
    /// </summary>
    private async Task DeletePreviewFolderAsync(ModInfo mod)
    {
        if (!mod.HasPreviewFolder)
        {
            _logger.Debug($"Step 2/4: No preview folder to delete for mod: {mod.SHA}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 2/4: Deleting preview folder for mod: {mod.SHA}", "ModDeletionService");
        try
        {
            var previewFolderPath = _profilePaths.GetPreviewDirectoryPath(mod.SHA);
            if (Directory.Exists(previewFolderPath))
            {
                Directory.Delete(previewFolderPath, recursive: true);
                _logger.Info($"Successfully deleted preview folder for mod: {mod.SHA}", "ModDeletionService");
            }
        }
        catch (Exception ex)
        {
            throw new OperationException(
                ModErrorCodes.DELETE_PREVIEW_FAILED,
                new Dictionary<string, string> { { "sha", mod.SHA }, { "name", mod.Name } },
                $"Failed to delete preview folder for mod {mod.SHA}: {ex.Message}",
                ex
            );
        }

        // Avoid compiler warning about async method without await
        await Task.CompletedTask;
    }

    /// <summary>
    /// Step 3: Delete archive file (if exists)
    /// </summary>
    private async Task DeleteArchiveAsync(ModInfo mod)
    {
        if (!mod.IsAvailable)
        {
            _logger.Debug($"Step 3/4: No archive to delete for mod: {mod.SHA}", "ModDeletionService");
            return;
        }

        _logger.Info($"Step 3/4: Deleting archive for mod: {mod.SHA}", "ModDeletionService");
        var archiveDeleted = await _archiveService.DeleteArchiveAsync(mod.SHA).ConfigureAwait(false);
        if (!archiveDeleted)
        {
            throw new OperationException(
                ModErrorCodes.DELETE_ARCHIVE_FAILED,
                new Dictionary<string, string> { { "sha", mod.SHA }, { "name", mod.Name } },
                $"Failed to delete archive for mod {mod.SHA}"
            );
        }

        _logger.Info($"Successfully deleted archive for mod: {mod.SHA}", "ModDeletionService");
    }

    /// <summary>
    /// Step 4: Delete from database
    /// </summary>
    private async Task DeleteFromDatabaseAsync(ModInfo mod)
    {
        _logger.Info($"Step 4/4: Deleting from database for mod: {mod.SHA}", "ModDeletionService");
        var success = await _repository.DeleteAsync(mod.SHA).ConfigureAwait(false);
        if (!success)
        {
            throw new OperationException(
                ModErrorCodes.DELETE_DATABASE_FAILED,
                new Dictionary<string, string> { { "sha", mod.SHA }, { "name", mod.Name } },
                $"Failed to delete mod from database: {mod.SHA}"
            );
        }

        _logger.Info($"Successfully deleted mod from database: {mod.SHA}", "ModDeletionService");
    }

    /// <summary>
    /// Batch delete multiple mods
    /// Processes all deletions and returns summary of results
    /// Emits events for each successful deletion
    /// </summary>
    public async Task<BatchDeleteResult> BatchDeleteAsync(List<string> shas)
    {
        var result = new BatchDeleteResult();

        if (shas == null || shas.Count == 0)
        {
            return result;
        }

        _logger.Info($"Starting batch deletion for {shas.Count} mods", "ModDeletionService");

        foreach (var sha in shas)
        {
            try
            {
                var success = await DeleteAsync(sha).ConfigureAwait(false);
                if (success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailedCount++;
                    result.FailedShas.Add(sha);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting mod {sha}: {ex.Message}", "ModDeletionService", ex);
                result.FailedCount++;
                result.FailedShas.Add(sha);
            }
        }

        _logger.Info($"Batch deletion completed: {result.SuccessCount} succeeded, {result.FailedCount} failed", "ModDeletionService");

        return result;
    }
}
