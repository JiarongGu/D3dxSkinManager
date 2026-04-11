namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Domain model for mod preset (returned to frontend)
/// </summary>
public class ModPresetInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ModCount { get; set; }
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
}
