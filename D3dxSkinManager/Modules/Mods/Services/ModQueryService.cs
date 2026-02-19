using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for mod query service
/// </summary>
public interface IModQueryService
{
    Task<List<ModInfo>> SearchAsync(string searchTerm);
    Task<List<ModInfo>> FilterAsync(string? category = null, string? author = null,
        string? grading = null, bool? isLoaded = null, bool? isAvailable = null);
    Task<Dictionary<string, List<ModInfo>>> GetGroupedByObjectAsync();
    Task<ModStatistics> GetStatisticsAsync();
    Task<List<ModInfo>> GetModsByClassificationAsync(string classificationNodeId);
    Task<List<ModInfo>> GetUnclassifiedModsAsync();
    Task<int> GetUnclassifiedCountAsync();
}

/// <summary>
/// Service for querying and searching mods
/// Responsibility: Complex queries, search logic, filtering
/// </summary>
public class ModQueryService : IModQueryService
{
    private readonly IModRepository _repository;
    private readonly IClassificationRepository _classificationRepository;

    public ModQueryService(IModRepository repository, IClassificationRepository classificationRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _classificationRepository = classificationRepository ?? throw new ArgumentNullException(nameof(classificationRepository));
    }

    /// <summary>
    /// Search mods by keyword with support for negation (!) and AND logic
    /// </summary>
    public async Task<List<ModInfo>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await _repository.GetAllAsync();
        }

        var allMods = await _repository.GetAllAsync();

        // Split search term into individual terms
        var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var results = allMods.Where(mod =>
        {
            // All terms must match (AND logic)
            foreach (var term in terms)
            {
                var isNegation = term.StartsWith("!");
                var searchValue = isNegation ? term.Substring(1) : term;

                var matches = ModMatchesSearchTerm(mod, searchValue);

                // If negation and matches, exclude
                if (isNegation && matches)
                {
                    return false;
                }

                // If not negation and doesn't match, exclude
                if (!isNegation && !matches)
                {
                    return false;
                }
            }

            return true;
        }).ToList();

        return results;
    }

    /// <summary>
    /// Filter mods by multiple criteria
    /// </summary>
    public async Task<List<ModInfo>> FilterAsync(
        string? category = null,
        string? author = null,
        string? grading = null,
        bool? isLoaded = null,
        bool? isAvailable = null)
    {
        var mods = await _repository.GetAllAsync();

        if (!string.IsNullOrEmpty(category))
        {
            mods = mods.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(author))
        {
            mods = mods.Where(m => m.Author?.Equals(author, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        if (!string.IsNullOrEmpty(grading))
        {
            mods = mods.Where(m => m.Grading.Equals(grading, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (isLoaded.HasValue)
        {
            mods = mods.Where(m => m.IsLoaded == isLoaded.Value).ToList();
        }

        if (isAvailable.HasValue)
        {
            mods = mods.Where(m => m.IsAvailable == isAvailable.Value).ToList();
        }

        return mods;
    }

    /// <summary>
    /// Get mods grouped by object name
    /// </summary>
    public async Task<Dictionary<string, List<ModInfo>>> GetGroupedByObjectAsync()
    {
        var mods = await _repository.GetAllAsync();
        return mods.GroupBy(m => m.Category)
                   .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Get statistics about mods
    /// </summary>
    public async Task<ModStatistics> GetStatisticsAsync()
    {
        var allMods = await _repository.GetAllAsync();

        return new ModStatistics
        {
            TotalMods = allMods.Count,
            LoadedMods = allMods.Count(m => m.IsLoaded),
            AvailableMods = allMods.Count(m => m.IsAvailable),
            UniqueObjects = allMods.Select(m => m.Category).Distinct().Count(),
            UniqueAuthors = allMods.Where(m => !string.IsNullOrEmpty(m.Author))
                                    .Select(m => m.Author)
                                    .Distinct()
                                    .Count(),
            ModsByGrading = allMods.GroupBy(m => m.Grading)
                                    .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Get all mods that belong to a specific classification node
    /// If the node has children, includes all mods from child nodes recursively
    /// Uses the Category field to match mods (Category = classificationNodeId)
    /// </summary>
    public async Task<List<ModInfo>> GetModsByClassificationAsync(string classificationNodeId)
    {
        if (string.IsNullOrWhiteSpace(classificationNodeId))
        {
            return new List<ModInfo>();
        }

        // Get all descendant node IDs (includes self + all children recursively)
        var descendantIds = await _classificationRepository.GetAllDescendantIdsAsync(classificationNodeId);

        // Get all mods matching any of these categories
        var allMods = await _repository.GetAllAsync();
        var matchingMods = allMods
            .Where(mod => descendantIds.Contains(mod.Category))
            .ToList();

        return matchingMods;
    }

    /// <summary>
    /// Get all mods that don't have a category assigned
    /// Returns mods with empty or "Unknown" category
    /// </summary>
    public async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var allMods = await _repository.GetAllAsync();

        // Filter mods that don't have a category assigned
        var unclassifiedMods = allMods
            .Where(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                         mod.Category.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return unclassifiedMods;
    }

    /// <summary>
    /// Get count of mods that don't have a category assigned
    /// </summary>
    public async Task<int> GetUnclassifiedCountAsync()
    {
        var allMods = await _repository.GetAllAsync();

        // Count mods that don't have a category assigned
        var count = allMods.Count(mod => string.IsNullOrWhiteSpace(mod.Category) ||
                                         mod.Category.Equals("Unknown", StringComparison.OrdinalIgnoreCase));

        return count;
    }

    private bool ModMatchesSearchTerm(ModInfo mod, string searchTerm)
    {
        var lowerSearch = searchTerm.ToLowerInvariant();

        return mod.SHA.ToLowerInvariant().Contains(lowerSearch) ||
               mod.Name.ToLowerInvariant().Contains(lowerSearch) ||
               mod.Category.ToLowerInvariant().Contains(lowerSearch) ||
               (mod.Author?.ToLowerInvariant().Contains(lowerSearch) == true) ||
               (mod.Description?.ToLowerInvariant().Contains(lowerSearch) == true) ||
               mod.Tags.Any(t => t.ToLowerInvariant().Contains(lowerSearch));
    }
}
