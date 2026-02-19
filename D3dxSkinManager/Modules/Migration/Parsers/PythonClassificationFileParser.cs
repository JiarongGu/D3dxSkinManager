using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python d3dxSkinManage classification files
/// Used for Python-to-React migration
/// Each file in the classification directory represents a category
/// Each line in the file is an object name belonging to that category
/// </summary>
public interface IPythonClassificationFileParser
{
    /// <summary>
    /// Parse Python classification directory containing text files
    /// </summary>
    /// <param name="classificationDirectory">Path to classification directory (e.g., home/Endfield/classification)</param>
    /// <returns>Dictionary of categoryName → List of objectNames</returns>
    Task<Dictionary<string, List<string>>> ParseAsync(string classificationDirectory);

    /// <summary>
    /// Get statistics about Python classification files
    /// </summary>
    Task<PythonClassificationStatistics> GetStatisticsAsync(string classificationDirectory);
}

/// <summary>
/// Implementation of Python classification file parser
/// Reads text files from Python installation where each line is an object name
/// </summary>
public class PythonClassificationFileParser : IPythonClassificationFileParser
{
    private readonly ILogHelper _logger;

    public PythonClassificationFileParser(ILogHelper logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse classification directory
    /// File name becomes category name, each line becomes an object name
    /// </summary>
    public async Task<Dictionary<string, List<string>>> ParseAsync(string classificationDirectory)
    {
        var result = new Dictionary<string, List<string>>();

        if (!Directory.Exists(classificationDirectory))
        {
            _logger.Warning($"Python classification directory not found: {classificationDirectory}", "PythonClassificationFileParser");
            return result;
        }

        var files = Directory.GetFiles(classificationDirectory);
        _logger.Info($"Found {files.Length} Python classification files", "PythonClassificationFileParser");

        foreach (var file in files)
        {
            try
            {
                var categoryName = Path.GetFileName(file);
                var lines = await File.ReadAllLinesAsync(file);

                // Parse each line as an object name
                var objectNames = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();

                result[categoryName] = objectNames;

                _logger.Info($"Parsed Python category '{categoryName}': {objectNames.Count} objects", "PythonClassificationFileParser");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", "PythonClassificationFileParser");
            }
        }

        return result;
    }

    /// <summary>
    /// Get statistics about Python classification files
    /// </summary>
    public async Task<PythonClassificationStatistics> GetStatisticsAsync(string classificationDirectory)
    {
        var stats = new PythonClassificationStatistics();

        if (!Directory.Exists(classificationDirectory))
            return stats;

        var files = Directory.GetFiles(classificationDirectory);
        stats.TotalFiles = files.Length;

        foreach (var file in files)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file);
                var objectCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));
                stats.TotalObjects += objectCount;
            }
            catch
            {
                // Ignore errors during statistics gathering
            }
        }

        return stats;
    }
}

/// <summary>
/// Statistics about Python classification files
/// </summary>
public class PythonClassificationStatistics
{
    public int TotalFiles { get; set; }
    public int TotalObjects { get; set; }

    public override string ToString()
    {
        return $"{TotalFiles} categories, {TotalObjects} objects";
    }
}
