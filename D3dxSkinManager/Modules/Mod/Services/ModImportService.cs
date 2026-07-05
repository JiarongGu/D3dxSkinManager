using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod import service
/// </summary>
public interface IModImportService
{
    Task<ModInfo?> ImportAsync(string filePath);

    /// <summary>
    /// Replace an EXISTING mod's content with a new archive/file, keeping the same id and all metadata
    /// (name/category/tags/previews). Caches are invalidated so the next load extracts the new content.
    /// </summary>
    Task<ModInfo?> UpdateModAsync(string id, string filePath);

    Task<int> ScanAndImportPreviewsFromFolderAsync(string id, string folderPath);
}

/// <summary>
/// Service for importing new mods
/// Responsibility: Import workflow coordination (generate ID, extract, classify, generate images, save)
/// </summary>
public class ModImportService : IModImportService
{
    private readonly IFileHelper _fileService;
    private readonly IImageService _imageService;
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModCacheService _cacheService;
    private readonly IModMetadataService _metadataService;
    private readonly IPathValidator _pathValidator;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModImportService(
        IFileHelper fileService,
        IImageService imageService,
        IModRepository repository,
        IModArchiveService archiveService,
        IModCacheService cacheService,
        IModMetadataService metadataService,
        IPathValidator pathValidator,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _fileService = fileService;
        _imageService = imageService;
        _repository = repository;
        _archiveService = archiveService;
        _cacheService = cacheService;
        _metadataService = metadataService;
        _pathValidator = pathValidator;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Import a mod from a file
    /// </summary>
    public async Task<ModInfo?> ImportAsync(string filePath)
    {
        _pathValidator.ValidateFileExists(filePath);

        try
        {
            _logger.Info($"Starting import: {filePath}", "ModImportService");

            // Step 1: Generate unique GUID-based ID
            var id = ModInfo.NewId();
            _logger.Info($"Generated ID: {id}", "ModImportService");

            // Check if already exists (should never happen with GUID, but check anyway)
            if (await _repository.ExistsAsync(id))
            {
                _logger.Info($"Mod already exists: {id}", "ModImportService");
                var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
                return entity != null ? ModMapper.ToDomain(entity) : null;
            }

            // Step 2: Copy archive to mods directory
            await _archiveService.CopyArchiveAsync(filePath, id).ConfigureAwait(false);

            try
            {
                // Step 3: Try to scan for preview images from cache directory
                // This will look in common cache locations for matching images
                try
                {
                    var previewCount = await _imageService.TryAutoImportPreviewsFromCacheAsync(id).ConfigureAwait(false);
                    if (previewCount > 0)
                    {
                        _logger.Info($"Auto-imported {previewCount} preview(s) from cache", "ModImportService");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Info($"Failed to auto-import previews from cache: {ex.Message}", "ModImportService");
                }

                // Step 4: Create ModInfo with default values (user can edit later)
                var createRequest = new CreateModRequest
                {
                    Id = id,
                    Category = null, // User will categorize manually
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Author = null, // User can add later
                    Description = null, // User can add later
                    Type = Path.GetExtension(filePath).TrimStart('.'),
                    Grading = "G", // Default to General
                    Tags = new List<string>()
                };

                var mod = await _metadataService.CreateAsync(createRequest).ConfigureAwait(false);
                _logger.Info($"Import complete: {mod.Name} ({id})", "ModImportService");

                // Emit IMPORTED event
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.IMPORTED, mod).ConfigureAwait(false);

                return mod;
            }
            catch
            {
                // The archive was already copied but the mod has no DB row — without rollback it
                // would sit invisible until a cleanup-tool orphan scan. Best-effort undo.
                await RollbackImportAsync(id).ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Info($"Import failed: {ex.Message}", "ModImportService");
            throw;
        }
    }

    /// <summary>
    /// Best-effort rollback of a partial import (archive copied, DB row never created): delete the
    /// copied archive (planner-serialized) and any auto-imported previews. Failures are only logged —
    /// the file-cleanup tool's orphan scan remains the safety net.
    /// </summary>
    private async Task RollbackImportAsync(string id)
    {
        try
        {
            await _archiveService.DeleteArchiveAsync(id).ConfigureAwait(false);
            foreach (var preview in await _imageService.GetPreviewPathsAsync(id).ConfigureAwait(false))
            {
                await _imageService.DeletePreviewAsync(id, preview).ConfigureAwait(false);
            }
            _logger.Info($"Rolled back partial import: {id}", "ModImportService");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Import rollback incomplete for {id}: {ex.Message} (the cleanup tool's orphan scan will catch leftovers)", "ModImportService");
        }
    }

    /// <summary>
    /// Replace an existing mod's archive with new content, keeping the same id + metadata. (#14)
    /// Mods are stored compressed and only extracted on load, so updating = overwrite the archive +
    /// invalidate the cache; the new content is extracted on the next load (see #9 staleness check).
    /// </summary>
    public async Task<ModInfo?> UpdateModAsync(string id, string filePath)
    {
        _pathValidator.ValidateFileExists(filePath);

        var entity = await _repository.GetByIdAsync(id).ConfigureAwait(false);
        if (entity == null)
        {
            throw new OperationException(
                ErrorCodes.MOD_NOT_FOUND,
                new Dictionary<string, string> { { "id", id } },
                $"Mod not found: {id}");
        }

        _logger.Info($"Updating mod '{entity.Name}' ({id}) from: {filePath}", "ModImportService");

        // 1. Overwrite the compressed archive in place (planner-serialized, Overwrite=true).
        await _archiveService.CopyArchiveAsync(filePath, id).ConfigureAwait(false);

        // 2. Invalidate caches (active + disabled) so the next load extracts the new content.
        await _cacheService.DeleteCacheAsync(id).ConfigureAwait(false);

        var mod = ModMapper.ToDomain(entity);

        // Refresh previews from the new content's cache locations (best-effort).
        try { await _imageService.TryAutoImportPreviewsFromCacheAsync(id).ConfigureAwait(false); }
        catch (Exception ex) { _logger.Info($"Preview re-scan after update failed: {ex.Message}", "ModImportService"); }

        // Emit IMPORTED so the frontend refreshes the mod list (now unloaded + new content).
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.IMPORTED, mod).ConfigureAwait(false);
        _logger.Info($"Update complete: {entity.Name} ({id})", "ModImportService");
        return mod;
    }

    /// <summary>
    /// Scan a folder for preview images and import them for a mod
    /// This is used during mod import workflow to auto-import previews from the source folder
    /// Uses the same logic as ScanAndImportFromCacheAsync but for the original folder
    /// </summary>
    public async Task<int> ScanAndImportPreviewsFromFolderAsync(string id, string folderPath)
    {
        if (!_fileService.DirectoryExists(folderPath))
        {
            _logger.Warn($"Folder does not exist for preview import: {folderPath}", "ModImportService");
            return 0;
        }

        try
        {
            // Delegate to ImageService which handles the actual scanning and importing
            // This reuses the existing ScanAndImportFromCacheAsync logic with ID-based deduplication
            var importCount = await _imageService.ScanAndImportFromCacheAsync(id, folderPath).ConfigureAwait(false);

            if (importCount > 0)
            {
                _logger.Info($"Imported {importCount} preview image(s) from folder: {folderPath}", "ModImportService");

                // Emit PREVIEW_IMPORTED event to notify frontend
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new { id, source = "folder" }).ConfigureAwait(false);
            }

            return importCount;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to scan and import previews from folder: {ex.Message}", "ModImportService", ex);
            return 0;
        }
    }
}
