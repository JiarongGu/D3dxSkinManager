namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Models;

/// <summary>
/// Request to show the border overlay for a profile
/// </summary>
public class ShowBorderRequest
{
    public required string ProfileId { get; set; }
}
