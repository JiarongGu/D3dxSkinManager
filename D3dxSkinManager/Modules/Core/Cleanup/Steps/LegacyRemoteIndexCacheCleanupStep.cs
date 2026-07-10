using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Cleanup.Steps;

/// <summary>Delete the LEGACY {data}/remote-sources/.cache dir — the remote-index v1 JSON cache,
/// superseded by the per-profile SQLite index (migration 202607050002). Re-syncable data; nothing
/// reads it anymore.</summary>
public class LegacyRemoteIndexCacheCleanupStep : IStartupCleanupStep
{
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    public LegacyRemoteIndexCacheCleanupStep(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public string Name => "legacy-remote-index-cache";

    public Task RunAsync()
    {
        var legacy = Path.Combine(_globalPaths.RemoteSourcesDirectory, ".cache");
        if (Directory.Exists(legacy))
        {
            Directory.Delete(legacy, recursive: true);
            _logger.Info("Startup cleanup: removed legacy remote-sources/.cache (index lives in the per-profile SQLite)", "StartupCleanup");
        }
        return Task.CompletedTask;
    }
}
