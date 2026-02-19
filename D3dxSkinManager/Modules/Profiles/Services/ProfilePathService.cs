using System;
using System.IO;

namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Service for providing standardized profile directory paths
/// Centralizes all path logic for profile subdirectories
/// All paths are absolute and ready for file operations
/// </summary>
public interface IProfilePathService
{
    // Standard file name constants

    /// <summary>
    /// Standard name for mod database file
    /// </summary>
    string ModDatabaseFileName { get; }

    /// <summary>
    /// Standard name for profile config file
    /// </summary>
    string ConfigFileName { get; }

    /// <summary>
    /// Standard name for auto-detection rules file
    /// </summary>
    string AutoDetectionRulesFileName { get; }

    // Directory paths

    /// <summary>
    /// Base profile directory (e.g., data/profiles/{profileId}/)
    /// </summary>
    string ProfilePath { get; }

    /// <summary>
    /// Mod archives directory (data/profiles/{profileId}/mods/)
    /// </summary>
    string ModsDirectory { get; }

    /// <summary>
    /// Work directory base (data/profiles/{profileId}/work/)
    /// </summary>
    string WorkDirectory { get; }

    /// <summary>
    /// Work mods directory (data/profiles/{profileId}/work/Mods/)
    /// </summary>
    string WorkModsDirectory { get; }

    /// <summary>
    /// Thumbnails directory (data/profiles/{profileId}/thumbnails/)
    /// </summary>
    string ThumbnailsDirectory { get; }

    /// <summary>
    /// Previews directory (data/profiles/{profileId}/previews/)
    /// </summary>
    string PreviewsDirectory { get; }

    /// <summary>
    /// Logs directory (data/profiles/{profileId}/logs/)
    /// </summary>
    string LogsDirectory { get; }

    /// <summary>
    /// Plugins directory (data/profiles/{profileId}/plugins/)
    /// </summary>
    string PluginsDirectory { get; }

    /// <summary>
    /// Mod database path (data/profiles/{profileId}/mods.db)
    /// </summary>
    string ModDatabasePath { get; }

    /// <summary>
    /// Classifications database path (data/profiles/{profileId}/classifications.db)
    /// </summary>
    string ClassificationsDatabasePath { get; }

    /// <summary>
    /// Profile configuration file path (data/profiles/{profileId}/config.json)
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    /// Auto-detection rules file path (data/profiles/{profileId}/auto_detection_rules.json)
    /// </summary>
    string AutoDetectionRulesPath { get; }

    /// <summary>
    /// Ensure all standard profile directories exist
    /// Should be called when creating/switching profiles
    /// </summary>
    void EnsureDirectoriesExist();

    // Helper methods for parameterized paths

    /// <summary>
    /// Get path for a specific mod archive file by SHA
    /// </summary>
    /// <param name="sha">Mod SHA hash</param>
    /// <param name="extension">Archive extension (e.g., ".7z", ".zip")</param>
    /// <returns>Full path to mod archive</returns>
    string GetModArchivePath(string sha, string extension = ".7z");

    /// <summary>
    /// Get path for a specific mod thumbnail by SHA
    /// </summary>
    /// <param name="sha">Mod SHA hash</param>
    /// <param name="extension">Image extension (e.g., ".png", ".jpg")</param>
    /// <returns>Full path to thumbnail</returns>
    string GetThumbnailPath(string sha, string extension = ".png");

    /// <summary>
    /// Get directory path for a specific mod's previews by SHA
    /// </summary>
    /// <param name="sha">Mod SHA hash</param>
    /// <returns>Full path to preview directory</returns>
    string GetPreviewDirectoryPath(string sha);

    /// <summary>
    /// Get path for a specific log file by name
    /// </summary>
    /// <param name="logFileName">Log file name</param>
    /// <returns>Full path to log file</returns>
    string GetLogFilePath(string logFileName);
}

/// <summary>
/// Implementation of ProfilePathService
/// Provides centralized access to all profile-related directory paths
/// All paths are absolute and constructed relative to the base profile path
/// </summary>
public class ProfilePathService : IProfilePathService
{
    private readonly string _profilePath;

    public ProfilePathService(IProfileContext profileContext)
    {
        _profilePath = profileContext?.ProfilePath ?? throw new ArgumentNullException(nameof(profileContext));
    }

    // Standard file name constants
    public string ModDatabaseFileName => "mods.db";
    public string ClassificationsDatabaseFileName => "classifications.db";
    public string ConfigFileName => "config.json";
    public string AutoDetectionRulesFileName => "auto_detection_rules.json";

    // Directory paths
    public string ProfilePath => _profilePath;

    public string ModsDirectory => Path.Combine(_profilePath, "mods");

    public string WorkDirectory => Path.Combine(_profilePath, "work");

    public string WorkModsDirectory => Path.Combine(_profilePath, "work", "Mods");

    public string ThumbnailsDirectory => Path.Combine(_profilePath, "thumbnails");

    public string PreviewsDirectory => Path.Combine(_profilePath, "previews");

    public string LogsDirectory => Path.Combine(_profilePath, "logs");

    public string PluginsDirectory => Path.Combine(_profilePath, "plugins");

    // File paths using constants
    public string ModDatabasePath => Path.Combine(_profilePath, ModDatabaseFileName);

    public string ClassificationsDatabasePath => Path.Combine(_profilePath, ClassificationsDatabaseFileName);

    public string ConfigPath => Path.Combine(_profilePath, ConfigFileName);

    public string AutoDetectionRulesPath => Path.Combine(_profilePath, AutoDetectionRulesFileName);

    /// <summary>
    /// Ensure all standard profile directories exist
    /// Creates directories if they don't exist
    /// Safe to call multiple times (idempotent)
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        // Create all standard directories
        Directory.CreateDirectory(ModsDirectory);
        Directory.CreateDirectory(WorkDirectory);
        Directory.CreateDirectory(WorkModsDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
        Directory.CreateDirectory(PreviewsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(PluginsDirectory);
    }

    // Helper method implementations

    /// <summary>
    /// Get path for a specific mod archive file by SHA
    /// </summary>
    public string GetModArchivePath(string sha, string extension = ".7z")
    {
        return Path.Combine(ModsDirectory, $"{sha}{extension}");
    }

    /// <summary>
    /// Get path for a specific mod thumbnail by SHA
    /// </summary>
    public string GetThumbnailPath(string sha, string extension = ".png")
    {
        return Path.Combine(ThumbnailsDirectory, $"{sha}{extension}");
    }

    /// <summary>
    /// Get directory path for a specific mod's previews by SHA
    /// </summary>
    public string GetPreviewDirectoryPath(string sha)
    {
        return Path.Combine(PreviewsDirectory, sha);
    }

    /// <summary>
    /// Get path for a specific log file by name
    /// </summary>
    public string GetLogFilePath(string logFileName)
    {
        return Path.Combine(LogsDirectory, logFileName);
    }
}
