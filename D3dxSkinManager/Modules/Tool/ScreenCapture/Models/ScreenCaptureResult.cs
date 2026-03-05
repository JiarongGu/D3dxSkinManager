namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Result of a capture operation
/// </summary>
public class ScreenCaptureResult
{
    /// <summary>
    /// Whether the capture was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if capture failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Path to the saved image file (if SaveToFile was true)
    /// </summary>
    public string? SavedPath { get; set; }

    /// <summary>
    /// Whether the image was copied to clipboard
    /// </summary>
    public bool CopiedToClipboard { get; set; }

    /// <summary>
    /// Final capture bounds used
    /// </summary>
    public ScreenCaptureArea? CapturedArea { get; set; }

    /// <summary>
    /// Timestamp of the capture
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
