using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python mod index files
/// Parses index_*.json files containing mod metadata
/// </summary>
public interface IPythonModIndexParser
{
    /// <summary>
    /// Parse mod index directory containing index_*.json files
    /// </summary>
    /// <param name="modsIndexDirectory">Path to modsIndex directory (e.g., home/Endfield/modsIndex)</param>
    /// <returns>List of mod entries with metadata (deduplicated by SHA)</returns>
    Task<List<PythonModEntry>> ParseAsync(string modsIndexDirectory);
}

/// <summary>
/// Implementation of Python mod index parser
/// Reads and parses index_*.json files
/// </summary>
public class PythonModIndexParser : IPythonModIndexParser
{
    private readonly ILogHelper _logger;

    public PythonModIndexParser(ILogHelper logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse all index_*.json files in the directory
    /// Deduplicates mods by SHA
    /// </summary>
    public async Task<List<PythonModEntry>> ParseAsync(string modsIndexDirectory)
    {
        var allMods = new List<PythonModEntry>();

        if (!Directory.Exists(modsIndexDirectory))
        {
            _logger.Warn($"ModsIndex directory not found: {modsIndexDirectory}", "PythonModIndexParser");
            return allMods;
        }

        // Find all JSON files in modsIndex directory
        var indexFiles = Directory.GetFiles(modsIndexDirectory, "*.json");
        _logger.Info($"Found {indexFiles.Length} mod index files", "PythonModIndexParser");

        foreach (var indexFile in indexFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(indexFile).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("mods", out var modsElement) || modsElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.Warn($"No 'mods' object in {Path.GetFileName(indexFile)}", "PythonModIndexParser");
                    continue;
                }

                // Parse each mod entry
                int modCount = 0;
                foreach (var prop in modsElement.EnumerateObject())
                {
                    var sha = prop.Name;
                    var modData = prop.Value;

                    var entry = new PythonModEntry
                    {
                        Sha = sha,
                        Object = modData.TryGetProperty("object", out var objProp) ? objProp.GetString() ?? "Unknown" : "Unknown",
                        Type = modData.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "7z" : "7z",
                        Name = modData.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Unknown" : "Unknown",
                        Author = modData.TryGetProperty("author", out var authorProp) ? authorProp.GetString() ?? "" : "",
                        Grading = modData.TryGetProperty("grading", out var gradingProp) ? gradingProp.GetString() ?? "G" : "G",
                        Explain = modData.TryGetProperty("explain", out var explainProp) ? explainProp.GetString() ?? "" : "",
                        Tags = modData.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array
                            ? JsonSerializer.Deserialize<List<string>>(tagsProp.GetRawText()) ?? new List<string>()
                            : new List<string>()
                    };

                    // Deduplicate by SHA
                    if (!allMods.Any(m => m.Sha == sha))
                    {
                        allMods.Add(entry);
                        modCount++;
                    }
                }

                _logger.Info($"Parsed {Path.GetFileName(indexFile)}: {modCount} mods", "PythonModIndexParser");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse {Path.GetFileName(indexFile)}: {ex.Message}", "PythonModIndexParser");
            }
        }

        _logger.Info($"Parsed total: {allMods.Count} unique mods", "PythonModIndexParser");
        return allMods;
    }
}
