using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Tools.Models;

namespace D3dxSkinManager.Modules.Tools;

/// <summary>
/// Interface for Tools facade
/// Handles: TOOLS_SCAN_CACHE, TOOLS_CLEAN_CACHE, TOOLS_VALIDATE_STARTUP, etc.
/// Prefix: TOOLS_*
/// </summary>
public interface IToolsFacade : IModuleFacade
{

    // Cache Management
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);
    Task<bool> DeleteCacheItemAsync(string sha);

    // Validation
    Task<StartupValidationReport> ValidateStartupAsync();
}
