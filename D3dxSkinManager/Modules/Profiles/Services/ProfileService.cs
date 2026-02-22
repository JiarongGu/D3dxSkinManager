using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
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
    /// Switch to a different profile (set as active)
    /// </summary>
    Task<bool> SwitchProfileAsync(string profileId);

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

/// <summary>
/// Service for managing mod management profiles
/// Each profile has isolated data directory and configuration
/// Business logic layer - delegates data operations to ProfileRepository
/// </summary>
public class ProfileService : IProfileService
{
    private readonly IGlobalPathService _globalPaths;
    private readonly IPathHelper _pathHelper;
    private readonly IFileHelper _fileService;
    private readonly IProfileRepository _repository;
    private readonly ILogHelper _logger;
    private readonly Lazy<Task> _init;

    public ProfileService(
        IGlobalPathService globalPaths,
        IFileHelper fileService,
        IPathHelper pathHelper,
        IProfileRepository repository,
        ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _pathHelper = pathHelper;
        _fileService = fileService;
        _repository = repository;
        _logger = logger;

        // Lazy initialization to avoid blocking constructor
        _init = new Lazy<Task>(EnsureDefaultProfileExistsAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    private async Task EnsureDefaultProfileExistsAsync()
    {
        try
        {
            var profiles = await _repository.GetAllProfilesAsync().ConfigureAwait(false);
            if (profiles.Count == 0)
            {
                // No profiles found - create default profile
                _logger.Info("No profiles found. Creating default profile.", "ProfileService");
                await CreateProfileAsync(new CreateProfileRequest
                {
                    Name = "Default",
                    Description = "Default profile",
                    GameDirectory = string.Empty,
                    WorkDirectory = string.Empty,
                    ColorTag = "#1890ff",
                    IconName = "home",
                    GameName = "Default"
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to ensure default profile exists: {ex.Message}", "ProfileService");
        }
    }

    public async Task<List<Profile>> GetAllProfilesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetAllProfilesAsync().ConfigureAwait(false);
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var activeId = await _repository.GetActiveProfileIdAsync().ConfigureAwait(false);
        return await _repository.GetProfileAsync(activeId).ConfigureAwait(false);
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetProfileAsync(profileId).ConfigureAwait(false);
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest request)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var profileId = Guid.NewGuid().ToString();
        var dataDir = _globalPaths.GetProfileDirectoryPath(profileId);
        var workDir = string.IsNullOrEmpty(request.WorkDirectory) ? Path.Combine(dataDir, "work") : request.WorkDirectory;

        var profile = new Profile
        {
            Id = profileId,
            Name = request.Name,
            Description = request.Description,
            GameDirectory = request.GameDirectory,
            // WorkDirectory might be external (game folder) - use PathHelper to store as relative if under data path
            WorkDirectory = _pathHelper.ToRelativePath(workDir) ?? workDir,
            // DataDirectory is always under data path - store as relative
            DataDirectory = _pathHelper.ToRelativePath(dataDir) ?? dataDir,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ColorTag = request.ColorTag ?? GenerateRandomColor(),
            IconName = request.IconName ?? "folder",
            GameName = request.GameName
        };

        // Create profile in repository (this will create directory and save to disk)
        await _repository.CreateProfileAsync(profile).ConfigureAwait(false);

        // Create default profile configuration
        var config = new ProfileConfiguration
        {
            ProfileId = profile.Id
        };
        await _repository.SaveProfileConfigurationAsync(profile.Id, config).ConfigureAwait(false);

        _logger.Info($"Created profile: {profile.Name} ({profile.Id})", "ProfileService");
        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var profile = await _repository.GetProfileAsync(request.ProfileId).ConfigureAwait(false);
        if (profile == null)
        {
            return false;
        }

        // Update profile properties
        if (!string.IsNullOrEmpty(request.Name)) profile.Name = request.Name;
        if (request.Description != null) profile.Description = request.Description;
        if (request.GameDirectory != null) profile.GameDirectory = request.GameDirectory;
        if (!string.IsNullOrEmpty(request.WorkDirectory)) profile.WorkDirectory = request.WorkDirectory;
        if (request.ColorTag != null) profile.ColorTag = request.ColorTag;
        if (request.IconName != null) profile.IconName = request.IconName;
        if (request.GameName != null) profile.GameName = request.GameName;

        await _repository.UpdateProfileAsync(profile).ConfigureAwait(false);
        _logger.Info($"Updated profile: {profile.Name} ({profile.Id})", "ProfileService");
        return true;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var activeId = await _repository.GetActiveProfileIdAsync().ConfigureAwait(false);
        if (profileId == activeId)
        {
            throw new InvalidOperationException("Cannot delete the active profile. Please switch to another profile first.");
        }

        var profile = await _repository.GetProfileAsync(profileId).ConfigureAwait(false);
        if (profile == null)
        {
            return false;
        }

        // Delete profile via repository (handles directory deletion)
        await _repository.DeleteProfileAsync(profileId).ConfigureAwait(false);

        _logger.Info($"Deleted profile: {profile.Name} ({profile.Id})", "ProfileService");

        // Note: ProfileServiceProvider cleanup is handled by ProfileFacade
        // to avoid circular dependency (ProfileService cannot depend on IProfileServiceProvider)

        return true;
    }

    public async Task<bool> SwitchProfileAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        // Set the new active profile
        await _repository.SetActiveProfileIdAsync(profileId).ConfigureAwait(false);

        _logger.Info($"Switched to profile: {profileId}", "ProfileService");
        return true;
    }

    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var sourceProfile = await _repository.GetProfileAsync(sourceProfileId).ConfigureAwait(false);
        if (sourceProfile == null)
        {
            throw new ArgumentException($"Source profile not found: {sourceProfileId}");
        }

        var newProfileId = Guid.NewGuid().ToString();
        var newDataDir = _globalPaths.GetProfileDirectoryPath(newProfileId);

        var newProfile = new Profile
        {
            Id = newProfileId,
            Name = newName,
            Description = $"Copy of {sourceProfile.Name}",
            WorkDirectory = sourceProfile.WorkDirectory,
            DataDirectory = _pathHelper.ToRelativePath(newDataDir) ?? newDataDir,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ColorTag = GenerateRandomColor(),
            IconName = sourceProfile.IconName,
            GameName = sourceProfile.GameName
        };

        // Create profile in repository
        await _repository.CreateProfileAsync(newProfile).ConfigureAwait(false);

        // Copy all data from source profile
        var sourceDataDir = _pathHelper.ToAbsolutePath(sourceProfile.DataDirectory) ?? sourceProfile.DataDirectory;
        await CopyDirectoryAsync(sourceDataDir, newDataDir).ConfigureAwait(false);

        _logger.Info($"Duplicated profile: {sourceProfile.Name} -> {newProfile.Name}", "ProfileService");
        return newProfile;
    }

    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var profile = await _repository.GetProfileAsync(profileId).ConfigureAwait(false);
        if (profile == null)
        {
            throw new ArgumentException($"Profile not found: {profileId}");
        }

        var config = await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);

        var exportData = new
        {
            Profile = profile,
            Configuration = config
        };

        return JsonHelper.Serialize(exportData);
    }

    public async Task<Profile> ImportProfileConfigAsync(string configJson, string workDirectory)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // TODO: Implement import logic
        // For now, return null to indicate the operation is not supported
        _logger.Warning("Profile import requested but this feature is not yet implemented", "Profiles");

        // Parse JSON, create profile with imported settings
        await Task.CompletedTask;
        return null;
    }

    public async Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
    }

    public async Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _repository.SaveProfileConfigurationAsync(config.ProfileId, config).ConfigureAwait(false);
        return true;
    }

    private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, overwrite: true);
        }

        // Copy all subdirectories recursively
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            await CopyDirectoryAsync(dir, targetSubDir).ConfigureAwait(false);
        }
    }

    private string GenerateRandomColor()
    {
        var colors = new[] { "#1890ff", "#52c41a", "#faad14", "#f5222d", "#722ed1", "#13c2c2", "#eb2f96", "#fa8c16" };
        var random = new Random();
        return colors[random.Next(colors.Length)];
    }
}
