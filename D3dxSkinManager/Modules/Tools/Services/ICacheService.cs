using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Tools.Models;

namespace D3dxSkinManager.Modules.Tools.Services;

/// <summary>
/// Service for cache management operations
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Scan cache directories and categorize cache files
    /// </summary>
    Task<List<CacheItem>> ScanCacheAsync();

    /// <summary>
    /// Get cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Clean cache by category
    /// </summary>
    /// <param name="category">Category to clean</param>
    /// <returns>Number of items deleted</returns>
    Task<int> CleanCacheAsync(CacheCategory category);

    /// <summary>
    /// Delete specific cache item by SHA
    /// </summary>
    Task<bool> DeleteCacheItemAsync(string sha);

    /// <summary>
    /// Check if a SHA has cached files
    /// </summary>
    bool HasCache(string sha);

    /// <summary>
    /// Get cache path for a specific SHA
    /// </summary>
    string? GetCachePath(string sha);

    /// <summary>
    /// Calculate directory size in bytes
    /// </summary>
    long GetDirectorySize(string path);
}
