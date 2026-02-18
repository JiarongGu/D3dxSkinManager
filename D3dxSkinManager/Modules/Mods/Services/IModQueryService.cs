using System.Collections.Generic;
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
