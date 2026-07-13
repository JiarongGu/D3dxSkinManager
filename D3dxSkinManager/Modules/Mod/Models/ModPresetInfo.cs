namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Domain model for mod preset (returned to frontend)
/// </summary>
public class ModPresetInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ModCount { get; set; }
    /// <summary>True when this preset also captured per-mod 3DMigoto $var state (restored on apply).</summary>
    public bool HasModState { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Result of applying a mod preset
/// </summary>
public class ModPresetApplyResult
{
    public string PresetName { get; set; } = string.Empty;
    public int LoadedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> FailedModIds { get; set; } = new();

    /// <summary>Members skipped because they no longer resolve to a managed mod (deleted, or a legacy
    /// unmanaged entry that can't be redeployed) — self-healed, NOT counted as a failure (#36).</summary>
    public int SkippedCount { get; set; }
}
