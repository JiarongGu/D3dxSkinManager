using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Cleanup.Steps;

/// <summary>
/// Delete the orphaned PRE-MIGRATION launcher (<c>{install}/D3dxSkinManager Launcher.exe</c>). The
/// topology moved the launcher to <c>{install}/D3dxSkinManager.exe</c> (and the runtime to
/// <c>{install}/lib/D3dxSkinManager.App.exe</c>), so after the one-time auto-update migration the
/// old-named launcher is dead weight.
///
/// Guard: only delete when the NEW launcher (<c>{install}/D3dxSkinManager.exe</c>) is present — that
/// proves the install has migrated, so we never delete the live entry point of a not-yet-migrated
/// install. The old launcher is not the running image at app boot (the new launcher spawned us), so the
/// file is unlocked; a lock still fails soft (logged, non-fatal).
/// </summary>
public class LegacyLauncherCleanupStep : IStartupCleanupStep
{
    private const string OldLauncherName = "D3dxSkinManager Launcher.exe";
    private const string NewLauncherName = "D3dxSkinManager.exe";

    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogHelper _logger;

    public LegacyLauncherCleanupStep(IAppEnvironment appEnvironment, ILogHelper logger)
    {
        _appEnvironment = appEnvironment;
        _logger = logger;
    }

    public string Name => "legacy-launcher";

    public Task RunAsync()
    {
        var installRoot = _appEnvironment.BaseDirectory;
        var oldLauncher = Path.Combine(installRoot, OldLauncherName);
        var newLauncher = Path.Combine(installRoot, NewLauncherName);

        // Only sweep once the new-topology launcher is in place — otherwise this is a pre-migration
        // install and the old launcher is still the live entry point.
        if (File.Exists(oldLauncher) && File.Exists(newLauncher))
        {
            try
            {
                File.Delete(oldLauncher);
                _logger.Info("Startup cleanup: removed orphaned legacy launcher (D3dxSkinManager Launcher.exe)", "StartupCleanup");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Startup cleanup: could not delete legacy launcher: {ex.Message}", "StartupCleanup");
            }
        }
        return Task.CompletedTask;
    }
}
