using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Helpers over <see cref="IProcessRegistry"/> that consolidate the repeated long-op boilerplate
/// (background-task-tracking.md).
/// </summary>
public static class ProcessRegistryExtensions
{
    /// <summary>
    /// Fire-and-forget a tracked long operation: <see cref="IProcessRegistry.Start"/> a process, run
    /// <paramref name="work"/> in the background, then <c>Complete</c> on success / <c>Cancel</c> on
    /// cancellation / <c>Fail</c> on any other exception (all idempotent). Returns the process id
    /// IMMEDIATELY (never await it — that would block the IPC and time out; see background-task-tracking.md).
    /// The <paramref name="work"/> delegate reports progress via <c>registry.Report(procId, …)</c> and does
    /// its OWN try/finally for resource cleanup; <paramref name="onError"/> runs BEFORE Fail (e.g. to log).
    /// Replaces the hand-written <c>Start</c> + <c>Task.Run</c> + try/Complete/catch-Cancel/catch-Fail block.
    /// </summary>
    public static string RunTrackedAsync(
        this IProcessRegistry registry,
        ProcessType type,
        string title,
        Func<string, CancellationToken, Task> work,
        bool cancellable = false,
        string? titleKey = null,
        string? titleArg = null,
        Action<Exception>? onError = null)
    {
        var id = registry.Start(type, title, cancellable: cancellable, titleKey: titleKey, titleArg: titleArg);
        _ = Task.Run(async () =>
        {
            var ct = registry.GetToken(id);
            try
            {
                await work(id, ct).ConfigureAwait(false);
                registry.Complete(id);
            }
            catch (OperationCanceledException)
            {
                registry.Cancel(id);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                registry.Fail(id, ex.Message);
            }
        });
        return id;
    }
}
