namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Represents a single keybinding from mod .ini files
/// </summary>
public class ModKeybinding
{
    /// <summary>
    /// Section name from .ini file (e.g., "KeyBodyColor", "KeyHorn")
    /// </summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>
    /// The key assigned (e.g., "9", "i", "VK_UP", "[")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the key (converted from technical names like VK_UP to friendly names)
    /// </summary>
    public string KeyDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Description/purpose extracted from section name (e.g., "Body Color", "Horn")
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Keybinding type (e.g., "cycle", "toggle", "hold")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Associated variable name (e.g., "$color", "$horn")
    /// </summary>
    public string Variable { get; set; } = string.Empty;

    /// <summary>
    /// Values for cycle type (e.g., "0,1,2,3")
    /// </summary>
    public string CycleValues { get; set; } = string.Empty;
}
