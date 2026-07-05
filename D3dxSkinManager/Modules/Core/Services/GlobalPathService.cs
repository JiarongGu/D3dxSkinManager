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

    /// <summary>Managed area for files fetched over HTTP (update packages, future plugin/asset downloads).
    /// Centralized so they can be listed + cleaned in one place (see IDownloadService.CleanupAsync).</summary>
    string DownloadsDirectory { get; }

    /// <summary>Shared fix-tool library ({data}/fixtools). Global (not per-profile) — fix tools are
    /// game-agnostic scripts, so one central place is easier to manage.</summary>
    string FixToolsDirectory { get; }

    /// <summary>Remote mod-library site adapters ({data}/remote-sources — one JSON per site).
    /// Global: a site serves multiple games; the list/game choice happens in the UI.</summary>
    string RemoteSourcesDirectory { get; }

    /// <summary>SHIPPED remote-source adapters ({data}/remote-source-seeds — csproj Content).
    /// Read-only seed source; RemoteSourceStore copies missing adapters into RemoteSourcesDirectory.</summary>
    string RemoteSourceSeedsDirectory { get; }

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

    public string DownloadsDirectory => Path.Combine(BaseDataPath, "downloads");

    // LEGACY (pre-2026-07): fix tools moved to {profile}/fixtools — kept only as the one-time
    // seed source (ModFixToolService.EnsureSeeded). Do not create or write it anymore.
    public string FixToolsDirectory => Path.Combine(BaseDataPath, "fixtools");

    public string RemoteSourcesDirectory => Path.Combine(BaseDataPath, "remote-sources");

    // Shipped, read-only default configs live under a top-level `resources/` folder (sibling of
    // `data/`), NOT inside user `data/`. Created by the build (csproj Content), not EnsureDirectoriesExist.
    // RemoteSourceStore copies any missing adapter from here into the writable RemoteSourcesDirectory.
    public string ResourcesPath => Path.Combine(_environment.BaseDirectory, "resources");

    public string RemoteSourceSeedsDirectory => Path.Combine(ResourcesPath, "remote-sources");

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
        Directory.CreateDirectory(DownloadsDirectory);
        Directory.CreateDirectory(RemoteSourcesDirectory);
        // FixToolsDirectory intentionally NOT created — legacy location (per-profile since 2026-07).
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
