namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Profile configuration settings stored in {profileId}/config.json
/// </summary>
public class ProfileConfiguration
{
    /// <summary>
    /// Profile ID this configuration belongs to
    /// </summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// 3DMigoto version to use (3dmigoto, 3dmigoto-dev, custom)
    /// </summary>
    public string MigotoVersion { get; set; } = "3dmigoto";

    /// <summary>
    /// Work directory configuration (parent of Mods folder)
    /// </summary>
    public WorkDirectoryConfiguration Work { get; set; } = new WorkDirectoryConfiguration();

    /// <summary>
    /// Window positions and sizes for secondary windows (e.g., "capture", "debug")
    /// Key: window name, Value: window configuration (position and size)
    /// </summary>
    public Dictionary<string, WindowConfiguration> Windows { get; set; } = new Dictionary<string, WindowConfiguration>();

    /// <summary>
    /// Tab-specific settings (per-profile UI preferences)
    /// </summary>
    public TabSettings Tabs { get; set; } = new TabSettings();
}

/// <summary>
/// Window position and size configuration for any secondary window
/// </summary>
public class WindowConfiguration
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <summary>
/// Tab-specific settings for the profile
/// </summary>
public class TabSettings
{
    /// <summary>
    /// Mod tab settings
    /// </summary>
    public ModTabSettings Mod { get; set; } = new ModTabSettings();
}

/// <summary>
/// Mod tab specific settings
/// </summary>
public class ModTabSettings
{
    /// <summary>
    /// Panel sizes as percentages (e.g., "20 35" means CategoryPanel=20%, ModListPanel=35%, Preview=45%)
    /// Format: "categoryWidth modListWidth" (both in percentage, preview takes remaining space)
    /// Default: "20 35" (CategoryPanel=20%, ModListPanel=35%, Preview=45%)
    /// </summary>
    public string PanelSize { get; set; } = "20 35";

    /// <summary>
    /// Category IDs that are locked expanded (cannot be collapsed by clicking)
    /// </summary>
    public List<string> LockedExpandedCategories { get; set; } = new List<string>();
}
