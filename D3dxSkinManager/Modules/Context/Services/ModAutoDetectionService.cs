using System.Text.RegularExpressions;
using Newtonsoft.Json;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Rule for auto-detecting object names from mod file patterns
/// </summary>
public class ModAutoDetectionRule
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Interface for mod auto-detection service
/// Auto-detects object names by scanning mod files against pattern rules
/// </summary>
public interface IModAutoDetectionService
{
    Task<string?> DetectObjectNameAsync(string modDirectory);
    Task<bool> LoadRulesAsync(string rulesFilePath);
    Task<List<ModAutoDetectionRule>> GetRulesAsync();
    Task AddRuleAsync(ModAutoDetectionRule rule);
    Task<bool> SaveRulesAsync(string rulesFilePath);
}

/// <summary>
/// Service for auto-detecting object names using file pattern matching
/// Responsibility: Scan mod files and match against rules to determine object name
/// </summary>
public class ModAutoDetectionService : IModAutoDetectionService
{
    private readonly string _rulePath;
    private readonly ILogHelper _logger;
    private readonly List<ModAutoDetectionRule> _rules = new();
    private readonly Lazy<Task> _init;

    public ModAutoDetectionService(IProfilePathService profilePaths, ILogHelper logger)
    {
        _rulePath = profilePaths?.AutoDetectionRulesPath ?? throw new ArgumentNullException(nameof(profilePaths));
        _logger = logger;
        _init = new Lazy<Task>(async () => await LoadRulesAsync(_rulePath), isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    /// <summary>
    /// Auto-detect object name from mod folder contents
    /// </summary>
    public async Task<string?> DetectObjectNameAsync(string modDirectory)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        if (!Directory.Exists(modDirectory))
            return null;

        // Get all files in directory (recursive)
        var files = Directory.GetFiles(modDirectory, "*", SearchOption.AllDirectories);

        // Check each rule (sorted by priority, higher first)
        foreach (var rule in _rules.OrderByDescending(r => r.Priority))
        {
            // Convert Unix-style wildcard to regex
            var regexPattern = WildcardToRegex(rule.Pattern);
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            // Check if any file matches the pattern
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(modDirectory, file);
                if (regex.IsMatch(relativePath))
                {
                    _logger.Debug($"Matched rule '{rule.Name}': {relativePath} -> {rule.Category}", "ModAutoDetectionService");
                    return rule.Category;
                }
            }
        }

        // No match found
        return await Task.FromResult<string?>(null).ConfigureAwait(false);
    }

    /// <summary>
    /// Load classification rules from JSON file
    /// </summary>
    public async Task<bool> LoadRulesAsync(string rulesFilePath)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        if (!File.Exists(rulesFilePath))
        {
            // Create default rules file
            await CreateDefaultRulesAsync(rulesFilePath).ConfigureAwait(false);
        }

        try
        {
            var json = await File.ReadAllTextAsync(rulesFilePath).ConfigureAwait(false);
            var rules = JsonConvert.DeserializeObject<List<ModAutoDetectionRule>>(json);

            if (rules != null)
            {
                _rules.Clear();
                _rules.AddRange(rules);
                _logger.Info($"Loaded {_rules.Count} rules from {rulesFilePath}", "ModAutoDetectionService");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load rules: {ex.Message}", "ModAutoDetectionService", ex);
        }

        return false;
    }

    /// <summary>
    /// Get all auto-detection rules
    /// </summary>
    public async Task<List<ModAutoDetectionRule>> GetRulesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return _rules;
    }

    /// <summary>
    /// Add a new auto-detection rule
    /// </summary>
    public async Task AddRuleAsync(ModAutoDetectionRule rule)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        _rules.Add(rule);
    }

    /// <summary>
    /// Save rules to JSON file
    /// </summary>
    public async Task<bool> SaveRulesAsync(string rulesFilePath)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            var json = JsonConvert.SerializeObject(_rules, Formatting.Indented);
            await File.WriteAllTextAsync(rulesFilePath, json).ConfigureAwait(false);
            _logger.Info($"Saved {_rules.Count} rules to {rulesFilePath}", "ModAutoDetectionService");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save rules: {ex.Message}", "ModAutoDetectionService", ex);
            return false;
        }
    }

    /// <summary>
    /// Create default classification rules
    /// </summary>
    private async Task CreateDefaultRulesAsync(string rulesFilePath)
    {

        await EnsureInitializedAsync().ConfigureAwait(false);

        var defaultRules = new List<ModAutoDetectionRule>
            {
                // Genshin Impact characters (example)
                new ModAutoDetectionRule { Name = "Fischl", Pattern = "*Fischl*", Category = "Fischl", Priority = 100 },
                new ModAutoDetectionRule { Name = "Nahida", Pattern = "*Nahida*", Category = "Nahida", Priority = 100 },
                new ModAutoDetectionRule { Name = "Keqing", Pattern = "*Keqing*", Category = "Keqing", Priority = 100 },
                new ModAutoDetectionRule { Name = "Raiden", Pattern = "*Raiden*", Category = "Raiden Shogun", Priority = 100 },
                new ModAutoDetectionRule { Name = "Ganyu", Pattern = "*Ganyu*", Category = "Ganyu", Priority = 100 },
                new ModAutoDetectionRule { Name = "Hutao", Pattern = "*Hutao*", Category = "Hu Tao", Priority = 100 },
                new ModAutoDetectionRule { Name = "Hutao Alt", Pattern = "*HuTao*", Category = "Hu Tao", Priority = 100 },
                new ModAutoDetectionRule { Name = "Ayaka", Pattern = "*Ayaka*", Category = "Kamisato Ayaka", Priority = 100 },
                new ModAutoDetectionRule { Name = "Yelan", Pattern = "*Yelan*", Category = "Yelan", Priority = 100 },
                new ModAutoDetectionRule { Name = "Nilou", Pattern = "*Nilou*", Category = "Nilou", Priority = 100 },

                // Generic patterns (lower priority)
                new ModAutoDetectionRule { Name = "Character Texture", Pattern = "*CharacterTexture*", Category = "Character", Priority = 10 },
                new ModAutoDetectionRule { Name = "Face Mod", Pattern = "*Face*", Category = "Face", Priority = 10 },
                new ModAutoDetectionRule { Name = "Body Mod", Pattern = "*Body*", Category = "Body", Priority = 10 },
                new ModAutoDetectionRule { Name = "Outfit Mod", Pattern = "*Outfit*", Category = "Outfit", Priority = 10 },
            };

        _rules.Clear();
        _rules.AddRange(defaultRules);

        // Create directory if needed
        var directory = Path.GetDirectoryName(rulesFilePath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await SaveRulesAsync(rulesFilePath).ConfigureAwait(false);
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
