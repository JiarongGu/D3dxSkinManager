namespace D3dxSkinManager.Modules.Settings.Models;

/// <summary>
/// Global settings that apply across all profiles
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// Theme mode: light, dark, or auto
    /// </summary>
    public string Theme { get; set; } = "light";

    /// <summary>
    /// Annotation/tooltip level: all, more, less, off
    /// </summary>
    public string AnnotationLevel { get; set; } = "all";

    /// <summary>
    /// Log level: debug, info, warn, error
    /// </summary>
    public string LogLevel { get; set; } = "info";

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
