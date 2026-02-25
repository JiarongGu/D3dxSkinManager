namespace D3dxSkinManager.Modules.Setting.Models;

/// <summary>
/// Global settings that apply across all profiles
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// Theme mode: light, dark, or auto
    /// </summary>
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// Annotation/tooltip level: all, more, less, off
    /// </summary>
    public string AnnotationLevel { get; set; } = "all";

    /// <summary>
    /// Log level for C# backend file logging: ALL, DEBUG, INFO, WARNING, ERROR, OFF
    /// </summary>
    public string LogLevel { get; set; } = "OFF";

    /// <summary>
    /// Language/Locale: en, cn, etc.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Window size and position settings
    /// </summary>
    public WindowSettings Window { get; set; } = new();

    /// <summary>
    /// Tab-specific settings (panel sizes, layouts, etc.)
    /// </summary>
    public TabSettings Tabs { get; set; } = new();
}

/// <summary>
/// Window size and position settings
/// </summary>
public class WindowSettings
{
    /// <summary>
    /// Window X position in pixels (null = use default/center)
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Window Y position in pixels (null = use default/center)
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Window width in pixels (null = use default 1280)
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Window height in pixels (null = use default 800)
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Whether the window was maximized when last closed
    /// </summary>
    public bool Maximized { get; set; } = false;
}

/// <summary>
/// Tab-specific settings for panel layouts
/// </summary>
public class TabSettings
{
    /// <summary>
    /// Mod tab settings
    /// </summary>
    public ModTabSettings Mod { get; set; } = new();
}

/// <summary>
/// Settings specific to the Mod tab
/// </summary>
public class ModTabSettings
{
    /// <summary>
    /// Panel sizes as percentages (e.g., "20 35" means CategoryPanel=20%, ModListPanel=35%, Preview=45%)
    /// Format: "categoryWidth modListWidth" (both in percentage, preview takes remaining space)
    /// Default: "20 35" (CategoryPanel=20%, ModListPanel=35%, Preview=45%)
    /// </summary>
    public string PanelSize { get; set; } = "20 35";
}
