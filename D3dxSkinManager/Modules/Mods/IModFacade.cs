using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Interface for Mod Management facade
/// Handles: MOD_GET_ALL, MOD_LOAD, MOD_UNLOAD, etc.
/// Prefix: MOD_*
/// </summary>
public interface IModFacade : IModuleFacade
{

    // Core Mod Operations
    Task<List<ModInfo>> GetAllModsAsync();
    Task<ModInfo?> GetModByIdAsync(string sha);
    Task<bool> LoadModAsync(string sha);
    Task<bool> UnloadModAsync(string sha);
    Task<List<string>> GetLoadedModIdsAsync();
    Task<ModInfo?> ImportModAsync(string filePath);
    Task<bool> DeleteModAsync(string sha);

    // Query Operations
    Task<List<ModInfo>> GetModsByObjectAsync(string category);
    Task<List<string>> GetObjectNamesAsync();
    Task<List<string>> GetAuthorsAsync();
    Task<List<string>> GetTagsAsync();
    Task<List<ModInfo>> SearchModsAsync(string searchTerm);
    Task<ModStatistics> GetStatisticsAsync();

    // Metadata Operations
    Task<bool> UpdateMetadataAsync(string sha, string? name, string? author, List<string>? tags, string? grading, string? description);
    Task<int> BatchUpdateMetadataAsync(List<string> shas, string? name, string? author, List<string>? tags, string? grading, string? description, List<string> fieldMask);
    Task<bool> ImportPreviewImageAsync(string sha, string imagePath);

    // Classification Operations
    Task<List<ClassificationNode>> GetClassificationTreeAsync();
    Task<bool> RefreshClassificationTreeAsync();
}
