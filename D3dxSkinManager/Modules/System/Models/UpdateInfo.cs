namespace D3dxSkinManager.Modules.System.Models;

/// <summary>
/// Result of an app self-update check against the GitHub Releases API.
/// Plain DTO — serialized to the frontend as camelCase JSON.
/// </summary>
public class UpdateInfo
{
    /// <summary>Currently running app version (from the assembly), e.g. "2.4".</summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>Latest published release version (tag, leading 'v' stripped), e.g. "2.5". Empty if unknown.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>True when the latest release is strictly newer than the current version.</summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>Human-readable release title (GitHub release name), may be empty.</summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>Release notes / changelog body (Markdown). May be empty.</summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>Public URL of the release page (where the user downloads the new version).</summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>ISO-8601 publish timestamp of the latest release. May be empty.</summary>
    public string PublishedAt { get; set; } = string.Empty;

    /// <summary>
    /// True when a file-level changeset was computed (the release published a manifest AND a local
    /// installed manifest was found). When false, only the version comparison is available.
    /// </summary>
    public bool HasManifest { get; set; }

    /// <summary>Number of files that would change (added + updated + removed). Valid when HasManifest.</summary>
    public int ChangedFileCount { get; set; }

    /// <summary>Total bytes to download for the update (added + updated files). Valid when HasManifest.</summary>
    public long DownloadSize { get; set; }
}

/// <summary>
/// Whether a downloaded update is staged and waiting to be applied by the launcher on next startup.
/// </summary>
public class UpdateState
{
    public bool Pending { get; set; }
    public string PendingVersion { get; set; } = string.Empty;
}
