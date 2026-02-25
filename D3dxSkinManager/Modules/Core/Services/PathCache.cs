using Microsoft.Extensions.Caching.Memory;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for path caching with LRU eviction
/// Dedicated cache for CustomSchemeHandler to prevent unbounded memory growth
/// </summary>
public interface IPathCache : IMemoryCache
{
}

/// <summary>
/// Implementation of path cache with size-limited LRU eviction
/// Used by CustomSchemeHandler for caching normalized file paths
/// </summary>
public class PathCache : MemoryCache, IPathCache
{
    public PathCache() : base(new MemoryCacheOptions
    {
        SizeLimit = 50, // Maximum 500 cached file paths
        CompactionPercentage = 0.25 // Compact 25% when size limit reached (LRU eviction)
    })
    {
    }
}
