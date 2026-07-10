using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Cleanup.Steps;

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

        // A complete pending update (ready.json present) is left for the launcher to apply on the
        // next launch. Only an orphaned/partial stage (no ready.json) is swept here.
        if (File.Exists(Path.Combine(stagingRoot, "ready.json"))) return Task.CompletedTask;

        Directory.Delete(stagingRoot, recursive: true);
        _logger.Info("Startup cleanup: removed orphaned update staging", "StartupCleanup");
        return Task.CompletedTask;
    }
}
