namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Request to create or update a screen capture profile
/// </summary>
public class SaveScreenCaptureProfileRequest
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
