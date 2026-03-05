namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Represents a screen capture profile with position and size
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
    /// X coordinate of the capture area
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the capture area
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Width of the capture area
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the capture area
    /// </summary>
    public int Height { get; set; }
}
