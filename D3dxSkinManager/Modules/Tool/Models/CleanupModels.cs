namespace D3dxSkinManager.Modules.Tool.Models;

/// <summary>
/// Category of orphaned file for cleanup
/// </summary>
public enum OrphanCategory
{
    Thumbnail,
    Preview,
    TempFile,
    ModCache,
    OrphanedArchive,
    MissingArchive
}

/// <summary>
/// An orphaned file or directory found during scan
/// </summary>
public class OrphanedItem
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string LastModified { get; set; } = string.Empty;
    public OrphanCategory Category { get; set; }
    /// <summary>True when Path is a directory. The scanner knows — the UI must not guess from the
    /// name (mod archives are extensionless files and used to be misclassified as directories).</summary>
    public bool IsDirectory { get; set; }
}

/// <summary>
/// Result of scanning for orphaned files by category
/// </summary>
public class OrphanScanResult
{
    public OrphanCategory Category { get; set; }
    public List<OrphanedItem> Items { get; set; } = new();
    public int TotalCount => Items.Count;
    public long TotalSizeBytes => Items.Sum(i => i.SizeBytes);
}

/// <summary>
/// Result of a cleanup operation
/// </summary>
public class CleanupResult
{
    public OrphanCategory Category { get; set; }
    public int DeletedCount { get; set; }
    public long FreedBytes { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
