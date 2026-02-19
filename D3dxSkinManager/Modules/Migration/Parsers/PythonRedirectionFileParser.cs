using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python d3dxSkinManage _redirection.ini files
/// Used for Python-to-React migration
/// Format:
/// - [*] folder\path\* - Folder declarations (load all images from folder)
/// - characterName = folder\path\thumbnail.png - Explicit character thumbnail mappings
/// </summary>
public interface IPythonRedirectionFileParser
{
    /// <summary>
    /// Parse Python _redirection.ini file and extract character-thumbnail mappings
    /// </summary>
    /// <param name="redirectionFilePath">Full path to _redirection.ini file</param>
    /// <returns>Dictionary of character names to thumbnail relative paths</returns>
    Task<Dictionary<string, string>> ParseAsync(string redirectionFilePath);

    /// <summary>
    /// Get statistics about Python _redirection.ini file
    /// </summary>
    Task<PythonRedirectionFileStatistics> GetStatisticsAsync(string redirectionFilePath);
}

/// <summary>
/// Implementation of Python _redirection.ini parser
/// Handles both explicit mappings and directory wildcards
/// </summary>
public class PythonRedirectionFileParser : IPythonRedirectionFileParser
{
    private readonly IImageService _imageService;
    private readonly ILogHelper _logger;

    public PythonRedirectionFileParser(
        IImageService imageService,
        ILogHelper logger)
    {
        _imageService = imageService;
        _logger = logger;
    }

    /// <summary>
    /// Parse _redirection.ini file and extract character-thumbnail mappings
    /// Handles both explicit mappings (=) and directory wildcards ([*])
    /// </summary>
    public async Task<Dictionary<string, string>> ParseAsync(string redirectionFilePath)
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
                ParseFolderDeclaration(trimmed, baseDir, mappings);
            }
            // Handle explicit character thumbnail mappings: 管理员 = 终末地\干员-物理\管理员.png
            else if (trimmed.Contains("="))
            {
                ParseExplicitMapping(trimmed, mappings);
            }
        }

        return mappings;
    }

    /// <summary>
    /// Parse folder wildcard declaration and add all images from that folder
    /// </summary>
    private void ParseFolderDeclaration(string line, string baseDir, Dictionary<string, string> mappings)
    {
        var folderPath = line.Substring(3, line.Length - 5).Trim(); // Remove "[*] " and "\*"
        var fullFolderPath = Path.Combine(baseDir, folderPath);

        if (!Directory.Exists(fullFolderPath))
        {
            _logger.Warning($"Directory not found: {fullFolderPath}", "PythonRedirectionFileParser");
            return;
        }

        // Load all image files from this directory using IImageService
        var supportedExtensions = _imageService.GetSupportedImageExtensions();
        var imageFiles = Directory.GetFiles(fullFolderPath, "*.*")
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

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

        _logger.Info($"Loaded {imageFiles.Count()} thumbnails from: {folderPath}", "PythonRedirectionFileParser");
    }

    /// <summary>
    /// Parse explicit character thumbnail mapping
    /// </summary>
    private void ParseExplicitMapping(string line, Dictionary<string, string> mappings)
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            var characterName = parts[0].Trim();
            var thumbnailRelativePath = parts[1].Trim();

            // Explicit mappings override directory wildcards
            mappings[characterName] = thumbnailRelativePath;
        }
    }

    /// <summary>
    /// Get statistics about Python _redirection.ini file
    /// Useful for logging and validation
    /// </summary>
    public async Task<PythonRedirectionFileStatistics> GetStatisticsAsync(string redirectionFilePath)
    {
        var lines = await File.ReadAllLinesAsync(redirectionFilePath);
        var stats = new PythonRedirectionFileStatistics
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
/// Statistics about a Python _redirection.ini file
/// </summary>
public class PythonRedirectionFileStatistics
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
