using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod import service
/// </summary>
public interface IModImportService
{
    Task<ModInfo?> ImportAsync(string filePath);
    Task<int> ScanAndImportPreviewsFromFolderAsync(string sha, string folderPath);
}

/// <summary>
/// Service for importing new mods
/// Responsibility: Import workflow coordination (hash, extract, classify, generate images, save)
/// </summary>
public class ModImportService : IModImportService
{
    private readonly IFileHelper _fileService;
    private readonly IHashHelper _hashHelper;
    private readonly IImageService _imageService;
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModMetadataService _metadataService;
    private readonly IPathValidator _pathValidator;
    private readonly ILogHelper _logger;
    private readonly IProfileEventBus _eventBus;

    public ModImportService(
        IFileHelper fileService,
        IHashHelper hashHelper,
        IImageService imageService,
        IModRepository repository,
        IModArchiveService archiveService,
        IModMetadataService metadataService,
        IPathValidator pathValidator,
        ILogHelper logger,
        IProfileEventBus eventBus)
    {
        _fileService = fileService;
        _hashHelper = hashHelper;
        _imageService = imageService;
        _repository = repository;
        _archiveService = archiveService;
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

            // Step 1: Calculate SHA256
            var sha = await _hashHelper.CalculateFileSHA256Async(filePath).ConfigureAwait(false);
            _logger.Info($"SHA256: {sha}", "ModImportService");

            // Check if already exists
            if (await _repository.ExistsAsync(sha))
            {
                _logger.Info($"Mod already exists: {sha}", "ModImportService");
                var entity = await _repository.GetByIdAsync(sha).ConfigureAwait(false);
                return entity != null ? ModMapper.ToDomain(entity) : null;
            }

            // Step 2: Copy archive to mods directory
            await _archiveService.CopyArchiveAsync(filePath, sha).ConfigureAwait(false);

            // Step 3: Try to scan for preview images from cache directory
            // This will look in common cache locations for matching images
            try
            {
                var previewCount = await _imageService.TryAutoImportPreviewsFromCacheAsync(sha).ConfigureAwait(false);
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
                SHA = sha,
                Category = null, // User will categorize manually
                Name = Path.GetFileNameWithoutExtension(filePath),
                Author = null, // User can add later
                Description = null, // User can add later
                Type = Path.GetExtension(filePath).TrimStart('.'),
                Grading = "G", // Default to General
                Tags = new List<string>()
            };

            var mod = await _metadataService.CreateAsync(createRequest).ConfigureAwait(false);
            _logger.Info($"Import complete: {mod.Name} ({sha})", "ModImportService");

            // Emit IMPORTED event
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.IMPORTED, mod).ConfigureAwait(false);

            return mod;
        }
        catch (Exception ex)
        {
            _logger.Info($"Import failed: {ex.Message}", "ModImportService");
            throw;
        }
    }

    /// <summary>
    /// Scan a folder for preview images and import them for a mod
    /// This is used during mod import workflow to auto-import previews from the source folder
    /// Uses the same logic as ScanAndImportFromCacheAsync but for the original folder
    /// </summary>
    public async Task<int> ScanAndImportPreviewsFromFolderAsync(string sha, string folderPath)
    {
        if (!_fileService.DirectoryExists(folderPath))
        {
            _logger.Warn($"Folder does not exist for preview import: {folderPath}", "ModImportService");
            return 0;
        }

        try
        {
            // Delegate to ImageService which handles the actual scanning and importing
            // This reuses the existing ScanAndImportFromCacheAsync logic with SHA-based deduplication
            var importCount = await _imageService.ScanAndImportFromCacheAsync(sha, folderPath).ConfigureAwait(false);

            if (importCount > 0)
            {
                _logger.Info($"Imported {importCount} preview image(s) from folder: {folderPath}", "ModImportService");

                // Emit PREVIEW_IMPORTED event to notify frontend
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new { sha, source = "folder" }).ConfigureAwait(false);
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
