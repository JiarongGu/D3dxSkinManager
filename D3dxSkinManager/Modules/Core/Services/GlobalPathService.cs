using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Service for providing standardized application-level directory paths
/// Centralizes all path logic for global (non-profile) directories
/// All paths are absolute and ready for file operations
/// </summary>
public interface IGlobalPathService
{
    /// <summary>
    /// Base data directory (e.g., {app}/data/)
    /// </summary>
    string BaseDataPath { get; }

    /// <summary>
    /// Profiles directory (data/profiles/)
    /// </summary>
    string ProfilesDirectory { get; }

    /// <summary>
    /// Profiles configuration file (data/settings/profiles.json)
    /// </summary>
    string ProfilesConfigPath { get; }

    /// <summary>
    /// Global settings directory (data/settings/)
    /// </summary>
    string GlobalSettingsDirectory { get; }

    /// <summary>
    /// Global settings file path (data/settings/global.json)
    /// </summary>
    string GlobalSettingsFilePath { get; }

    /// <summary>
    /// Frontend assets directory (wwwroot/)
    /// </summary>
    string FrontendPath { get; }

    /// <summary>
    /// Frontend index.html path (wwwroot/index.html)
    /// </summary>
    string FrontendIndexPath { get; }

    /// <summary>
    /// Ensure all standard global directories exist
    /// Should be called during application initialization
    /// </summary>
    void EnsureDirectoriesExist();

    // Helper methods for parameterized paths

    /// <summary>
    /// Get path for a specific profile directory by profile ID
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>Full path to profile directory</returns>
    string GetProfileDirectoryPath(string profileId);

    /// <summary>
    /// Get path for a specific profile's configuration file
    /// </summary>
    /// <param name="profileId">Profile ID</param>
    /// <returns>Full path to profile config.json</returns>
    string GetProfileConfigPath(string profileId);

    /// <summary>
    /// Get path for a global settings file by name
    /// </summary>
    /// <param name="settingsFileName">Settings file name</param>
    /// <returns>Full path to settings file</returns>
    string GetGlobalSettingsFilePath(string settingsFileName);
}

/// <summary>
/// Implementation of GlobalPathService
/// Provides centralized access to all application-level directory paths
/// All paths are absolute and constructed relative to the base data path
/// </summary>
public class GlobalPathService : IGlobalPathService
{
    private readonly AppEnvironment _environment;

    public GlobalPathService(AppEnvironment appEnvironment)
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
    /// Get path for a global settings file by name
    /// </summary>
    public string GetGlobalSettingsFilePath(string settingsFileName)
    {
        return Path.Combine(GlobalSettingsDirectory, settingsFileName);
    }
}
