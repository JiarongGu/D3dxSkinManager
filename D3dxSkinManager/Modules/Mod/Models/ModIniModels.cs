namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// A single <c>key = value</c> assignment inside a mod .ini section, with an editability verdict.
/// Line-indexed so the write-back can patch exactly this line without re-parsing the whole file.
/// </summary>
public class ModIniEntry
{
    /// <summary>Left-hand side, trimmed (e.g. <c>key</c>, <c>type</c>, <c>$swapvar</c>, <c>global persist $x</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Right-hand side value, inline comment stripped (e.g. <c>0</c>, <c>cycle</c>, <c>0,1,2</c>).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>0-based line index within the file — the write-back key.</summary>
    public int LineIndex { get; set; }

    /// <summary>True when the user may safely change <see cref="Value"/> (tunable key in a Key/Constants section).</summary>
    public bool Editable { get; set; }

    /// <summary>Why a non-editable entry is locked (<c>advancedSection</c> / <c>command</c>) — drives the UI tooltip. Null when editable.</summary>
    public string? LockReason { get; set; }
}

/// <summary>A section header plus the assignments under it, in file order.</summary>
public class ModIniSection
{
    /// <summary>Section name without brackets (e.g. <c>KeySwap0</c>, <c>Constants</c>, <c>TextureOverrideBody</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>True if the whole section is advanced/read-only (hash/override/resource/shader/command-list).</summary>
    public bool Advanced { get; set; }

    public List<ModIniEntry> Entries { get; set; } = new();
}

/// <summary>One parsed .ini file of a mod, addressed by its path relative to the mod cache dir.</summary>
public class ModIniFile
{
    /// <summary>Path relative to the mod cache dir, forward-slashed (the archive entry path too).</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Just the file name for display (e.g. <c>mod.ini</c>).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The file's 3DMigoto <c>namespace</c> directive value, if declared (e.g. <c>Merge\Master</c>).
    /// Files in the same namespace are related — other files reference this one's resources/vars
    /// through it. Shown so the user understands which configs belong together. Null when none.
    /// </summary>
    public string? Namespace { get; set; }

    public List<ModIniSection> Sections { get; set; } = new();
}
