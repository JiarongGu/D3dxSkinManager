using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mods.Services;

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
    private readonly IModAutoDetectionService _autoDetectionService;
    private readonly IImageService _imageService;
    private readonly IModRepository _repository;
    private readonly IModFileService _modFileService;
    private readonly IModManagementService _modManagementService;
    private readonly IPathValidator _pathValidator;
    private readonly IArchiveHelper _archiveService;
    private readonly ILogHelper _logger;

    public ModImportService(
        IFileHelper fileService,
        IModAutoDetectionService autoDetectionService,
        IImageService imageService,
        IModRepository repository,
        IModFileService modFileService,
        IModManagementService modManagementService,
        IPathValidator pathValidator,
        IArchiveHelper archiveService,
        ILogHelper logger)
    {
        _fileService = fileService;
        _autoDetectionService = autoDetectionService;
        _imageService = imageService;
        _repository = repository;
        _modFileService = modFileService;
        _modManagementService = modManagementService;
        _pathValidator = pathValidator;
        _archiveService = archiveService;
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
            var sha = await _fileService.CalculateSha256Async(filePath).ConfigureAwait(false);
            _logger.Info($"SHA256: {sha}", "ModImportService");

            // Check if already exists
            if (await _repository.ExistsAsync(sha))
            {
                _logger.Info($"Mod already exists: {sha}", "ModImportService");
                return await _repository.GetByIdAsync(sha).ConfigureAwait(false);
            }

            // Step 2: Copy archive to mods directory
            await _modFileService.CopyArchiveAsync(filePath, sha).ConfigureAwait(false);

            // Step 3: Extract to temporary directory for metadata reading
            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"mod_import_{sha}");
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
            }

            var result = await _archiveService.ExtractArchiveAsync(
                _modFileService.GetArchivePath(sha),
                tempExtractPath
            );

            if (!result.Success)
            {
                throw new Exception("Failed to extract archive");
            }

            // Step 4: Read metadata
            var metadata = await ReadMetadataAsync(tempExtractPath).ConfigureAwait(false);
            _logger.Info($"Metadata: Name={metadata.Name}, Author={metadata.Author}", "ModImportService");

            // Step 5: Auto-classify object name if not provided
            var category = metadata.Category;
            if (string.IsNullOrEmpty(category))
            {
                category = await _autoDetectionService.DetectObjectNameAsync(tempExtractPath).ConfigureAwait(false);
                _logger.Info($"Auto-detected as: {category ?? "Unknown"}", "ModImportService");
            }

            // Step 6: Generate previews
            try
            {
                var previewCount = await _imageService.GeneratePreviewsAsync(tempExtractPath, sha).ConfigureAwait(false);
                _logger.Info($"Generated {previewCount} preview(s)", "ModImportService");
            }
            catch (Exception ex)
            {
                _logger.Info($"Failed to generate previews: {ex.Message}", "ModImportService");
            }

            // Step 7 & 8: Create and save ModInfo using centralized service
            var createRequest = new CreateModRequest
            {
                SHA = sha,
                Category = category,
                Name = metadata.Name ?? Path.GetFileNameWithoutExtension(filePath),
                Author = metadata.Author,
                Description = metadata.Description,
                Type = Path.GetExtension(filePath).TrimStart('.'),
                Grading = metadata.Grading ?? "G",
                Tags = metadata.Tags ?? new List<string>()
            };

            var mod = await _modManagementService.CreateModAsync(createRequest).ConfigureAwait(false);
            _logger.Info($"Import complete: {mod.Name} ({sha})", "ModImportService");

            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"Failed to cleanup temp directory: {ex.Message}", "ModImportService");
            }

            return mod;
        }
        catch (Exception ex)
        {
            _logger.Info($"Import failed: {ex.Message}", "ModImportService");
            throw;
        }
    }

    /// <summary>
    /// Read metadata from extracted mod directory
    /// </summary>
    private async Task<ModMetadata> ReadMetadataAsync(string modDirectory)
    {
        var metadata = new ModMetadata();

        // Look for metadata.json
        var metadataPath = Path.Combine(modDirectory, "metadata.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(metadataPath).ConfigureAwait(false);
                var parsedMetadata = JsonHelper.Deserialize<ModMetadata>(json);
                if (parsedMetadata != null)
                {
                    return parsedMetadata;
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"Failed to parse metadata.json: {ex.Message}", "ModImportService");
            }
        }

        // Look for README or similar files to extract info
        var readmeFiles = Directory.GetFiles(modDirectory, "readme*", SearchOption.TopDirectoryOnly);
        if (readmeFiles.Length > 0)
        {
            try
            {
                var content = await File.ReadAllTextAsync(readmeFiles[0]).ConfigureAwait(false);
                // Try to extract author from "Author:", "By:", etc.
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("By:", StringComparison.OrdinalIgnoreCase))
                    {
                        metadata.Author = line.Split(':', 2)[1].Trim();
                        break;
                    }
                }
            }
            catch
            {
                // Ignore errors reading README
            }
        }

        return metadata;
    }
}
