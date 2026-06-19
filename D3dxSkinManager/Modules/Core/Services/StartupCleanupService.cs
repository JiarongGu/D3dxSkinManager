using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// App self-cleanup, run once at startup. Sweeps transient leftovers so the app keeps itself tidy
/// without the user managing it:
///   - stale managed downloads (older than a retention window),
///   - an orphaned update staging dir (a partial stage with no ready.json — a complete pending update
///     is left for the launcher to apply),
///   - stale process entries from a previous session (keeps resumable-interrupted ones).
/// Every step is isolated + non-fatal: one failure never blocks the others or startup.
/// </summary>
public interface IStartupCleanupService
{
    Task RunAsync();
}

public class StartupCleanupService : IStartupCleanupService
{
    // How long a managed download may sit unused before startup cleanup removes it.
    private static readonly TimeSpan ManagedDownloadRetention = TimeSpan.FromDays(7);

    private readonly IDownloadService _downloadService;
    private readonly IAppEnvironment _appEnvironment;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    public StartupCleanupService(
        IDownloadService downloadService,
        IAppEnvironment appEnvironment,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _downloadService = downloadService;
        _appEnvironment = appEnvironment;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public Task RunAsync()
    {
        CleanManagedDownloads();
        CleanOrphanedUpdateStaging();
        PurgeStaleProcesses();
        return Task.CompletedTask;
    }

    private void CleanManagedDownloads()
    {
        try
        {
            var result = _downloadService.CleanupManaged(ManagedDownloadRetention);
            if (result.DeletedCount > 0)
            {
                _logger.Info(
                    $"Startup cleanup: removed {result.DeletedCount} stale download(s), freed {result.BytesFreed} bytes",
                    "StartupCleanup");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Startup cleanup (downloads) failed: {ex.Message}", "StartupCleanup");
        }
    }

    private void CleanOrphanedUpdateStaging()
    {
        try
        {
            var stagingRoot = Path.Combine(_appEnvironment.BaseDirectory, ".update");
            if (!Directory.Exists(stagingRoot)) return;

            // A complete pending update (ready.json present) is left for the launcher to apply on the
            // next launch. Only an orphaned/partial stage (no ready.json) is swept here.
            if (File.Exists(Path.Combine(stagingRoot, "ready.json"))) return;

            Directory.Delete(stagingRoot, recursive: true);
            _logger.Info("Startup cleanup: removed orphaned update staging", "StartupCleanup");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Startup cleanup (update staging) failed: {ex.Message}", "StartupCleanup");
        }
    }

    private void PurgeStaleProcesses()
    {
        try
        {
            _processRegistry.PurgeStaleProcesses();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Startup cleanup (processes) failed: {ex.Message}", "StartupCleanup");
        }
    }
}
