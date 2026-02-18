using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using D3dxSkinManager.Modules.Mods.Models;
namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Service for querying and searching mods
/// Responsibility: Complex queries, search logic, filtering
/// </summary>
public class ModQueryService : IModQueryService
{
    private readonly IModRepository _repository;

    public ModQueryService(IModRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
    /// Looks for "classification:{nodeId}" tag in mod's Tags list
    /// </summary>
    public async Task<List<ModInfo>> GetModsByClassificationAsync(string classificationNodeId)
    {
        if (string.IsNullOrWhiteSpace(classificationNodeId))
        {
            return new List<ModInfo>();
        }

        var allMods = await _repository.GetAllAsync();
        var classificationTag = $"classification:{classificationNodeId}";

        // Filter mods that have the classification tag
        var matchingMods = allMods
            .Where(mod => mod.Tags.Contains(classificationTag))
            .ToList();

        return matchingMods;
    }

    /// <summary>
    /// Get all mods that don't have any classification tags
    /// Returns mods without any "classification:" prefix tags
    /// </summary>
    public async Task<List<ModInfo>> GetUnclassifiedModsAsync()
    {
        var allMods = await _repository.GetAllAsync();

        // Filter mods that don't have any classification tags
        var unclassifiedMods = allMods
            .Where(mod => !mod.Tags.Any(tag => tag.StartsWith("classification:")))
            .ToList();

        return unclassifiedMods;
    }

    /// <summary>
    /// Get count of mods that don't have any classification tags
    /// </summary>
    public async Task<int> GetUnclassifiedCountAsync()
    {
        var allMods = await _repository.GetAllAsync();

        // Count mods that don't have any classification tags
        var count = allMods.Count(mod => !mod.Tags.Any(tag => tag.StartsWith("classification:")));

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
