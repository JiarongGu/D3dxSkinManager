namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for profile switch result
/// </summary>
public class ProfileSwitchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Profile? ActiveProfile { get; set; }
    public int ModsLoaded { get; set; }
}
