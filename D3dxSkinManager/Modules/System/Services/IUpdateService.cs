using D3dxSkinManager.Modules.System.Models;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// Service for checking app self-updates against GitHub Releases and opening the release page.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Query the latest GitHub release and compare it to the running app version.
    /// </summary>
    Task<UpdateInfo> CheckForUpdateAsync();

    /// <summary>
    /// Open an http/https URL in the user's default browser (e.g. the release download page).
    /// </summary>
    Task OpenUrlAsync(string url);

    /// <summary>
    /// Download + stage the latest release so the launcher applies it on the next startup.
    /// Long-running; report progress via the ProcessRegistry.
    /// </summary>
    Task DownloadUpdateAsync();

    /// <summary>Whether a downloaded update is staged and waiting to be applied on the next startup.</summary>
    Task<UpdateState> GetUpdateStateAsync();
}
