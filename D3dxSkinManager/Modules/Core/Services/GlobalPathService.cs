using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Provides absolute paths for application-level directories (non-profile).
/// </summary>
public interface IGlobalPathService
{
    string BaseDataPath { get; }
    string ProfilesDirectory { get; }
    string ProfilesConfigPath { get; }
    string GlobalSettingsDirectory { get; }
    string GlobalSettingsFilePath { get; }
    string FrontendPath { get; }
    string FrontendIndexPath { get; }
    string LogsDirectory { get; }

    void EnsureDirectoriesExist();
    string GetProfileDirectoryPath(string profileId);
    string GetProfileConfigPath(string profileId);
    string GetProfileThumbnailsDirectory(string profileId);
    string GetGlobalSettingsFilePath(string settingsFileName);
}

public class GlobalPathService : IGlobalPathService
{
    private readonly IAppEnvironment _environment;

    public GlobalPathService(IAppEnvironment appEnvironment)
    {
        _environment = appEnvironment;
        EnsureDirectoriesExist();
    }

    // Directory paths
    public string BaseDataPath => Path.Combine(_environment.BaseDirectory, "data");

    public string ProfilesDirectory => Path.Combine(BaseDataPath, "profiles");

    public string GlobalSettingsDirectory => Path.Combine(BaseDataPath, "settings");

    public string ProfilesConfigPath => Path.Combine(GlobalSettingsDirectory, "profiles.json");

    public string GlobalSettingsFilePath => Path.Combine(GlobalSettingsDirectory, "global.json");

    public string FrontendPath => Path.Combine(_environment.BaseDirectory, "wwwroot");

    public string FrontendIndexPath => Path.Combine(FrontendPath, "index.html");

    public string LogsDirectory => Path.Combine(BaseDataPath, "logs");

    /// <summary>
    /// Ensure all standard global directories exist
    /// Creates directories if they don't exist
    /// Safe to call multiple times (idempotent)
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        // Create all standard global directories
        Directory.CreateDirectory(BaseDataPath);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(GlobalSettingsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    // Helper method implementations

    /// <summary>
    /// Get path for a specific profile directory by profile ID
    /// </summary>
    public string GetProfileDirectoryPath(string profileId)
    {
        return Path.Combine(ProfilesDirectory, profileId);
    }

    /// <summary>
    /// Get path for a specific profile's configuration file
    /// </summary>
    public string GetProfileConfigPath(string profileId)
    {
        return Path.Combine(GetProfileDirectoryPath(profileId), "config.json");
    }

    /// <summary>
    /// Get path for a specific profile's thumbnails directory
    /// </summary>
    public string GetProfileThumbnailsDirectory(string profileId)
    {
        return Path.Combine(GetProfileDirectoryPath(profileId), "thumbnails");
    }

    /// <summary>
    /// Get path for a global settings file by name
    /// </summary>
    public string GetGlobalSettingsFilePath(string settingsFileName)
    {
        return Path.Combine(GlobalSettingsDirectory, settingsFileName);
    }
}
