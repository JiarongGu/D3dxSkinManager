namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for creating a new profile
/// </summary>
public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? GameDirectory { get; set; }
    public string? WorkDirectory { get; set; } // Optional - defaults to {DataDirectory}/work/
    public string? ColorTag { get; set; }
    public string? IconName { get; set; }
    public string? GameName { get; set; }
    public bool CopyFromCurrent { get; set; } = false;
}
