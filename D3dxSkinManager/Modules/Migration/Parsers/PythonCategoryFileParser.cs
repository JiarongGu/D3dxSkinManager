using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python d3dxSkinManage Category files
/// Used for Python-to-React migration
/// Each file in the Category directory represents a category
/// Each line in the file is an object name belonging to that category
/// </summary>
public interface IPythonCategoryFileParser
{
    /// <summary>
    /// Parse Python Category directory containing text files
    /// </summary>
    /// <param name="categoryDirectory">Path to Category directory (e.g., home/Endfield/Category)</param>
    /// <returns>Dictionary of categoryName List of objectNames</returns>
    Task<Dictionary<string, List<string>>> ParseAsync(string categoryDirectory);

    /// <summary>
    /// Get statistics about Python Category files
    /// </summary>
    Task<PythonCategoryStatistics> GetStatisticsAsync(string categoryDirectory);
}

/// <summary>
/// Implementation of Python Category file parser
/// Reads text files from Python installation where each line is an object name
/// </summary>
public class PythonCategoryFileParser : IPythonCategoryFileParser
{
    private readonly ILogHelper _logger;

    public PythonCategoryFileParser(ILogHelper logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse Category directory
    /// File name becomes category name, each line becomes an object name
    /// </summary>
    public async Task<Dictionary<string, List<string>>> ParseAsync(string categoryDirectory)
    {
        var result = new Dictionary<string, List<string>>();

        if (!Directory.Exists(categoryDirectory))
        {
            _logger.Warn($"Python Category directory not found: {categoryDirectory}", "PythonCategoryFileParser");
            return result;
        }

        var files = Directory.GetFiles(categoryDirectory);
        _logger.Info($"Found {files.Length} Python Category files", "PythonCategoryFileParser");

        foreach (var file in files)
        {
            try
            {
                var categoryName = Path.GetFileName(file);
                var lines = await File.ReadAllLinesAsync(file).ConfigureAwait(false);

                // Parse each line as an object name
                var categoryNames = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();

                result[categoryName] = categoryNames;

                _logger.Info($"Parsed Python category '{categoryName}': {categoryNames.Count}", "PythonCategoryFileParser");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", "PythonCategoryFileParser");
            }
        }

        return result;
    }

    /// <summary>
    /// Get statistics about Python Category files
    /// </summary>
    public async Task<PythonCategoryStatistics> GetStatisticsAsync(string categoryDirectory)
    {
        var stats = new PythonCategoryStatistics();

        if (!Directory.Exists(categoryDirectory))
            return stats;

        var files = Directory.GetFiles(categoryDirectory);
        stats.TotalFiles = files.Length;

        foreach (var file in files)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file).ConfigureAwait(false);
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
/// Statistics about Python Category files
/// </summary>
public class PythonCategoryStatistics
{
    public int TotalFiles { get; set; }
    public int TotalObjects { get; set; }

    public override string ToString()
    {
        return $"{TotalFiles} categories, {TotalObjects} objects";
    }
}
