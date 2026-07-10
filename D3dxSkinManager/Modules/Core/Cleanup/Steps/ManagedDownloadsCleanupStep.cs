using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Cleanup.Steps;

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
