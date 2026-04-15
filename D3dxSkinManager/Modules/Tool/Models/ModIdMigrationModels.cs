namespace D3dxSkinManager.Modules.Tool.Models;

/// <summary>
/// Result of scanning mods for non-GUID IDs that need migration
/// </summary>
public class ModIdMigrationScanResult
{
    public int TotalMods { get; set; }
    public int ModsNeedingMigration { get; set; }
    public List<ModIdMigrationItem> Items { get; set; } = new();
}

/// <summary>
/// A single mod that needs ID migration
/// </summary>
public class ModIdMigrationItem
{
    public string OldId { get; set; } = string.Empty;
    public string NewId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public bool HasArchive { get; set; }
    public bool HasCache { get; set; }
    public bool HasPreview { get; set; }
}

/// <summary>
/// Result of the migration operation
/// </summary>
public class ModIdMigrationResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<ModIdMigrationItemResult> Results { get; set; } = new();
}

/// <summary>
/// Result for a single mod's migration
/// </summary>
public class ModIdMigrationItemResult
{
    public string OldId { get; set; } = string.Empty;
    public string NewId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
