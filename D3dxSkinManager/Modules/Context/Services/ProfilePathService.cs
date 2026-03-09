using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Core.Event;
using System;
using System.IO;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Provides absolute paths for profile-specific directories and files.
/// </summary>
public interface IProfilePathService
{
    string ProfileDatabaseFileName { get; }
    string ConfigFileName { get; }
    string ProfilePath { get; }
    string ModsDirectory { get; }
    string WorkDirectory { get; }

    /// <summary>Extracted mod folders: active ({SHA}) or disabled (DISABLED-{SHA})</summary>
    string CacheModsDirectory { get; }

    string ThumbnailsDirectory { get; }
    string PreviewsDirectory { get; }
    string LogsDirectory { get; }
    string PluginsDirectory { get; }
    string TdMigotoDirectory { get; }
    string TempDirectory { get; }
    string ProfileDatabasePath { get; }
    string ConfigPath { get; }
    string GetModArchivePath(string sha, string extension = ".7z");
    string GetPreviewDirectoryPath(string sha);
    Task LoadCacheDirectoryAsync();
    void InvalidateCacheDirectory();
}

public class ProfilePathService : IProfilePathService
{
    private readonly IGlobalPathService _globalPathService;
    private readonly IProfileContext _profileContext;
    private readonly IProfileRepository _profileRepository;
    private readonly IEventBus _eventBus;
    private readonly IMemoryCache _cache;
    private readonly string _workDirCacheKey;
    private readonly string _cacheModsDirCacheKey;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public ProfilePathService(IProfileContext profileContext, IGlobalPathService globalPathService, IProfileRepository profileRepository, IEventBus eventBus, IMemoryCache cache)
    {
        _globalPathService = globalPathService;
        _profileContext = profileContext;
        _profileRepository = profileRepository;
        _eventBus = eventBus;
        _cache = cache;

        // Use profile-specific cache keys since IMemoryCache is shared across all profiles
        _workDirCacheKey = $"WorkDirectory_{profileContext.ProfileId}";
        _cacheModsDirCacheKey = $"CacheModsDirectory_{profileContext.ProfileId}";

        EnsureDirectoriesExist();
        SubscribeToConfigChanges();
    }

    /// <summary>
    /// Subscribe to profile configuration change events
    /// </summary>
    private void SubscribeToConfigChanges()
    {
        _eventBus.Subscribe(ModuleNames.PROFILE, ProfileEvents.CONFIG_UPDATED, async (EventMessage eventMessage) =>
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
    /// Invalidate the cached directory paths
    /// Call this when profile configuration changes
    /// </summary>
    public void InvalidateCacheDirectory()
    {
        _cache.Remove(_workDirCacheKey);
        _cache.Remove(_cacheModsDirCacheKey);
    }

    // Standard file name constants
    public string ProfileDatabaseFileName => "profile.db";
    public string ConfigFileName => "config.json";

    // Directory paths
    public string ProfilePath => _globalPathService.GetProfileDirectoryPath(_profileContext.ProfileId);

    public string ModsDirectory => Path.Combine(ProfilePath, "mods");

    /// <summary>
    /// Work directory - can be internal (profile/work) or external (custom path)
    /// Reads from cache populated by LoadCacheDirectoryAsync()
    /// </summary>
    public string WorkDirectory
    {
        get
        {
            // Try to get from cache
            if (_cache.TryGetValue(_workDirCacheKey, out string? cachedPath) && cachedPath != null)
            {
                return cachedPath;
            }

            // Default to internal path initially
            // This will be updated asynchronously by LoadCacheDirectoryAsync
            return Path.Combine(ProfilePath, "work");
        }
    }

    /// <summary>
    /// Cache Mods directory - WorkDirectory/Mods subfolder
    /// Reads from cache populated by LoadCacheDirectoryAsync()
    /// </summary>
    public string CacheModsDirectory
    {
        get
        {
            // Try to get from cache
            if (_cache.TryGetValue(_cacheModsDirCacheKey, out string? cachedPath) && cachedPath != null)
            {
                return cachedPath;
            }

            // Default to internal path initially
            // This will be updated asynchronously by LoadCacheDirectoryAsync
            return Path.Combine(ProfilePath, "work", "Mods");
        }
    }

    /// <summary>
    /// Load the work and cache directory paths from configuration asynchronously
    /// This should be called during initialization
    /// Uses IMemoryCache with automatic expiration
    /// </summary>
    public async Task LoadCacheDirectoryAsync()
    {
        // Load and cache both work directory and cache mods directory
        await _cache.GetOrCreateAsync(_workDirCacheKey, async entry =>
        {
            entry.SlidingExpiration = CacheExpiry;

            try
            {
                // Load configuration to determine work directory
                var config = await _profileRepository.GetProfileConfigurationAsync(_profileContext.ProfileId).ConfigureAwait(false);

                // Determine work directory based on mode
                string workDirectory;
                if (config?.ModWork?.IsExternal() == true && !string.IsNullOrEmpty(config.ModWork.Directory))
                {
                    workDirectory = config.ModWork.Directory;
                }
                else
                {
                    // Default internal work path
                    workDirectory = Path.Combine(ProfilePath, "work");
                }

                // Ensure the work directory exists
                Directory.CreateDirectory(workDirectory);

                // Also cache the Mods subdirectory
                var modsDirectory = Path.Combine(workDirectory, "Mods");
                Directory.CreateDirectory(modsDirectory);
                _cache.Set(_cacheModsDirCacheKey, modsDirectory, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = CacheExpiry
                });

                return workDirectory;
            }
            catch
            {
                // Fallback to default if config loading fails
                var defaultWorkDirectory = Path.Combine(ProfilePath, "work");
                var defaultModsDirectory = Path.Combine(defaultWorkDirectory, "Mods");
                Directory.CreateDirectory(defaultModsDirectory);

                _cache.Set(_cacheModsDirCacheKey, defaultModsDirectory, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = CacheExpiry
                });

                return defaultWorkDirectory;
            }
        }).ConfigureAwait(false);
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

