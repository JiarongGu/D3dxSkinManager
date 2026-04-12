namespace D3dxSkinManager.Modules.Tool.ModPackage.Models;

/// <summary>
/// Package manifest stored as manifest.json in the export folder root.
/// Describes the entire package and maps mod file names to their metadata.
/// </summary>
public class PackageManifest
{
    public string Version { get; set; } = "1.0";
    public string AppName { get; set; } = "D3dxSkinManager";
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ModCount { get; set; }
    public int CategoryCount { get; set; }
    public bool IncludesArchives { get; set; }
    public bool IncludesPreviews { get; set; }
    public List<PackageCategory> Categories { get; set; } = new();
    public List<PackageModEntry> Mods { get; set; } = new();
}

/// <summary>
/// Category entry in the manifest - preserves hierarchy info for import.
/// </summary>
public class PackageCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Mod entry in the manifest - maps file name to mod identity and metadata.
/// </summary>
public class PackageModEntry
{
    /// <summary>Original mod ID for matching on import</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>File name in the mods/ folder (e.g., "Cool Skin.7z")</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Folder name in previews/ (matches escaped mod name)</summary>
    public string? PreviewFolder { get; set; }

    // Mod metadata
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryPath { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Grading { get; set; } = "G";
    public string Type { get; set; } = "7z";
    public bool HasArchive { get; set; }
    public bool HasPreviews { get; set; }
}

/// <summary>
/// Configuration for an export operation sent from the frontend.
/// </summary>
public class ExportConfig
{
    public string PackageName { get; set; } = string.Empty;
    public string PackageDescription { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public List<string> ModIds { get; set; } = new();
    public bool IncludeArchives { get; set; } = true;
    public bool IncludePreviews { get; set; } = true;
}

/// <summary>
/// Result of analyzing a package folder for import.
/// </summary>
public class PackageAnalysis
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageDescription { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public int TotalModCount { get; set; }
    public bool HasArchives { get; set; }
    public bool HasPreviews { get; set; }
    public List<PackageCategory> Categories { get; set; } = new();
    public List<AnalyzedModEntry> Mods { get; set; } = new();
}

/// <summary>
/// A mod entry after analysis - includes match status against local mods.
/// </summary>
public class AnalyzedModEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryPath { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Grading { get; set; } = "G";
    public bool HasArchive { get; set; }
    public bool HasPreviews { get; set; }

    /// <summary>"new", "update", or "identical"</summary>
    public string Status { get; set; } = "new";

    /// <summary>What changed if status is "update" (e.g., ["name", "tags", "archive"])</summary>
    public List<string> ChangedFields { get; set; } = new();

    /// <summary>Local mod name if exists (for comparison)</summary>
    public string? LocalName { get; set; }

    /// <summary>Local mod author if exists (for comparison)</summary>
    public string? LocalAuthor { get; set; }

    /// <summary>Absolute paths to preview images in the package folder</summary>
    public List<string> PreviewPaths { get; set; } = new();
}

/// <summary>
/// Configuration for an import operation sent from the frontend.
/// </summary>
public class ImportConfig
{
    public string PackagePath { get; set; } = string.Empty;
    public List<string> SelectedModIds { get; set; } = new();
    public bool UpdateExisting { get; set; } = true;
    public bool ImportPreviews { get; set; } = true;
    public bool CreateMissingCategories { get; set; } = true;
}

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportResult
{
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> ImportedModNames { get; set; } = new();
    public List<string> UpdatedModNames { get; set; } = new();
}

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportResult
{
    public bool Success { get; set; }
    public int ExportedCount { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Progress data emitted during export/import operations.
/// </summary>
public class PackageProgress
{
    public string Operation { get; set; } = string.Empty;  // "export" or "import"
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentModName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;  // "copying", "metadata", "previews", etc.
}
