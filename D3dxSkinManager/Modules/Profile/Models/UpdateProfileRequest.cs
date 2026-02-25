namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for updating profile metadata
/// </summary>
public class UpdateProfileRequest
{
    public string ProfileId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? GameDirectory { get; set; }
    public string? WorkDirectory { get; set; }
    public string? ColorTag { get; set; }
    public string? IconName { get; set; }
    public string? GameName { get; set; }
}
