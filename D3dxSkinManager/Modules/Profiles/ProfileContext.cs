using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Interface for profile context service
/// </summary>
public interface IProfileContext
{
    /// <summary>
    /// Get the currently active profile
    /// </summary>
    Task<Profile?> GetProfileAsync();

    /// <summary>
    /// Get ProfileId of the currently active profile
    /// </summary>
    string ProfileId { get; }


    /// <summary>
    /// Get the base path for the currently active profile
    /// </summary>
    string ProfilePath { get; }

    /// <summary>
    /// Service for managing active profile context
    /// Provides profile-specific paths for all data operations
    /// </summary>
}
public class ProfileContext : IProfileContext
{
    private readonly string _profilesPath;
    private readonly string _profileId;
    private readonly IProfileService _profileService;

    public ProfileContext(string profileId, string profilePath, IProfileService profileService)
    {
        _profileService = profileService;
        _profileId = profileId;
        _profilesPath = profilePath;
    }

    public string ProfileId => _profileId;

    public string ProfilePath => _profilesPath;

    public Task<Profile?> GetProfileAsync()
    {
        return _profileService.GetProfileByIdAsync(_profileId);
    }
}