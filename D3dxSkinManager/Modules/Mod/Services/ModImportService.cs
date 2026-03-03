using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Interface for mod import service
/// </summary>
public interface IModImportService
{
    Task<ModInfo?> ImportAsync(string filePath);
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
    private readonly IModFileService _modFileService;
    private readonly IModManagementService _modManagementService;
    private readonly IPathValidator _pathValidator;
    private readonly ILogHelper _logger;

    public ModImportService(
        IFileHelper fileService,
        IHashHelper hashHelper,
        IImageService imageService,
        IModRepository repository,
        IModFileService modFileService,
        IModManagementService modManagementService,
        IPathValidator pathValidator,
        ILogHelper logger)
    {
        _fileService = fileService;
        _hashHelper = hashHelper;
        _imageService = imageService;
        _repository = repository;
        _modFileService = modFileService;
        _modManagementService = modManagementService;
        _pathValidator = pathValidator;
        _logger = logger;
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
                return await _repository.GetByIdAsync(sha).ConfigureAwait(false);
            }

            // Step 2: Copy archive to mods directory
            await _modFileService.CopyArchiveAsync(filePath, sha).ConfigureAwait(false);

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

            var mod = await _modManagementService.CreateModAsync(createRequest).ConfigureAwait(false);
            _logger.Info($"Import complete: {mod.Name} ({sha})", "ModImportService");

            return mod;
        }
        catch (Exception ex)
        {
            _logger.Info($"Import failed: {ex.Message}", "ModImportService");
            throw;
        }
    }
}
