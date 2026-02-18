using System;
using System.IO;
using System.Text.Json;
using D3dxSkinManager.Modules.Profiles.Models;

namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Interface for profile context service
/// </summary>
public interface IProfileContext
{
    /// <summary>
    /// Get the currently active profile
    /// </summary>
    Profile GetActiveProfile();

    /// <summary>
    /// Get profile-specific data directory path
    /// Format: {baseDataPath}/profiles/{profileId}/
    /// </summary>
    string GetProfileDataPath(string? profileId = null);

    /// <summary>
    /// Switch to a different profile
    /// </summary>
    void SetActiveProfile(string profileId);

    /// <summary>
    /// Get the base data directory (without profile path)
    /// </summary>
    string GetBaseDataPath();

    /// <summary>
    /// Ensure profile data directory exists
    /// </summary>
    void EnsureProfileDirectories(string? profileId = null);
}

/// <summary>
/// Service for managing active profile context
/// Provides profile-specific paths for all data operations
/// </summary>
public class ProfileContext : IProfileContext
{
    private readonly string _baseDataPath;
    private Profile _activeProfile;
    private readonly IProfileService _profileService;

    public ProfileContext(string baseDataPath, IProfileService profileService)
    {
        _baseDataPath = baseDataPath ?? throw new ArgumentNullException(nameof(baseDataPath));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));

        // Ensure base profiles directory exists
        var profilesDir = Path.Combine(_baseDataPath, "profiles");
        Directory.CreateDirectory(profilesDir);

        // Load or create default profile
        _activeProfile = LoadOrCreateDefaultProfile();
        EnsureProfileDirectories();
    }

    public Profile GetActiveProfile()
    {
        return _activeProfile;
    }

    public string GetProfileDataPath(string? profileId = null)
    {
        var id = profileId ?? _activeProfile.Id;
        return Path.Combine(_baseDataPath, "profiles", id);
    }

    public void SetActiveProfile(string profileId)
    {
        var profile = _profileService.GetProfileByIdAsync(profileId).Result;
        if (profile == null)
        {
            throw new InvalidOperationException($"Profile not found: {profileId}");
        }

        _activeProfile = profile;
        _activeProfile.IsActive = true;
        _activeProfile.LastUsedAt = DateTime.UtcNow;

        var updateRequest = new UpdateProfileRequest
        {
            ProfileId = profile.Id,
            Name = profile.Name,
            Description = profile.Description
        };
        _profileService.UpdateProfileAsync(updateRequest).Wait();

        // Ensure new profile directories exist
        EnsureProfileDirectories();

        Console.WriteLine($"[ProfileContext] Switched to profile: {profile.Name} ({profile.Id})");
    }

    public string GetBaseDataPath()
    {
        return _baseDataPath;
    }

    public void EnsureProfileDirectories(string? profileId = null)
    {
        var dataPath = GetProfileDataPath(profileId);

        // Create essential directories for profile
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(Path.Combine(dataPath, "cache"));
        Directory.CreateDirectory(Path.Combine(dataPath, "thumbnails"));
        Directory.CreateDirectory(Path.Combine(dataPath, "archives"));
        Directory.CreateDirectory(Path.Combine(dataPath, "temp"));

        Console.WriteLine($"[ProfileContext] Ensured directories for profile: {dataPath}");
    }

    /// <summary>
    /// Load or create the default profile
    /// </summary>
    private Profile LoadOrCreateDefaultProfile()
    {
        // Try to get active profile from service
        var profiles = _profileService.GetAllProfilesAsync().Result;
        var activeProfile = profiles?.Find(p => p.IsActive);

        if (activeProfile != null)
        {
            Console.WriteLine($"[ProfileContext] Loaded active profile: {activeProfile.Name} ({activeProfile.Id})");
            return activeProfile;
        }

        // No active profile, try to get default
        var defaultProfile = profiles?.Find(p => p.Id == "default");
        if (defaultProfile != null)
        {
            defaultProfile.IsActive = true;
            var updateRequest = new UpdateProfileRequest
            {
                ProfileId = defaultProfile.Id,
                Name = defaultProfile.Name,
                Description = defaultProfile.Description
            };
            _profileService.UpdateProfileAsync(updateRequest).Wait();
            Console.WriteLine($"[ProfileContext] Using default profile");
            return defaultProfile;
        }

        // Create new default profile
        Console.WriteLine($"[ProfileContext] Creating new default profile");
        var createRequest = new CreateProfileRequest
        {
            Name = "Default",
            Description = "Default profile"
        };

        var created = _profileService.CreateProfileAsync(createRequest).Result;
        return created;
    }
}
