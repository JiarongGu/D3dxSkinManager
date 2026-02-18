using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Migration.Services;

/// <summary>
/// Service responsible for processing _redirection.ini and associating thumbnails
/// with existing classification nodes during migration
///
/// The _redirection.ini file format:
/// - [*] folder\path\* - Folder declarations (ignored for node creation)
/// - characterName = folder\path\thumbnail.png - Character thumbnail mappings
///
/// Responsibility: Parse thumbnail mappings and update existing classification nodes
/// </summary>
public interface IClassificationThumbnailService
{
    Task<int> AssociateThumbnailsAsync(string redirectionFilePath, string thumbnailsBaseDir);
    Task<RedirectionFileStatistics> GetRedirectionStatisticsAsync(string redirectionFilePath);
}

/// <summary>
/// Implementation of thumbnail association service for classification nodes
/// </summary>
public class ClassificationThumbnailService : IClassificationThumbnailService
{
    private readonly IClassificationRepository _classificationRepository;

    public ClassificationThumbnailService(IClassificationRepository classificationRepository)
    {
        _classificationRepository = classificationRepository ?? throw new ArgumentNullException(nameof(classificationRepository));
    }

    /// <summary>
    /// Parse _redirection.ini and associate thumbnail paths with existing classification nodes
    /// </summary>
    /// <param name="redirectionFilePath">Full path to _redirection.ini file</param>
    /// <param name="thumbnailsBaseDir">Base directory where thumbnails are stored</param>
    /// <returns>Number of thumbnails successfully associated</returns>
    public async Task<int> AssociateThumbnailsAsync(string redirectionFilePath, string thumbnailsBaseDir)
    {
        if (!File.Exists(redirectionFilePath))
        {
            throw new FileNotFoundException($"Redirection file not found: {redirectionFilePath}");
        }

        if (!Directory.Exists(thumbnailsBaseDir))
        {
            throw new DirectoryNotFoundException($"Thumbnails base directory not found: {thumbnailsBaseDir}");
        }

        var mappings = await ParseRedirectionFileAsync(redirectionFilePath);
        var associatedCount = await AssociateMappingsWithNodesAsync(mappings, thumbnailsBaseDir);

        return associatedCount;
    }

    /// <summary>
    /// Parse _redirection.ini file and extract character-thumbnail mappings
    /// Handles both explicit mappings (=) and directory wildcards ([*])
    /// </summary>
    private async Task<Dictionary<string, string>> ParseRedirectionFileAsync(string redirectionFilePath)
    {
        var mappings = new Dictionary<string, string>();
        var lines = await File.ReadAllLinesAsync(redirectionFilePath);
        var baseDir = Path.GetDirectoryName(redirectionFilePath) ?? string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Skip comments
            if (trimmed.StartsWith(";") || trimmed.StartsWith("/") || trimmed.StartsWith("\\"))
                continue;

            // Handle folder wildcard declarations: [*] 终末地\干员-灼热\*
            // This loads ALL images from that directory
            if (trimmed.StartsWith("[*]") && trimmed.EndsWith("\\*"))
            {
                var folderPath = trimmed.Substring(3, trimmed.Length - 5).Trim(); // Remove "[*] " and "\*"
                var fullFolderPath = Path.Combine(baseDir, folderPath);

                if (Directory.Exists(fullFolderPath))
                {
                    // Load all image files from this directory
                    var imageFiles = Directory.GetFiles(fullFolderPath, "*.*")
                        .Where(f => IsImageFile(f));

                    foreach (var imageFile in imageFiles)
                    {
                        // Use filename without extension as the key
                        var fileName = Path.GetFileNameWithoutExtension(imageFile);
                        var relativePath = Path.Combine(folderPath, Path.GetFileName(imageFile));

                        // Don't overwrite explicit mappings
                        if (!mappings.ContainsKey(fileName))
                        {
                            mappings[fileName] = relativePath;
                        }
                    }

                    Console.WriteLine($"[ClassificationThumbnailService] Loaded {imageFiles.Count()} thumbnails from: {folderPath}");
                }
                else
                {
                    Console.WriteLine($"[ClassificationThumbnailService] WARNING: Directory not found: {fullFolderPath}");
                }
            }
            // Handle explicit character thumbnail mappings: 管理员 = 终末地\干员-物理\管理员-女.png
            else if (trimmed.Contains("="))
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    var characterName = parts[0].Trim();
                    var thumbnailRelativePath = parts[1].Trim();

                    // Explicit mappings override directory wildcards
                    mappings[characterName] = thumbnailRelativePath;
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Check if a file is an image based on extension
    /// </summary>
    private bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".gif";
    }

    /// <summary>
    /// Associate parsed thumbnail mappings with existing classification nodes
    /// </summary>
    private async Task<int> AssociateMappingsWithNodesAsync(
        Dictionary<string, string> mappings,
        string thumbnailsBaseDir)
    {
        int associatedCount = 0;

        foreach (var (characterName, thumbnailRelativePath) in mappings)
        {
            // Construct full thumbnail path
            var thumbnailFullPath = Path.Combine(thumbnailsBaseDir, thumbnailRelativePath);

            // Verify thumbnail file exists
            if (!File.Exists(thumbnailFullPath))
            {
                Console.WriteLine($"[ClassificationThumbnailService] WARNING: Thumbnail file not found: {thumbnailFullPath}");
                continue;
            }

            // Try to find existing classification node by character name
            var node = await _classificationRepository.GetByNameAsync(characterName);

            if (node != null)
            {
                // Update node with thumbnail path
                node.Thumbnail = thumbnailFullPath;
                var updated = await _classificationRepository.UpdateAsync(node);

                if (updated)
                {
                    associatedCount++;
                    Console.WriteLine($"[ClassificationThumbnailService] Associated thumbnail for: {characterName}");
                }
            }
            else
            {
                Console.WriteLine($"[ClassificationThumbnailService] INFO: No classification node found for '{characterName}'");
            }
        }

        return associatedCount;
    }

    /// <summary>
    /// Get statistics about _redirection.ini file
    /// Useful for logging and validation
    /// </summary>
    public async Task<RedirectionFileStatistics> GetRedirectionStatisticsAsync(string redirectionFilePath)
    {
        if (!File.Exists(redirectionFilePath))
        {
            throw new FileNotFoundException($"Redirection file not found: {redirectionFilePath}");
        }

        var lines = await File.ReadAllLinesAsync(redirectionFilePath);
        var stats = new RedirectionFileStatistics
        {
            TotalLines = lines.Length
        };

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                stats.EmptyLines++;
                continue;
            }

            if (trimmed.StartsWith("[*]"))
            {
                stats.FolderDeclarations++;
            }
            else if (trimmed.Contains("="))
            {
                stats.CharacterMappings++;
            }
        }

        return stats;
    }
}

/// <summary>
/// Statistics about a _redirection.ini file
/// </summary>
public class RedirectionFileStatistics
{
    public int TotalLines { get; set; }
    public int EmptyLines { get; set; }
    public int FolderDeclarations { get; set; }
    public int CharacterMappings { get; set; }

    public override string ToString()
    {
        return $"Total: {TotalLines}, Folders: {FolderDeclarations}, Characters: {CharacterMappings}, Empty: {EmptyLines}";
    }
}
