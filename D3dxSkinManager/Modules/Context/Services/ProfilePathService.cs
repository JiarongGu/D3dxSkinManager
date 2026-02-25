using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Core.Event;
using System;
using System.IO;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Service for providing standardized profile directory paths
/// Centralizes all path logic for profile subdirectories
/// All paths are absolute and ready for file operations
/// </summary>
public interface IProfilePathService
{
    // Standard file name constants

    /// <summary>
    /// Standard name for profile database file (consolidated mods + classifications)
    /// </summary>
    string ProfileDatabaseFileName { get; }

    /// <summary>
    /// Standard name for profile config file
    /// </summary>
    string ConfigFileName { get; }

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
    /// Cache mods directory (data/profiles/{profileId}/work/Mods/)
    /// Contains extracted mod folders in either active ({SHA}) or disabled (DISABLED-{SHA}) state
    /// This is a cache folder that can be in loaded or unloaded/disabled mode
    /// </summary>
    string CacheModsDirectory { get; }

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
    /// 3DMigoto directory (data/profiles/{profileId}/3dmigoto/)
    /// </summary>
    string TdMigotoDirectory { get; }

    /// <summary>
    /// Temporary files directory (data/profiles/{profileId}/temp/)
    /// Used for temporary file operations like folder compression before import
    /// </summary>
    string TempDirectory { get; }

    /// <summary>
    /// Profile database path (data/profiles/{profileId}/profile.db)
    /// Contains mods, classifications, and all profile-related data
    /// </summary>
    string ProfileDatabasePath { get; }

    /// <summary>
    /// Profile configuration file path (data/profiles/{profileId}/config.json)
    /// </summary>
    string ConfigPath { get; }

    // Helper methods for parameterized paths

    /// <summary>
    /// Get path for a specific mod archive file by SHA
    /// </summary>
    /// <param name="sha">Mod SHA hash</param>
    /// <param name="extension">Archive extension (e.g., ".7z", ".zip")</param>
    /// <returns>Full path to mod archive</returns>
    string GetModArchivePath(string sha, string extension = ".7z");

    /// <summary>
    /// Get directory path for a specific mod's previews by SHA
    /// </summary>
    /// <param name="sha">Mod SHA hash</param>
    /// <returns>Full path to preview directory</returns>
    string GetPreviewDirectoryPath(string sha);

    /// <summary>
    /// Load the cache directory path from configuration asynchronously
    /// Should be called during initialization
    /// </summary>
    Task LoadCacheDirectoryAsync();

    /// <summary>
    /// Invalidate the cached cache directory path
    /// Call this when profile configuration changes (e.g., when ModCacheStorageMode or CustomModCachePath changes)
    /// </summary>
    void InvalidateCacheDirectory();
}

/// <summary>
/// Implementation of ProfilePathService
/// Provides centralized access to all profile-related directory paths
/// All paths are absolute and constructed relative to the base profile path
/// </summary>
public class ProfilePathService : IProfilePathService
{
    private readonly IGlobalPathService _globalPathService;
    private readonly IProfileContext _profileContext;
    private readonly IProfileRepository _profileRepository;
    private readonly IEventBus _eventBus;
    private string? _cachedCacheDirectory;

    public ProfilePathService(IProfileContext profileContext, IGlobalPathService globalPathService, IProfileRepository profileRepository, IEventBus eventBus)
    {
        _globalPathService = globalPathService;
        _profileContext = profileContext;
        _profileRepository = profileRepository;
        _eventBus = eventBus;
        EnsureDirectoriesExist();
        SubscribeToConfigChanges();
    }

    /// <summary>
    /// Subscribe to profile configuration change events
    /// </summary>
    private void SubscribeToConfigChanges()
    {
        _eventBus.RegisterHandler(ModuleNames.PROFILE, ProfileEvents.CONFIG_UPDATED, async (EventMessage eventMessage) =>
        {
            if (eventMessage.Payload is ProfileConfiguration config && config.ProfileId == _profileContext.ProfileId)
            {
                // Reload cache directory when config changes
                InvalidateCacheDirectory();
                await LoadCacheDirectoryAsync().ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    /// Invalidate the cached cache directory path
    /// Call this when profile configuration changes
    /// </summary>
    public void InvalidateCacheDirectory()
    {
        _cachedCacheDirectory = null;
    }

    // Standard file name constants
    public string ProfileDatabaseFileName => "profile.db";
    public string ConfigFileName => "config.json";

    // Directory paths
    public string ProfilePath => _globalPathService.GetProfileDirectoryPath(_profileContext.ProfileId);

    public string ModsDirectory => Path.Combine(ProfilePath, "mods");

    public string WorkDirectory => Path.Combine(ProfilePath, "work");

    public string CacheModsDirectory
    {
        get
        {
            // Return cached value if already loaded
            if (_cachedCacheDirectory != null)
            {
                return _cachedCacheDirectory;
            }

            // Default to internal path initially
            // This will be updated asynchronously by LoadCacheDirectoryAsync
            return Path.Combine(ProfilePath, "work", "Mods");
        }
    }

    /// <summary>
    /// Load the cache directory path from configuration asynchronously
    /// This should be called during initialization
    /// </summary>
    public async Task LoadCacheDirectoryAsync()
    {
        try
        {
            // Load configuration to determine cache directory
            var config = await _profileRepository.GetProfileConfigurationAsync(_profileContext.ProfileId).ConfigureAwait(false);

            // Determine cache directory based on mode
            if (config?.ModCache?.Mode == "External" && !string.IsNullOrEmpty(config.ModCache.Directory))
            {
                _cachedCacheDirectory = config.ModCache.Directory;
            }
            else
            {
                // Default internal path
                _cachedCacheDirectory = Path.Combine(ProfilePath, "work", "Mods");
            }

            // Ensure the directory exists
            Directory.CreateDirectory(_cachedCacheDirectory);
        }
        catch
        {
            // Fallback to default if config loading fails
            _cachedCacheDirectory = Path.Combine(ProfilePath, "work", "Mods");
            Directory.CreateDirectory(_cachedCacheDirectory);
        }
    }

    public string ThumbnailsDirectory => Path.Combine(ProfilePath, "thumbnails");

    public string PreviewsDirectory => Path.Combine(ProfilePath, "previews");

    public string LogsDirectory => Path.Combine(ProfilePath, "logs");

    public string PluginsDirectory => Path.Combine(ProfilePath, "plugins");

    public string TdMigotoDirectory => Path.Combine(ProfilePath, "3dmigoto");

    public string TempDirectory => Path.Combine(ProfilePath, "temp");

    // File paths using constants
    public string ProfileDatabasePath => Path.Combine(ProfilePath, ProfileDatabaseFileName);

    public string ConfigPath => Path.Combine(ProfilePath, ConfigFileName);


    /// <summary>
    /// Ensure all standard profile directories exist
    /// Creates directories if they don't exist
    /// Safe to call multiple times (idempotent)
    /// Note: CacheModsDirectory is created on-demand in its getter based on configuration
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        // Create all standard directories
        Directory.CreateDirectory(ModsDirectory);
        Directory.CreateDirectory(WorkDirectory);
        // CacheModsDirectory is created on-demand in the property getter
        Directory.CreateDirectory(ThumbnailsDirectory);
        Directory.CreateDirectory(PreviewsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(TempDirectory);
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
    /// Get directory path for a specific mod's previews by SHA
    /// </summary>
    public string GetPreviewDirectoryPath(string sha)
    {
        return Path.Combine(PreviewsDirectory, sha);
    }
}
