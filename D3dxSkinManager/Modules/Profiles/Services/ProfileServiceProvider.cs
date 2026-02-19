using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Profiles.Models;

namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Service provider for profile management operations
///
/// Responsibilities:
/// - Profile CRUD operations (Create, Read, Update, Delete)
/// - Profile configuration and metadata management
/// - Profile switching and activation
/// - Profile import/export
///
/// This service manages the profiles themselves, not the per-profile services.
/// For per-profile service management, see ProfileManager.
/// </summary>
public interface IProfileServiceProvider : IProfileService
{
    // Inherits all methods from IProfileService for backward compatibility
    // Additional methods can be added here if needed
}

/// <summary>
/// Implementation of profile service provider
/// Manages profile metadata, configuration, and lifecycle
/// </summary>
public class ProfileServiceProvider : IProfileServiceProvider
{
    private readonly IGlobalPathService _globalPaths;
    private readonly IPathHelper _pathHelper;
    private readonly ILogHelper _logger;
    private List<Profile> _profiles;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true  // Handle existing PascalCase files
    };

    public ProfileServiceProvider(IGlobalPathService globalPaths, IPathHelper pathHelper, ILogHelper logger)
    {
        _globalPaths = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));
        _pathHelper = pathHelper;
        _logger = logger;
        _profiles = new List<Profile>();

        // Ensure directories exist
        _globalPaths.EnsureDirectoriesExist();

        // Load profiles configuration
        LoadProfilesConfiguration();
    }

    #region Profile CRUD Operations

    /// <summary>
    /// Get all profiles
    /// </summary>
    public async Task<List<Profile>> GetAllProfilesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _profiles.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get currently active profile
    /// Note: In stateless architecture, there is no active profile.
    /// This method is kept for interface compatibility but always returns null.
    /// Profile ID should come from the request.
    /// </summary>
    public async Task<Profile?> GetActiveProfileAsync()
    {
        // Stateless - no active profile concept
        // Profile ID comes from each request
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Get profile by ID
    /// </summary>
    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        await _lock.WaitAsync();
        try
        {
            return _profiles.FirstOrDefault(p => p.Id == profileId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Create a new profile
    /// </summary>
    public async Task<Profile> CreateProfileAsync(CreateProfileRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Profile name is required", nameof(request));
            }

            if (_profiles.Any(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Profile with name '{request.Name}' already exists");
            }

            // Create profile
            var profileId = Guid.NewGuid().ToString("N");
            var profile = new Profile
            {
                Id = profileId,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                WorkDirectory = request.WorkDirectory ?? Path.Combine(_globalPaths.GetProfileDirectoryPath(profileId), "work"),
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                IsActive = false,
                ModCount = 0,
                TotalSizeBytes = 0
            };

            // Create profile directory structure
            var profilePath = _globalPaths.GetProfileDirectoryPath(profile.Id);
            CreateProfileDirectories(profilePath);

            // Add to profiles list
            _profiles.Add(profile);

            // Note: No active profile in stateless architecture
            // Profile ID comes from each request

            // Save configuration
            await SaveProfilesConfiguration();

            _logger.Info($"Created profile: {profile.Name} ({profile.Id})", "ProfileServiceProvider");
            return profile;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Update profile metadata
    /// </summary>
    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == request.ProfileId);
            if (profile == null)
            {
                return false;
            }

            // Update fields if provided
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                // Check for duplicate name
                if (_profiles.Any(p => p.Id != request.ProfileId &&
                    p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Profile with name '{request.Name}' already exists");
                }
                profile.Name = request.Name;
            }

            if (request.Description != null)
            {
                profile.Description = request.Description;
            }

            if (!string.IsNullOrWhiteSpace(request.WorkDirectory))
            {
                profile.WorkDirectory = request.WorkDirectory;
            }

            // Save configuration
            await SaveProfilesConfiguration();

            _logger.Info($"Updated profile: {profile.Name} ({profile.Id})", "ProfileServiceProvider");
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Delete a profile
    /// </summary>
    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        await _lock.WaitAsync();
        try
        {
            // In stateless architecture, any profile can be deleted
            // Client is responsible for ensuring no active operations

            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
            {
                return false;
            }

            // Delete profile directory
            var profilePath = _globalPaths.GetProfileDirectoryPath(profileId);
            if (Directory.Exists(profilePath))
            {
                try
                {
                    Directory.Delete(profilePath, recursive: true);
                    _logger.Info($"Deleted profile directory: {profilePath}", "ProfileServiceProvider");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error deleting profile directory: {ex.Message}", "ProfileServiceProvider", ex);
                }
            }

            // Remove from profiles list
            _profiles.Remove(profile);

            // Save configuration
            await SaveProfilesConfiguration();

            _logger.Info($"Deleted profile: {profile.Name} ({profile.Id})", "ProfileServiceProvider");
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Profile Switching

    /// <summary>
    /// Switch to a different profile
    /// Note: In stateless architecture, there's no active profile to switch.
    /// This method is kept for interface compatibility.
    /// </summary>
    public async Task<ProfileSwitchResult> SwitchProfileAsync(string profileId)
    {
        await _lock.WaitAsync();
        try
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null)
            {
                return new ProfileSwitchResult
                {
                    Success = false,
                    ErrorMessage = $"Profile not found: {profileId}"
                };
            }

            // Update last used timestamp
            profile.LastUsedAt = DateTime.UtcNow;

            // Save configuration
            await SaveProfilesConfiguration();

            // In stateless architecture, just return the profile
            // Client will use this profile ID for subsequent requests
            return new ProfileSwitchResult
            {
                Success = true,
                ActiveProfile = profile,
                ErrorMessage = null
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Profile Statistics

    /// <summary>
    /// Get profile statistics (mod count, total size)
    /// </summary>
    public async Task<Profile> GetProfileStatisticsAsync(string profileId)
    {
        var profile = await GetProfileByIdAsync(profileId);
        if (profile == null)
        {
            throw new InvalidOperationException($"Profile not found: {profileId}");
        }

        // Calculate statistics from profile directory
        var profilePath = _globalPaths.GetProfileDirectoryPath(profileId);
        if (Directory.Exists(profilePath))
        {
            var modPath = Path.Combine(profilePath, "mods");
            if (Directory.Exists(modPath))
            {
                var modFiles = Directory.GetFiles(modPath, "*", SearchOption.AllDirectories);
                profile.ModCount = modFiles.Length;
                profile.TotalSizeBytes = modFiles.Sum(f => new FileInfo(f).Length);
            }
        }

        return profile;
    }

    #endregion

    #region Profile Duplication

    /// <summary>
    /// Duplicate a profile (copy all data)
    /// </summary>
    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        var sourceProfile = await GetProfileByIdAsync(sourceProfileId);
        if (sourceProfile == null)
        {
            throw new InvalidOperationException($"Source profile not found: {sourceProfileId}");
        }

        // Create new profile (work directory will be auto-generated based on new profile ID)
        var createRequest = new CreateProfileRequest
        {
            Name = newName,
            Description = $"Duplicate of {sourceProfile.Name}",
            WorkDirectory = null // Let CreateProfileAsync auto-generate based on new profile ID
        };

        var newProfile = await CreateProfileAsync(createRequest);

        // Copy profile data
        var sourcePath = _globalPaths.GetProfileDirectoryPath(sourceProfileId);
        var targetPath = _globalPaths.GetProfileDirectoryPath(newProfile.Id);

        if (Directory.Exists(sourcePath))
        {
            await CopyDirectoryAsync(sourcePath, targetPath);
            _logger.Info($"Duplicated profile data from {sourceProfileId} to {newProfile.Id}", "ProfileServiceProvider");
        }

        return newProfile;
    }

    #endregion

    #region Import/Export

    /// <summary>
    /// Export profile configuration to JSON
    /// </summary>
    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        var profile = await GetProfileByIdAsync(profileId);
        if (profile == null)
        {
            throw new InvalidOperationException($"Profile not found: {profileId}");
        }

        // Just serialize the profile itself
        return JsonHelper.Serialize(profile);
    }

    /// <summary>
    /// Import profile from configuration JSON
    /// </summary>
    public async Task<Profile> ImportProfileConfigAsync(string configJson, string workDirectory)
    {
        // Deserialize the profile
        var profile = JsonHelper.Deserialize<Profile>(configJson);
        if (profile == null)
        {
            throw new InvalidOperationException("Invalid profile configuration");
        }

        var createRequest = new CreateProfileRequest
        {
            Name = profile.Name,
            Description = profile.Description,
            WorkDirectory = workDirectory
        };

        return await CreateProfileAsync(createRequest);
    }

    #endregion

    #region Configuration Management

    /// <summary>
    /// Get profile configuration
    /// </summary>
    public async Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId)
    {
        var profile = await GetProfileByIdAsync(profileId);
        if (profile == null)
        {
            return null;
        }

        return new ProfileConfiguration
        {
            ProfileId = profile.Id,
            GamePath = profile.GameDirectory
        };
    }

    /// <summary>
    /// Update profile configuration
    /// </summary>
    public async Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config)
    {
        // For now, just update basic settings
        var request = new UpdateProfileRequest
        {
            ProfileId = config.ProfileId
        };

        return await UpdateProfileAsync(request);
    }

    #endregion

    #region Private Helpers

    private void LoadProfilesConfiguration()
    {
        try
        {
            if (File.Exists(_globalPaths.ProfilesConfigPath))
            {
                var json = File.ReadAllText(_globalPaths.ProfilesConfigPath);
                var config = JsonHelper.Deserialize<ProfilesConfiguration>(json);
                if (config != null)
                {
                    _profiles = config.Profiles ?? new List<Profile>();
                    // Note: No active profile in stateless architecture
                    _logger.Info($"Loaded {_profiles.Count} profiles", "ProfileServiceProvider");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading profiles configuration: {ex.Message}", "ProfileServiceProvider", ex);
            _profiles = new List<Profile>();
        }
    }

    private async Task SaveProfilesConfiguration()
    {
        try
        {
            var config = new ProfilesConfiguration
            {
                Profiles = _profiles,
                // No active profile in stateless architecture
                ActiveProfileId = null
            };

            await JsonHelper.SerializeToFileAsync(_globalPaths.ProfilesConfigPath, config);
            _logger.Info($"Saved profiles configuration", "ProfileServiceProvider");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error saving profiles configuration: {ex.Message}", "ProfileServiceProvider", ex);
        }
    }

    private void CreateProfileDirectories(string profilePath)
    {
        Directory.CreateDirectory(profilePath);
        Directory.CreateDirectory(Path.Combine(profilePath, "mods"));
        Directory.CreateDirectory(Path.Combine(profilePath, "thumbnails"));
        Directory.CreateDirectory(Path.Combine(profilePath, "previews"));
        Directory.CreateDirectory(Path.Combine(profilePath, "work"));
        Directory.CreateDirectory(Path.Combine(profilePath, "logs"));
        Directory.CreateDirectory(Path.Combine(profilePath, "settings"));
    }

    private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        await Task.Run(() =>
        {
            foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, targetDir));
            }

            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(sourceDir, targetDir), true);
            }
        });
    }

    #endregion

    private class ProfilesConfiguration
    {
        public List<Profile> Profiles { get; set; } = new();
        public string? ActiveProfileId { get; set; }
    }
}