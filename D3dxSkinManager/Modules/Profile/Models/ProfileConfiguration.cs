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
    /// Mod work directory configuration (parent of Mods folder) with cache cleanup
    /// </summary>
    public ModWorkConfiguration ModWork { get; set; } = new ModWorkConfiguration();

    /// <summary>
    /// Window positions and sizes for secondary windows (e.g., "capture", "debug")
    /// Key: window name, Value: window configuration (position and size)
    /// </summary>
    public Dictionary<string, WindowConfiguration> Windows { get; set; } = new Dictionary<string, WindowConfiguration>();

    /// <summary>
    /// Tab-specific settings (per-profile UI preferences)
    /// </summary>
    public TabSettings Tabs { get; set; } = new TabSettings();

    /// <summary>
    /// Mod import configuration (compression settings, etc.)
    /// </summary>
    public ModImportConfiguration ModImport { get; set; } = new ModImportConfiguration();

    /// <summary>
    /// Game launch configuration (path to the game / XXMI launcher / 3DMigoto loader + args).
    /// </summary>
    public LaunchConfiguration Launch { get; set; } = new LaunchConfiguration();

    /// <summary>
    /// Fix-tool runner settings (Python interpreter, timeout, accepted extensions, auto-confirm).
    /// Editable per profile; seeds sensible defaults.
    /// </summary>
    public ModFixConfiguration FixTools { get; set; } = new ModFixConfiguration();
}

/// <summary>
/// Per-profile fix-tool runner configuration — the editable surface of the runner's defaults
/// (was a hard-coded <c>ModFixOptions</c>). Game-agnostic.
/// </summary>
public class ModFixConfiguration
{
    /// <summary>
    /// Explicit Python interpreter (path or command) to run .py fix scripts. Empty = auto-detect
    /// from the default candidates (py / python / python3).
    /// </summary>
    public string PythonPath { get; set; } = string.Empty;

    /// <summary>Per-mod fix execution timeout, in minutes (script exceeding this is killed). Default 5.</summary>
    public int TimeoutMinutes { get; set; } = 5;

    /// <summary>Script extensions the runner accepts (lower-case, leading dot).</summary>
    public List<string> SupportedExtensions { get; set; } = new() { ".py", ".exe", ".bat", ".cmd" };

    /// <summary>
    /// Auto-feed newlines to a script's stdin so interactive "Press Enter to continue" prompts run
    /// unattended. Default true.
    /// </summary>
    public bool AutoConfirm { get; set; } = true;
}

/// <summary>
/// Per-profile game launch configuration. <see cref="Path"/> is the executable/launcher to run
/// (e.g. an XXMI Launcher, a 3DMigoto loader, or the game exe); <see cref="Args"/> are optional
/// command-line arguments. Empty Path = not configured.
/// </summary>
public class LaunchConfiguration
{
    public string Path { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
}

/// <summary>
/// Window position and size configuration for any secondary window
/// All values are stored in logical pixels (DPI-independent)
/// With HighDpiMode.PerMonitorV2, Windows automatically scales these for different DPI displays
/// </summary>
public class WindowConfiguration
{
    /// <summary>
    /// X position in logical pixels (DPI-independent)
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Y position in logical pixels (DPI-independent)
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Width in logical pixels (DPI-independent)
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height in logical pixels (DPI-independent)
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// DPI scale factor when this configuration was saved (for migration from old configs)
    /// 1.0 = 100%, 1.5 = 150%, 2.0 = 200%, etc.
    /// If null, assumes values are already in logical pixels
    /// </summary>
    public double? SavedDpiScale { get; set; }
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
    /// Category panel view mode: "tree" or "grid"
    /// Default: "tree"
    /// </summary>
    public string CategoryViewMode { get; set; } = "tree";

    /// <summary>
    /// Category IDs that are locked expanded (persist across sessions)
    /// Default: empty list
    /// </summary>
    public List<string> LockedExpandedCategories { get; set; } = new List<string>();
}

/// <summary>
/// Mod import configuration (compression settings, etc.)
/// </summary>
public class ModImportConfiguration
{
    /// <summary>
    /// Compression type for imported mods
    /// Valid values: "7z", "zip", "rar"
    /// Default: "7z"
    /// </summary>
    public string CompressionType { get; set; } = "7z";

    /// <summary>
    /// Compression mode/level
    /// Valid values: "fast", "high", "ultra"
    /// Default: "fast"
    /// </summary>
    public string CompressionMode { get; set; } = "fast";
}
