using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for mod auto-detection service
/// Auto-detects classification based on mod file patterns using database-stored rules
/// </summary>
public interface IModAutoDetectionService
{
    Task<string?> DetectClassificationAsync(string modDirectory);
}

/// <summary>
/// Service for auto-detecting classifications using file pattern matching
/// Uses MatchMode and MatchPattern from ClassificationNode database records
/// Responsibility: Scan mod files and match against classification patterns
/// </summary>
public class ModAutoDetectionService : IModAutoDetectionService
{
    private readonly IClassificationRepository _classificationRepository;
    private readonly ILogHelper _logger;

    public ModAutoDetectionService(IClassificationRepository classificationRepository, ILogHelper logger)
    {
        _classificationRepository = classificationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Auto-detect classification from mod folder contents
    /// Matches files against MatchPattern in classification nodes (sorted by Priority)
    /// </summary>
    public async Task<string?> DetectClassificationAsync(string modDirectory)
    {
        if (!Directory.Exists(modDirectory))
            return null;

        // Get all files in directory (recursive)
        var files = Directory.GetFiles(modDirectory, "*", SearchOption.AllDirectories);

        // Get all classifications with patterns (ordered by priority descending)
        var allNodes = await _classificationRepository.GetAllAsync().ConfigureAwait(false);
        var nodesWithPatterns = allNodes
            .Where(n => !string.IsNullOrEmpty(n.MatchPattern))
            .OrderByDescending(n => n.Priority)
            .ToList();

        // Check each classification pattern
        foreach (var node in nodesWithPatterns)
        {
            var matchMode = node.MatchMode ?? "Wildcard"; // Default to Wildcard for legacy compatibility
            var pattern = node.MatchPattern!;

            try
            {
                // Create regex based on match mode
                Regex regex;
                if (matchMode.Equals("Regex", StringComparison.OrdinalIgnoreCase))
                {
                    regex = new Regex(pattern, RegexOptions.IgnoreCase);
                }
                else // Wildcard mode (default)
                {
                    var regexPattern = WildcardToRegex(pattern);
                    regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                }

                // Check if any file matches the pattern
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(modDirectory, file);
                    if (regex.IsMatch(relativePath))
                    {
                        _logger.Debug($"Matched classification '{node.Name}' (ID: {node.Id}): {relativePath} -> Pattern: {pattern}", "ModAutoDetectionService");
                        return node.Id; // Return classification ID
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Invalid pattern in classification '{node.Name}': {pattern} - {ex.Message}", "ModAutoDetectionService");
            }
        }

        // No match found
        return null;
    }

    /// <summary>
    /// Convert Unix-style wildcard pattern to regex
    /// </summary>
    private string WildcardToRegex(string pattern)
    {
        // Escape special regex characters except * and ?
        var escaped = Regex.Escape(pattern);

        // Replace escaped wildcards with regex equivalents
        escaped = escaped.Replace(@"\*", ".*");  // * matches any characters
        escaped = escaped.Replace(@"\?", ".");   // ? matches single character

        // Match full string
        return "^" + escaped + "$";
    }
}
