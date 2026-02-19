using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Tools.Services;

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
    List<ModAutoDetectionRule> GetRules();
    void AddRule(ModAutoDetectionRule rule);
    Task<bool> SaveRulesAsync(string rulesFilePath);
}

/// <summary>
/// Service for auto-detecting object names using file pattern matching
/// Responsibility: Scan mod files and match against rules to determine object name
/// </summary>
public class ModAutoDetectionService : IModAutoDetectionService
{
    private readonly string _rulePath;

    public ModAutoDetectionService(IProfileContext profileContext)
    {
        _rulePath = Path.Combine(profileContext.ProfilePath, "auto_detection_rules.json");
        LoadRulesAsync(_rulePath).Wait();
    }

    private readonly List<ModAutoDetectionRule> _rules = new();

        /// <summary>
        /// Auto-detect object name from mod folder contents
        /// </summary>
        public async Task<string?> DetectObjectNameAsync(string modDirectory)
        {
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
                        Console.WriteLine($"[Classification] Matched rule '{rule.Name}': {relativePath} -> {rule.Category}");
                        return rule.Category;
                    }
                }
            }

            // No match found
            return await Task.FromResult<string?>(null);
        }

        /// <summary>
        /// Load classification rules from JSON file
        /// </summary>
        public async Task<bool> LoadRulesAsync(string rulesFilePath)
        {
            if (!File.Exists(rulesFilePath))
            {
                // Create default rules file
                await CreateDefaultRulesAsync(rulesFilePath);
            }

            try
            {
                var json = await File.ReadAllTextAsync(rulesFilePath);
                var rules = JsonConvert.DeserializeObject<List<ModAutoDetectionRule>>(json);

                if (rules != null)
                {
                    _rules.Clear();
                    _rules.AddRange(rules);
                    Console.WriteLine($"[Classification] Loaded {_rules.Count} rules from {rulesFilePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Classification] Failed to load rules: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get all auto-detection rules
        /// </summary>
        public List<ModAutoDetectionRule> GetRules()
        {
            return new List<ModAutoDetectionRule>(_rules);
        }

        /// <summary>
        /// Add a new auto-detection rule
        /// </summary>
        public void AddRule(ModAutoDetectionRule rule)
        {
            _rules.Add(rule);
        }

        /// <summary>
        /// Save rules to JSON file
        /// </summary>
        public async Task<bool> SaveRulesAsync(string rulesFilePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_rules, Formatting.Indented);
                await File.WriteAllTextAsync(rulesFilePath, json);
                Console.WriteLine($"[Classification] Saved {_rules.Count} rules to {rulesFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Classification] Failed to save rules: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create default classification rules
        /// </summary>
        private async Task CreateDefaultRulesAsync(string rulesFilePath)
        {
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

            await SaveRulesAsync(rulesFilePath);
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

    /// <summary>
    /// Extension methods for search and filter
    /// </summary>
    public static class ModSearchExtensions
    {
        /// <summary>
        /// Search mods by keyword (searches SHA, Name, Object, Author, Tags)
        /// </summary>
        public static List<ModInfo> Search(this List<ModInfo> mods, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return mods;

            var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var results = mods;

            foreach (var term in terms)
            {
                var isNegated = term.StartsWith("!");
                var actualTerm = isNegated ? term.Substring(1) : term;

                if (string.IsNullOrWhiteSpace(actualTerm))
                    continue;

                if (isNegated)
                {
                    // Exclude mods matching this term
                    results = results.Where(m => !MatchesTerm(m, actualTerm)).ToList();
                }
                else
                {
                    // Include only mods matching this term
                    results = results.Where(m => MatchesTerm(m, actualTerm)).ToList();
                }
            }

            return results;
        }

        private static bool MatchesTerm(ModInfo mod, string term)
        {
            var lowerTerm = term.ToLowerInvariant();

            return mod.SHA.ToLowerInvariant().Contains(lowerTerm) ||
                   mod.Name.ToLowerInvariant().Contains(lowerTerm) ||
                   mod.Category.ToLowerInvariant().Contains(lowerTerm) ||
                   mod.Author.ToLowerInvariant().Contains(lowerTerm) ||
                   mod.Grading.ToLowerInvariant().Contains(lowerTerm) ||
                   mod.Tags.Any(t => t.ToLowerInvariant().Contains(lowerTerm));
        }

        /// <summary>
        /// Filter by object name
        /// </summary>
        public static List<ModInfo> FilterByObject(this List<ModInfo> mods, string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return mods;

            return mods.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Filter by grading
        /// </summary>
        public static List<ModInfo> FilterByGrading(this List<ModInfo> mods, string grading)
        {
            if (string.IsNullOrWhiteSpace(grading))
                return mods;

            return mods.Where(m => m.Grading.Equals(grading, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Filter by loaded status
        /// </summary>
        public static List<ModInfo> FilterByLoadedStatus(this List<ModInfo> mods, bool? isLoaded)
        {
            if (isLoaded == null)
                return mods;

            return mods.Where(m => m.IsLoaded == isLoaded.Value).ToList();
        }

        /// <summary>
        /// Get unique object names from mod list
        /// </summary>
        public static List<string> GetUniqueObjects(this List<ModInfo> mods)
        {
            return mods.Select(m => m.Category)
                       .Distinct()
                       .OrderBy(o => o)
                       .ToList();
        }

        /// <summary>
        /// Get unique authors from mod list
        /// </summary>
        public static List<string> GetUniqueAuthors(this List<ModInfo> mods)
        {
            return mods.Where(m => !string.IsNullOrEmpty(m.Author))
                       .Select(m => m.Author)
                       .Distinct()
                       .OrderBy(a => a)
                       .ToList();
        }

        /// <summary>
        /// Get all unique tags from mod list
        /// </summary>
        public static List<string> GetUniqueTags(this List<ModInfo> mods)
        {
        return mods.SelectMany(m => m.Tags)
                   .Distinct()
                   .OrderBy(t => t)
                   .ToList();
    }
}
