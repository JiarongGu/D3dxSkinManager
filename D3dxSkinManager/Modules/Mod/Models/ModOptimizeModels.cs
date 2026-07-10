namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>A set of byte-identical files inside one mod (same sha256 + length).</summary>
public class ModDuplicateGroup
{
    /// <summary>Size of ONE copy in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>The copy that is kept — every `filename =` reference is rewritten to it.</summary>
    public string Canonical { get; set; } = string.Empty;

    /// <summary>The redundant copies (forward-slash relpaths) that can be removed.</summary>
    public List<string> Duplicates { get; set; } = new();
}

/// <summary>A `filename =`-referenced asset whose name has non-ASCII/unsafe characters, with the
/// normalized name it would be renamed to (both forward-slash relpaths).</summary>
public class ModNameFix
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

/// <summary>Result of scanning a mod for duplicate asset files (read-only, nothing changed yet).</summary>
public class ModOptimizeScanResult
{
    public int TotalFiles { get; set; }
    public List<ModDuplicateGroup> Groups { get; set; } = new();
    /// <summary>Bytes freed if all duplicates are removed (copies beyond the canonical).</summary>
    public long WastedBytes { get; set; }
    /// <summary>Referenced asset files with unsafe (non-ASCII/symbol) names + their normalized names.
    /// Optionally applied — renames on disk + rewrites the `filename =` refs.</summary>
    public List<ModNameFix> Normalizable { get; set; } = new();
}

/// <summary>Result of applying the optimization.</summary>
public class ModOptimizeResult
{
    public int RemovedFiles { get; set; }
    public int RewrittenRefs { get; set; }
    public long FreedBytes { get; set; }
    /// <summary>Files renamed to a normalized name (when normalization was requested).</summary>
    public int RenamedFiles { get; set; }
}
