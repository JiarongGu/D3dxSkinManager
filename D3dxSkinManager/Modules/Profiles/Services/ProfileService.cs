using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;

using D3dxSkinManager.Modules.Profiles.Models;
namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Service for managing mod management profiles
/// Each profile has isolated data directory and configuration
/// </summary>
public class ProfileService : IProfileService
{
    private readonly string _profilesDirectory;
    private readonly string _profilesConfigFile;
    private readonly string _baseDataPath;
    private List<Profile> _profiles;
    private string _activeProfileId;

    public ProfileService(string baseDataPath)
    {
        _baseDataPath = baseDataPath;
        _profilesDirectory = Path.Combine(baseDataPath, "profiles");
        _profilesConfigFile = Path.Combine(baseDataPath, "profiles.json");
        _profiles = new List<Profile>();
        _activeProfileId = string.Empty;

        // Ensure profiles directory exists
        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }

        // Load profiles from disk
        LoadProfilesFromDisk().Wait();
    }

    private async Task LoadProfilesFromDisk()
    {
        if (File.Exists(_profilesConfigFile))
        {
            var json = await File.ReadAllTextAsync(_profilesConfigFile);
            var data = JsonSerializer.Deserialize<ProfilesData>(json);
            if (data != null)
            {
                _profiles = data.Profiles;
                _activeProfileId = data.ActiveProfileId;
            }
        }

        // If no profiles exist, create default profile
        if (_profiles.Count == 0)
        {
            await CreateDefaultProfileAsync();
        }
    }

    private async Task SaveProfilesToDisk()
    {
        var data = new ProfilesData
        {
            Profiles = _profiles,
            ActiveProfileId = _activeProfileId
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(_profilesConfigFile, json);
    }

    private async Task CreateDefaultProfileAsync()
    {
        var profileId = "default"; // Use fixed "default" ID for the default profile
        var defaultProfile = new Profile
        {
            Id = profileId,
            Name = "Default",
            Description = "Default profile",
            WorkDirectory = Path.Combine(_baseDataPath, "work_mods"),
            DataDirectory = Path.Combine(_baseDataPath, "profiles", profileId), // Profile-based structure
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ColorTag = "#1890ff",
            IconName = "home",
            GameName = "Default"
        };

        _profiles.Add(defaultProfile);
        _activeProfileId = defaultProfile.Id;

        // Ensure profile directory structure exists
        var profileDataDir = Path.Combine(_baseDataPath, "profiles", profileId);
        Directory.CreateDirectory(profileDataDir);
        Directory.CreateDirectory(Path.Combine(profileDataDir, "cache"));
        Directory.CreateDirectory(Path.Combine(profileDataDir, "thumbnails"));
        Directory.CreateDirectory(Path.Combine(profileDataDir, "archives"));
        Directory.CreateDirectory(Path.Combine(profileDataDir, "temp"));

        await SaveProfilesToDisk();
        Console.WriteLine($"[ProfileService] Created default profile with data directory: {profileDataDir}");
    }

    public async Task<List<Profile>> GetAllProfilesAsync()
    {
        // Update statistics for all profiles
        foreach (var profile in _profiles)
        {
            await UpdateProfileStatisticsAsync(profile);
        }
        return _profiles.ToList();
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == _activeProfileId);
        if (profile != null)
        {
            await UpdateProfileStatisticsAsync(profile);
        }
        return profile;
    }

    public Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        return Task.FromResult(profile);
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest request)
    {
        var profile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            WorkDirectory = request.WorkDirectory,
            DataDirectory = Path.Combine(_profilesDirectory, Guid.NewGuid().ToString()),
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ColorTag = request.ColorTag ?? GenerateRandomColor(),
            IconName = request.IconName ?? "folder",
            GameName = request.GameName
        };

        // Create profile data directory structure
        Directory.CreateDirectory(profile.DataDirectory);
        Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "mods"));
        Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "work_mods"));
        Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "cache"));
        Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "thumbnails"));
        Directory.CreateDirectory(Path.Combine(profile.DataDirectory, "previews"));

        // Create profile configuration
        var config = new ProfileConfiguration
        {
            ProfileId = profile.Id
        };
        await SaveProfileConfigurationAsync(profile.Id, config);

        // If copyFromCurrent, copy database and config from active profile
        if (request.CopyFromCurrent)
        {
            await CopyProfileDataAsync(_activeProfileId, profile.Id);
        }

        _profiles.Add(profile);
        await SaveProfilesToDisk();

        Console.WriteLine($"[ProfileService] Created profile: {profile.Name} ({profile.Id})");
        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == request.ProfileId);
        if (profile == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.Name)) profile.Name = request.Name;
        if (request.Description != null) profile.Description = request.Description;
        if (!string.IsNullOrEmpty(request.WorkDirectory)) profile.WorkDirectory = request.WorkDirectory;
        if (request.ColorTag != null) profile.ColorTag = request.ColorTag;
        if (request.IconName != null) profile.IconName = request.IconName;
        if (request.GameName != null) profile.GameName = request.GameName;

        await SaveProfilesToDisk();
        Console.WriteLine($"[ProfileService] Updated profile: {profile.Name} ({profile.Id})");
        return true;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        if (profileId == _activeProfileId)
        {
            throw new InvalidOperationException("Cannot delete the active profile. Please switch to another profile first.");
        }

        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            return false;
        }

        // Delete profile data directory
        if (Directory.Exists(profile.DataDirectory))
        {
            Directory.Delete(profile.DataDirectory, recursive: true);
        }

        _profiles.Remove(profile);
        await SaveProfilesToDisk();

        Console.WriteLine($"[ProfileService] Deleted profile: {profile.Name} ({profile.Id})");
        return true;
    }

    public async Task<ProfileSwitchResult> SwitchProfileAsync(string profileId)
    {
        var targetProfile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (targetProfile == null)
        {
            return new ProfileSwitchResult
            {
                Success = false,
                ErrorMessage = $"Profile not found: {profileId}"
            };
        }

        // Mark old profile as inactive
        var oldProfile = _profiles.FirstOrDefault(p => p.Id == _activeProfileId);
        if (oldProfile != null)
        {
            oldProfile.IsActive = false;
        }

        // Activate new profile
        targetProfile.IsActive = true;
        targetProfile.LastUsedAt = DateTime.UtcNow;
        _activeProfileId = profileId;

        await SaveProfilesToDisk();
        await UpdateProfileStatisticsAsync(targetProfile);

        Console.WriteLine($"[ProfileService] Switched to profile: {targetProfile.Name} ({targetProfile.Id})");

        return new ProfileSwitchResult
        {
            Success = true,
            ActiveProfile = targetProfile,
            ModsLoaded = targetProfile.ModCount
        };
    }

    public async Task<Profile> GetProfileStatisticsAsync(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            throw new ArgumentException($"Profile not found: {profileId}");
        }

        await UpdateProfileStatisticsAsync(profile);
        return profile;
    }

    private async Task UpdateProfileStatisticsAsync(Profile profile)
    {
        // Count mods in profile database
        var dbPath = Path.Combine(profile.DataDirectory, "mods.db");
        if (File.Exists(dbPath))
        {
            // TODO: Query database for mod count and total size
            // For now, estimate from mods directory
            var modsDir = Path.Combine(profile.DataDirectory, "mods");
            if (Directory.Exists(modsDir))
            {
                var files = Directory.GetFiles(modsDir, "*.*", SearchOption.TopDirectoryOnly);
                profile.ModCount = files.Length;
                profile.TotalSizeBytes = files.Sum(f => new FileInfo(f).Length);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        var sourceProfile = _profiles.FirstOrDefault(p => p.Id == sourceProfileId);
        if (sourceProfile == null)
        {
            throw new ArgumentException($"Source profile not found: {sourceProfileId}");
        }

        var newProfile = new Profile
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            Description = $"Copy of {sourceProfile.Name}",
            WorkDirectory = sourceProfile.WorkDirectory,
            DataDirectory = Path.Combine(_profilesDirectory, Guid.NewGuid().ToString()),
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ColorTag = GenerateRandomColor(),
            IconName = sourceProfile.IconName,
            GameName = sourceProfile.GameName
        };

        // Create directory structure
        Directory.CreateDirectory(newProfile.DataDirectory);

        // Copy all data from source profile
        await CopyDirectoryAsync(sourceProfile.DataDirectory, newProfile.DataDirectory);

        _profiles.Add(newProfile);
        await SaveProfilesToDisk();

        Console.WriteLine($"[ProfileService] Duplicated profile: {sourceProfile.Name} -> {newProfile.Name}");
        return newProfile;
    }

    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            throw new ArgumentException($"Profile not found: {profileId}");
        }

        var config = await GetProfileConfigurationAsync(profileId);

        var exportData = new
        {
            Profile = profile,
            Configuration = config
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(exportData, options);
    }

    public async Task<Profile> ImportProfileConfigAsync(string configJson, string workDirectory)
    {
        // TODO: Implement import logic
        // Parse JSON, create profile with imported settings
        await Task.CompletedTask;
        throw new NotImplementedException("Profile import not yet implemented");
    }

    public async Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId)
    {
        var configPath = Path.Combine(_profilesDirectory, profileId, "config.json");
        if (!File.Exists(configPath))
        {
            // Return default configuration
            return new ProfileConfiguration
            {
                ProfileId = profileId
            };
        }

        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<ProfileConfiguration>(json);
    }

    public async Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config)
    {
        await SaveProfileConfigurationAsync(config.ProfileId, config);
        return true;
    }

    private async Task SaveProfileConfigurationAsync(string profileId, ProfileConfiguration config)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
        {
            throw new ArgumentException($"Profile not found: {profileId}");
        }

        var configPath = Path.Combine(profile.DataDirectory, "config.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(configPath, json);
    }

    private async Task CopyProfileDataAsync(string sourceProfileId, string targetProfileId)
    {
        var sourceProfile = _profiles.FirstOrDefault(p => p.Id == sourceProfileId);
        var targetProfile = _profiles.FirstOrDefault(p => p.Id == targetProfileId);

        if (sourceProfile == null || targetProfile == null)
        {
            return;
        }

        // Copy database file
        var sourceDb = Path.Combine(sourceProfile.DataDirectory, "mods.db");
        var targetDb = Path.Combine(targetProfile.DataDirectory, "mods.db");
        if (File.Exists(sourceDb))
        {
            File.Copy(sourceDb, targetDb, overwrite: true);
        }

        // Copy configuration
        var sourceConfig = Path.Combine(sourceProfile.DataDirectory, "config.json");
        var targetConfig = Path.Combine(targetProfile.DataDirectory, "config.json");
        if (File.Exists(sourceConfig))
        {
            File.Copy(sourceConfig, targetConfig, overwrite: true);
        }

        await Task.CompletedTask;
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
            await CopyDirectoryAsync(dir, targetSubDir);
        }
    }

    private string GenerateRandomColor()
    {
        var colors = new[] { "#1890ff", "#52c41a", "#faad14", "#f5222d", "#722ed1", "#13c2c2", "#eb2f96", "#fa8c16" };
        var random = new Random();
        return colors[random.Next(colors.Length)];
    }

    private class ProfilesData
    {
        public List<Profile> Profiles { get; set; } = new();
        public string ActiveProfileId { get; set; } = string.Empty;
    }
}
