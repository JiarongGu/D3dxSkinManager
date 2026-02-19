using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Tools.Services;

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
    private readonly IFileService _fileService;
    private readonly IModAutoDetectionService _autoDetectionService;
    private readonly IImageService _imageService;
    private readonly IModRepository _repository;
    private readonly IModFileService _modFileService;
    private readonly IModManagementService _modManagementService;

    public ModImportService(
        IFileService fileService,
        IModAutoDetectionService autoDetectionService,
        IImageService imageService,
        IModRepository repository,
        IModFileService modFileService,
        IModManagementService modManagementService)
    {
        _fileService = fileService;
        _autoDetectionService = autoDetectionService;
        _imageService = imageService;
        _repository = repository;
        _modFileService = modFileService;
        _modManagementService = modManagementService;
    }

    /// <summary>
    /// Import a mod from a file
    /// </summary>
    public async Task<ModInfo?> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        try
        {
            Console.WriteLine($"[ModImport] Starting import: {filePath}");

            // Step 1: Calculate SHA256
            var sha = await _fileService.CalculateSha256Async(filePath);
            Console.WriteLine($"[ModImport] SHA256: {sha}");

            // Check if already exists
            if (await _repository.ExistsAsync(sha))
            {
                Console.WriteLine($"[ModImport] Mod already exists: {sha}");
                return await _repository.GetByIdAsync(sha);
            }

            // Step 2: Copy archive to mods directory
            await _modFileService.CopyArchiveAsync(filePath, sha);

            // Step 3: Extract to temporary directory for metadata reading
            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"mod_import_{sha}");
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
            }

            var extracted = await _fileService.ExtractArchiveAsync(
                _modFileService.GetArchivePath(sha),
                tempExtractPath
            );

            if (!extracted)
            {
                throw new Exception("Failed to extract archive");
            }

            // Step 4: Read metadata
            var metadata = await ReadMetadataAsync(tempExtractPath);
            Console.WriteLine($"[ModImport] Metadata: Name={metadata.Name}, Author={metadata.Author}");

            // Step 5: Auto-classify object name if not provided
            var category = metadata.Category;
            if (string.IsNullOrEmpty(category))
            {
                category = await _autoDetectionService.DetectObjectNameAsync(tempExtractPath);
                Console.WriteLine($"[ModImport] Auto-detected as: {category ?? "Unknown"}");
            }

            // Step 6: Generate thumbnail and previews
            string? thumbnailPath = null;

            try
            {
                thumbnailPath = await _imageService.GenerateThumbnailAsync(tempExtractPath, sha);
                Console.WriteLine($"[ModImport] Generated thumbnail: {thumbnailPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModImport] Failed to generate thumbnail: {ex.Message}");
            }

            try
            {
                var previewCount = await _imageService.GeneratePreviewsAsync(tempExtractPath, sha);
                Console.WriteLine($"[ModImport] Generated {previewCount} preview(s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModImport] Failed to generate previews: {ex.Message}");
            }

            // Step 7 & 8: Create and save ModInfo using centralized service
            var createRequest = new CreateModRequest
            {
                SHA = sha,
                Category = category ?? "Unknown",
                Name = metadata.Name ?? Path.GetFileNameWithoutExtension(filePath),
                Author = metadata.Author,
                Description = metadata.Description,
                Type = Path.GetExtension(filePath).TrimStart('.'),
                Grading = metadata.Grading ?? "G",
                Tags = metadata.Tags ?? new List<string>(),
                IsLoaded = false,
                IsAvailable = true,
                ThumbnailPath = thumbnailPath
            };

            var mod = await _modManagementService.CreateModAsync(createRequest);
            Console.WriteLine($"[ModImport] Import complete: {mod.Name} ({sha})");

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
                Console.WriteLine($"[ModImport] Failed to cleanup temp directory: {ex.Message}");
            }

            return mod;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModImport] Import failed: {ex.Message}");
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
                var json = await File.ReadAllTextAsync(metadataPath);
                var parsedMetadata = JsonConvert.DeserializeObject<ModMetadata>(json);
                if (parsedMetadata != null)
                {
                    return parsedMetadata;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModImport] Failed to parse metadata.json: {ex.Message}");
            }
        }

        // Look for README or similar files to extract info
        var readmeFiles = Directory.GetFiles(modDirectory, "readme*", SearchOption.TopDirectoryOnly);
        if (readmeFiles.Length > 0)
        {
            try
            {
                var content = await File.ReadAllTextAsync(readmeFiles[0]);
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
