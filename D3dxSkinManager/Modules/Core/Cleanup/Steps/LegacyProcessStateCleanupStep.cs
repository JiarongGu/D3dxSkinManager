using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Cleanup.Steps;

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
            _logger.Info("Startup cleanup: removed legacy process-state.json (registry is in-memory; profile DBs hold resumable checkpoints)", "StartupCleanup");
        }
        return Task.CompletedTask;
    }
}
