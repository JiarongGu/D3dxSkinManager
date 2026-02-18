using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Tools.Models;

/// <summary>
/// Cache category classification
/// </summary>
public enum CacheCategory
{
    Invalid,        // Cache with no matching SHA in database or index
    RarelyUsed,     // Cache with SHA only in index (not actively loaded)
    FrequentlyUsed  // Cache with SHA that is actively loaded
}

/// <summary>
/// Cache item information
/// </summary>
public class CacheItem
{
    public string Path { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public CacheCategory Category { get; set; }
    public string LastModified { get; set; } = string.Empty;
}

/// <summary>
/// Cache statistics summary
/// </summary>
public class CacheStatistics
{
    public int InvalidCount { get; set; }
    public long InvalidSizeBytes { get; set; }
    public int RarelyUsedCount { get; set; }
    public long RarelyUsedSizeBytes { get; set; }
    public int FrequentlyUsedCount { get; set; }
    public long FrequentlyUsedSizeBytes { get; set; }
    public int TotalCount { get; set; }
    public long TotalSizeBytes { get; set; }
}
