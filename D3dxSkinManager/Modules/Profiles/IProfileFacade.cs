using System.Collections.Generic;
using D3dxSkinManager.Facades;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Models;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Interface for Profile Management facade
/// Handles: PROFILE_GET_ALL, PROFILE_SWITCH, PROFILE_CREATE, etc.
/// Prefix: PROFILE_*
/// </summary>
public interface IProfileFacade : IModuleFacade
{

    Task<ProfileListResponse> GetAllProfilesAsync();
    Task<Profile?> GetActiveProfileAsync();
    Task<Profile?> GetProfileByIdAsync(string profileId);
    Task<Profile> CreateProfileAsync(CreateProfileRequest createRequest);
    Task<bool> UpdateProfileAsync(UpdateProfileRequest updateRequest);
    Task<bool> DeleteProfileAsync(string profileId);
    Task<ProfileSwitchResult> SwitchProfileAsync(string profileId);
    Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName);
    Task<string> ExportProfileConfigAsync(string profileId);
    Task<ProfileConfiguration?> GetProfileConfigAsync(string profileId);
    Task<bool> UpdateProfileConfigAsync(ProfileConfiguration config);
}
