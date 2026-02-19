using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using Newtonsoft.Json.Linq;

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
            _logger.Warning($"ModsIndex directory not found: {modsIndexDirectory}", "PythonModIndexParser");
            return allMods;
        }

        // Find all index_*.json files
        var indexFiles = Directory.GetFiles(modsIndexDirectory, "index_*.json");
        _logger.Info($"Found {indexFiles.Length} mod index files", "PythonModIndexParser");

        foreach (var indexFile in indexFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(indexFile);
                var doc = JObject.Parse(json);
                var modsObj = doc["mods"] as JObject;

                if (modsObj == null)
                {
                    _logger.Warning($"No 'mods' object in {Path.GetFileName(indexFile)}", "PythonModIndexParser");
                    continue;
                }

                // Parse each mod entry
                foreach (var prop in modsObj.Properties())
                {
                    var sha = prop.Name;
                    var modData = prop.Value;

                    var entry = new PythonModEntry
                    {
                        Sha = sha,
                        Object = modData["object"]?.ToString() ?? "Unknown",
                        Type = modData["type"]?.ToString() ?? "7z",
                        Name = modData["name"]?.ToString() ?? "Unknown",
                        Author = modData["author"]?.ToString() ?? "",
                        Grading = modData["grading"]?.ToString() ?? "G",
                        Explain = modData["explain"]?.ToString() ?? "",
                        Tags = modData["tags"]?.ToObject<List<string>>() ?? new List<string>()
                    };

                    // Deduplicate by SHA
                    if (!allMods.Any(m => m.Sha == sha))
                    {
                        allMods.Add(entry);
                    }
                }

                _logger.Info($"Parsed {Path.GetFileName(indexFile)}: {modsObj.Properties().Count()} mods", "PythonModIndexParser");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to parse {Path.GetFileName(indexFile)}: {ex.Message}", "PythonModIndexParser");
            }
        }

        _logger.Info($"Parsed total: {allMods.Count} unique mods", "PythonModIndexParser");
        return allMods;
    }
}
