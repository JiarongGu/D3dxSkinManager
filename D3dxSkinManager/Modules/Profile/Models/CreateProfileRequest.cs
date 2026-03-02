namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for creating a new profile
/// </summary>
public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? GameName { get; set; }
    public string? ThumbnailPath { get; set; } // Path to thumbnail image to copy
}
