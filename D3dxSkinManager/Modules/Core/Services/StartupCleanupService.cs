using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// One APP-LEVEL startup cleanup/migration step. Register implementations in
/// <c>CoreServiceExtensions</c> (multiple <c>AddSingleton&lt;IStartupCleanupStep, …&gt;</c>) and the
/// runner executes them in registration order — this is THE central place for "sweep a leftover /
/// migrate a legacy file on startup" work; don't scatter one-off cleanup into bootstrap code.
/// (Profile-level lazy upgrades — seed field fills, legacy-binding upgrades, plaintext-cookie
/// re-protection — stay in their stores, which upgrade on first read.)
/// </summary>
public interface IStartupCleanupStep
{
    /// <summary>Short name for logs (e.g. "managed-downloads").</summary>
    string Name { get; }

    Task RunAsync();
}

/// <summary>
/// App self-cleanup, run once at startup (from ApplicationHost, before eager loading). Executes every
/// registered <see cref="IStartupCleanupStep"/>, each isolated + non-fatal: one failure never blocks
/// the other steps or startup.
/// </summary>
public interface IStartupCleanupService
{
    Task RunAsync();
}

public class StartupCleanupService : IStartupCleanupService
{
    private readonly IReadOnlyList<IStartupCleanupStep> _steps;
    private readonly ILogHelper _logger;

    public StartupCleanupService(IEnumerable<IStartupCleanupStep> steps, ILogHelper logger)
    {
        _steps = steps.ToList();
        _logger = logger;
    }

    public async Task RunAsync()
    {
        foreach (var step in _steps)
        {
            try
            {
                await step.RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Startup cleanup step '{step.Name}' failed: {ex.Message}", "StartupCleanup");
            }
        }
    }
}

/// <summary>Remove managed downloads older than the retention window ({data}/downloads is a
/// self-cleaning scratch area — see download-service.md).</summary>
public class ManagedDownloadsCleanupStep : IStartupCleanupStep
{
    private static readonly TimeSpan ManagedDownloadRetention = TimeSpan.FromDays(7);

    private readonly IDownloadService _downloadService;
    private readonly ILogHelper _logger;

    public ManagedDownloadsCleanupStep(IDownloadService downloadService, ILogHelper logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }

    public string Name => "managed-downloads";

    public Task RunAsync()
    {
        var result = _downloadService.CleanupManaged(ManagedDownloadRetention);
        if (result.DeletedCount > 0)
        {
            _logger.Info(
                $"Startup cleanup: removed {result.DeletedCount} stale download(s), freed {result.BytesFreed} bytes",
                "StartupCleanup");
        }
        return Task.CompletedTask;
    }
}

/// <summary>Delete an ORPHANED update staging dir ({install}/.update with no ready.json). A complete
/// pending update is left for the launcher to apply on the next launch.</summary>
public class OrphanedUpdateStagingCleanupStep : IStartupCleanupStep
{
    private readonly IAppEnvironment _appEnvironment;
    private readonly ILogHelper _logger;

    public OrphanedUpdateStagingCleanupStep(IAppEnvironment appEnvironment, ILogHelper logger)
    {
        _appEnvironment = appEnvironment;
        _logger = logger;
    }

    public string Name => "update-staging";

    public Task RunAsync()
    {
        var stagingRoot = Path.Combine(_appEnvironment.BaseDirectory, ".update");
        if (!Directory.Exists(stagingRoot)) return Task.CompletedTask;
        if (File.Exists(Path.Combine(stagingRoot, "ready.json"))) return Task.CompletedTask;

        Directory.Delete(stagingRoot, recursive: true);
        _logger.Info("Startup cleanup: removed orphaned update staging", "StartupCleanup");
        return Task.CompletedTask;
    }
}

/// <summary>Delete the LEGACY {data}/process-state.json. The ProcessRegistry is purely in-memory
/// since 2026-07-10 — crash-interrupted resumable ops are re-announced from their PROFILE-DB
/// checkpoints (e.g. analysis sessions left "running"), so no global snapshot file exists.</summary>
public class LegacyProcessStateCleanupStep : IStartupCleanupStep
{
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    public LegacyProcessStateCleanupStep(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public string Name => "legacy-process-state";

    public Task RunAsync()
    {
        var legacy = Path.Combine(_globalPaths.BaseDataPath, "process-state.json");
        if (File.Exists(legacy))
        {
            File.Delete(legacy);
            _logger.Info("Startup cleanup: removed legacy process-state.json (profile DBs hold resumable checkpoints)", "StartupCleanup");
        }
        return Task.CompletedTask;
    }
}
