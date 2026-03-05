namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Configuration for a capture operation
/// </summary>
public class ScreenCaptureConfig
{
    /// <summary>
    /// Profile ID to use for capture (optional)
    /// </summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// X coordinate (overrides profile if provided)
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Y coordinate (overrides profile if provided)
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Width (overrides profile if provided)
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height (overrides profile if provided)
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Target window process name (overrides profile if provided)
    /// </summary>
    public string? TargetWindow { get; set; }

    /// <summary>
    /// Whether to show selection UI (interactive mode)
    /// </summary>
    public bool ShowSelectionUI { get; set; }

    /// <summary>
    /// Whether to copy to clipboard
    /// </summary>
    public bool CopyToClipboard { get; set; } = true;

    /// <summary>
    /// Whether to save to file
    /// </summary>
    public bool SaveToFile { get; set; }

    /// <summary>
    /// Output file path (required if SaveToFile is true)
    /// </summary>
    public string? OutputPath { get; set; }
}
