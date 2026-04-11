namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Represents a screen capture profile with position and size
/// All coordinates are stored in physical screen pixels (for actual screen capture)
/// NOT logical pixels - these are the actual pixel coordinates on screen
/// </summary>
public class ScreenCaptureProfile
{
    /// <summary>
    /// Unique identifier for the profile
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the profile
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// X coordinate of the capture area in physical screen pixels
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the capture area in physical screen pixels
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Width of the capture area in physical screen pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the capture area in physical screen pixels
    /// </summary>
    public int Height { get; set; }
}
