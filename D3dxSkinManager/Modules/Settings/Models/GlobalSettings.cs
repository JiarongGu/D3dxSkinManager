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
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
