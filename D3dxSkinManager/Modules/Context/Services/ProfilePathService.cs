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

    /// <summary>Extracted mod folders: active ({Id}) or disabled (DISABLED-{Id})</summary>
    string CacheModsDirectory { get; }

    string ThumbnailsDirectory { get; }
    string PreviewsDirectory { get; }
    string LogsDirectory { get; }
    string PluginsDirectory { get; }
    string TdMigotoDirectory { get; }
    string TempDirectory { get; }
    string ProfileDatabasePath { get; }
    string ConfigPath { get; }
    string GetModArchivePath(string id, string extension = ".7z");
    string GetPreviewDirectoryPath(string id);
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
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public ProfilePathService(IProfileContext profileContext, IGlobalPathService globalPathService, IProfileRepository profileRepository, IEventBus eventBus, IMemoryCache cache)
    {
        _globalPathService = globalPathService;
        _profileContext = profileContext;
        _profileRepository = profileRepository;
        _eventBus = eventBus;
        _cache = cache;

        // Use profile-specific cache key since IMemoryCache is shared across all profiles
        _workDirCacheKey = $"WorkDirectory_{profileContext.ProfileId}";

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
                // Invalidate cache - paths will be lazily reloaded on next access via GetOrCreate
                InvalidateCacheDirectory();
            }

            // Suppress async warning - event handler doesn't need to await anything
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// Invalidate the cached work directory path
    /// Call this when profile configuration changes
    /// </summary>
    public void InvalidateCacheDirectory()
    {
        _cache.Remove(_workDirCacheKey);
    }

    // Standard file name constants
    public string ProfileDatabaseFileName => "profile.db";
    public string ConfigFileName => "config.json";

    // Directory paths
    public string ProfilePath => _globalPathService.GetProfileDirectoryPath(_profileContext.ProfileId);

    public string ModsDirectory => Path.Combine(ProfilePath, "mods");

    /// <summary>
    /// Work directory - can be internal (profile/work) or external (custom path)
    /// Uses lazy initialization with GetOrCreate for thread-safe configuration loading
    /// Creates both work directory and Mods subdirectory
    /// </summary>
    public string WorkDirectory
    {
        get
        {
            return _cache.GetOrCreate(_workDirCacheKey, entry =>
            {
                entry.SlidingExpiration = CacheExpiry;

                try
                {
                    // Use Task.Run to avoid blocking IPC thread during profile switching
                    var config = Task.Run(async () =>
                        await _profileRepository.GetProfileConfigurationAsync(_profileContext.ProfileId)
                            .ConfigureAwait(false)
                    ).GetAwaiter().GetResult();

                    string workDirectory;
                    if (config?.ModWork?.IsExternal() == true && !string.IsNullOrEmpty(config.ModWork.Directory))
                    {
                        workDirectory = config.ModWork.Directory;
                    }
                    else
                    {
                        workDirectory = Path.Combine(ProfilePath, "work");
                    }

                    // Create both work directory and Mods subdirectory
                    Directory.CreateDirectory(workDirectory);
                    Directory.CreateDirectory(Path.Combine(workDirectory, "Mods"));
                    return workDirectory;
                }
                catch
                {
                    // Fallback to default on error
                    return Path.Combine(ProfilePath, "work");
                }
            }) ?? Path.Combine(ProfilePath, "work");
        }
    }

    /// <summary>
    /// Cache Mods directory - WorkDirectory/Mods subfolder
    /// Directory is created by WorkDirectory getter, just returns the path
    /// </summary>
    public string CacheModsDirectory => Path.Combine(WorkDirectory, "Mods");


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
    /// Note: WorkDirectory and CacheModsDirectory are created on-demand via lazy initialization
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        // Create all standard directories
        Directory.CreateDirectory(ModsDirectory);
        // WorkDirectory and CacheModsDirectory are created on-demand in their property getters
        Directory.CreateDirectory(ThumbnailsDirectory);
        Directory.CreateDirectory(PreviewsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(TempDirectory);
    }

    // Helper method implementations

    /// <summary>
    /// Get path for a specific mod archive file by Id
    /// </summary>
    public string GetModArchivePath(string id, string extension = ".7z")
    {
        return Path.Combine(ModsDirectory, $"{id}{extension}");
    }

    /// <summary>
    /// Get directory path for a specific mod's previews by Id
    /// </summary>
    public string GetPreviewDirectoryPath(string id)
    {
        return Path.Combine(PreviewsDirectory, id);
    }
}

