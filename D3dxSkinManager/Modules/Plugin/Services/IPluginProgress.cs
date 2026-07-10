namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// A handle for tracking a long-running PLUGIN operation in the app's Activity panel / status bar.
/// Obtained from <see cref="IPluginContext.ReportProgress"/>; the host owns the underlying
/// ProcessRegistry entry so plugins never touch it directly (or the ProcessType enum). Dispose to
/// auto-Complete if the op wasn't already finished — so a plugin can wrap work in a `using`.
/// </summary>
public interface IPluginProgress : IDisposable
{
    /// <summary>Update percent (0–100, or null for indeterminate) and/or a detail line.</summary>
    void Report(int? percent = null, string? detail = null);

    /// <summary>Mark the operation completed (idempotent).</summary>
    void Complete();

    /// <summary>Mark the operation failed (idempotent).</summary>
    void Fail(string error);

    /// <summary>Cancellation token when the op was started cancellable (else None).</summary>
    CancellationToken Token { get; }
}
