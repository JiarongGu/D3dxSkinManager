using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Cleanup;

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
