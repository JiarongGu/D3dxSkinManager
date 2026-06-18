namespace D3dxSkinManager.Modules.Tool.Models;

// ===== Enums =====

public enum HealthIssueSeverity { Error, Warning, Info }

public enum HealthIssueType
{
    NoIniFile,
    EmptyMod,
    MissingResource,
    InvalidIniSyntax,
    EmptyIniFile,
    StaleHash,
    MissingPlugin
}

public enum DuplicateType { Identical, TextureVariant }

public enum AnalysisStatus { Idle, Running, Paused, Completed, Cancelled }

// ===== Per-Mod Results =====

public class ModHealthIssue
{
    public HealthIssueType Type { get; set; }
    public HealthIssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

/// <summary>
/// Full analysis result for a single mod (health + fingerprint combined)
/// </summary>
public class ModAnalysisResult
{
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsLoaded { get; set; }
    public bool HasCache { get; set; }
    public bool IsAvailable { get; set; }

    // Health
    public string HealthStatus { get; set; } = "unknown"; // healthy, warning, error
    public List<ModHealthIssue> Issues { get; set; } = new();
    public int IniFileCount { get; set; }
    public int ResourceFileCount { get; set; }
    public int TextureOverrideCount { get; set; }

    // Fingerprint
    public List<string> TargetHashes { get; set; } = new();
    public string BufferHash { get; set; } = string.Empty;
    public string TextureHash { get; set; } = string.Empty;
    public List<string> BufferFileHashes { get; set; } = new();
    public List<string> TextureFileHashes { get; set; } = new();
    public long BufferSizeBytes { get; set; }
    public long TextureSizeBytes { get; set; }

    // Plugin dependencies
    public List<string> PluginDependencies { get; set; } = new();

    // Display
    public string? PreviewPath { get; set; }
}

// ===== INI Parsing =====

public class ModIniStructure
{
    public List<string> TargetHashes { get; set; } = new();
    public List<string> BufferFiles { get; set; } = new();
    public List<string> TextureFiles { get; set; } = new();
    public int TextureOverrideCount { get; set; }
    public int ResourceCount { get; set; }
    public List<string> PluginReferences { get; set; } = new();
}

// ===== Grouping Results =====

public class DuplicateGroup
{
    public DuplicateType Type { get; set; }
    public string GroupLabel { get; set; } = string.Empty;
    public List<string> SharedHashes { get; set; } = new();
    public List<ModAnalysisResult> Mods { get; set; } = new();

    /// <summary>
    /// True when all mods in the group target the exact same set of TextureOverride hashes.
    /// Combined with Identical type, this means the mods are exact clones.
    /// </summary>
    public bool AllHashesMatch { get; set; }
}

public class ModConflict
{
    public string Hash { get; set; } = string.Empty;
    public List<ModAnalysisResult> Mods { get; set; } = new();
}

/// <summary>
/// Hash frequency info for staleness detection
/// </summary>
public class HashFrequency
{
    public string Hash { get; set; } = string.Empty;
    public int ModCount { get; set; }
    public bool IsSuspicious { get; set; }
}

// ===== Session History =====

/// <summary>
/// Summary of an analysis session (for history list)
/// </summary>
public class AnalysisSessionSummary
{
    public string Id { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Status { get; set; } = "running";
    public int TotalMods { get; set; }
    public int AnalyzedCount { get; set; }
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int IdenticalCount { get; set; }
    public int TextureVariantCount { get; set; }
    public int ConflictCount { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
}

// ===== Latest per-mod health (for the mod-list "last scan" badge) =====

/// <summary>
/// Compact health summary for one mod from its most recent analysis — drives the mod-list badge.
/// Point-in-time (reflects the mod as last scanned), not a live guarantee.
/// </summary>
public class ModHealthSummary
{
    public string ModId { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = "unknown"; // healthy | warning | error
    public int IssueCount { get; set; }
}

// ===== Full Analysis Report =====

/// <summary>
/// Complete analysis report combining all views (returned to frontend)
/// </summary>
public class FullAnalysisReport
{
    // Session info
    public string SessionId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public AnalysisStatus Status { get; set; }
    public int TotalMods { get; set; }
    public int AnalyzedCount { get; set; }
    public int SkippedCount { get; set; }

    // Health summary
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }

    // All per-mod results
    public List<ModAnalysisResult> Results { get; set; } = new();

    // Duplicate groups
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
    public int IdenticalCount { get; set; }
    public int TextureVariantCount { get; set; }

    // Conflicts (loaded mods only)
    public List<ModConflict> Conflicts { get; set; } = new();
    public int ConflictCount { get; set; }
    public int AffectedModCount { get; set; }

    // Hash frequency (for stale hash detection)
    public List<HashFrequency> SuspiciousHashes { get; set; } = new();
}

// ===== Progress =====

public class AnalysisProgress
{
    public string SessionId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentModName { get; set; } = string.Empty;
    public AnalysisStatus Status { get; set; }

    /// <summary>Last analyzed mod's health status (healthy/warning/error), null before first result</summary>
    public string? LastModName { get; set; }
    public string? LastHealthStatus { get; set; }

    // Live counts updated during scan
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
}
