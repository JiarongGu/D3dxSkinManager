namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for updating profile metadata
/// </summary>
public class UpdateProfileRequest
{
    public string ProfileId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? GameName { get; set; }
    public string? ThumbnailPath { get; set; } // Path to thumbnail image to copy
}
