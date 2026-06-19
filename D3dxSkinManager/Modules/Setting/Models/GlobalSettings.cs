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
    /// Whether to automatically check GitHub for a newer app version on startup.
    /// Defaults to OFF — the user opts in (manual check is always available in Settings).
    /// </summary>
    public bool AutoUpdateCheck { get; set; } = false;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Window size and position settings
    /// </summary>
    public WindowSettings Window { get; set; } = new();
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
