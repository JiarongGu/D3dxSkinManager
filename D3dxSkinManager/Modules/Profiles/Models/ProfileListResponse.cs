using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// DTO for profile list response
/// </summary>
public class ProfileListResponse
{
    public List<Profile> Profiles { get; set; } = new();
    public string ActiveProfileId { get; set; } = string.Empty;
}
