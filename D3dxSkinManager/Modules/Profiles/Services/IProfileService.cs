using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;

using D3dxSkinManager.Modules.Profiles.Models;
namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Service for managing mod management profiles
/// Each profile has its own work directory, database, and configuration
/// Responsibility: Profile CRUD, switching, and data isolation
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Get all profiles
    /// </summary>
    Task<List<Profile>> GetAllProfilesAsync();

    /// <summary>
    /// Get currently active profile
    /// </summary>
    Task<Profile?> GetActiveProfileAsync();

    /// <summary>
    /// Get profile by ID
    /// </summary>
    Task<Profile?> GetProfileByIdAsync(string profileId);

    /// <summary>
    /// Create a new profile
    /// </summary>
    /// <param name="request">Profile creation parameters</param>
    /// <returns>Created profile</returns>
    Task<Profile> CreateProfileAsync(CreateProfileRequest request);

    /// <summary>
    /// Update profile metadata
    /// </summary>
    Task<bool> UpdateProfileAsync(UpdateProfileRequest request);

    /// <summary>
    /// Delete a profile (cannot delete active profile)
    /// </summary>
    Task<bool> DeleteProfileAsync(string profileId);

    /// <summary>
    /// Switch to a different profile
    /// </summary>
    /// <param name="profileId">Target profile ID</param>
    /// <returns>Switch result with new active profile</returns>
    Task<ProfileSwitchResult> SwitchProfileAsync(string profileId);

    /// <summary>
    /// Get profile statistics (mod count, total size)
    /// </summary>
    Task<Profile> GetProfileStatisticsAsync(string profileId);

    /// <summary>
    /// Duplicate a profile (copy all data)
    /// </summary>
    Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName);

    /// <summary>
    /// Export profile configuration to JSON
    /// </summary>
    Task<string> ExportProfileConfigAsync(string profileId);

    /// <summary>
    /// Import profile from configuration JSON
    /// </summary>
    Task<Profile> ImportProfileConfigAsync(string configJson, string workDirectory);

    /// <summary>
    /// Get profile configuration
    /// </summary>
    Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId);

    /// <summary>
    /// Update profile configuration
    /// </summary>
    Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config);
}
