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
    /// Content veil: blur previews the sensitivity heuristic flags (remote cards + the mod preview
    /// panel); hover, the detail view and the fullscreen viewer reveal. Defaults to OFF — an opt-in
    /// privacy mode (streaming/shared screens); current behavior is unchanged until the user flips it.
    /// </summary>
    public bool ContentVeilEnabled { get; set; } = false;

    /// <summary>
    /// How many imports/downloads run in PARALLEL through the import queue (the ImportQueueActor's max
    /// concurrency). Compression is CPU-bound, so more isn't always faster; clamped 1–8, default 5.
    /// Applies live (a settings change updates the running queue) and to all profiles.
    /// </summary>
    public int MaxParallelImports { get; set; } = 5;

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
    // NOTE: X/Y/Width/Height are stored in LOGICAL (device-independent, 96-DPI) pixels — NOT device px.
    // WinForms window coordinates are device px at the current monitor DPI, so WindowStateService converts
    // physical→logical on save and logical→physical (× the CURRENT monitor DPI) on load. Keeping the
    // persisted value logical means a size saved at 150% restores correctly at 100%/200% — the DPI is an
    // in-memory, per-start concern, never persisted (each launch can be a different DPI).

    /// <summary>
    /// Window X position in LOGICAL pixels (null = use default/center)
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Window Y position in LOGICAL pixels (null = use default/center)
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Window width in LOGICAL pixels (null = use default 1280)
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Window height in LOGICAL pixels (null = use default 800)
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Whether the window was maximized when last closed
    /// </summary>
    public bool Maximized { get; set; } = false;
}
