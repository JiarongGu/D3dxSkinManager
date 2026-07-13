namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// What a job handler reports after running ONE leg of a job. Informational — the actor frees the slot
/// and pumps the next job regardless of outcome; the value is for logging/metrics + intent clarity.
/// <list type="bullet">
/// <item><see cref="Completed"/> — the job is fully done (imported).</item>
/// <item><see cref="Yielded"/> — paused for user input (e.g. the import preview confirm); the row stays
///   WaitingForInput and its slot is freed until it is re-enqueued (on confirm).</item>
/// <item><see cref="Failed"/> — the leg errored; the handler already persisted the failure.</item>
/// <item><see cref="Cancelled"/> — the leg observed cancellation.</item>
/// </list>
/// </summary>
public enum JobOutcome
{
    Completed,
    Yielded,
    Failed,
    Cancelled,
}

/// <summary>
/// Processes one TYPE of import job for the <see cref="IImportQueueActor"/>. The actor owns scheduling
/// (priority, bounded concurrency, pull-next); the handler owns the WORK for one leg — loading its
/// <c>WorkflowInfo</c>, running the step, persisting status, and honoring <paramref name="ct"/>. Local
/// mod imports and remote downloads are two handlers behind the SAME queue.
/// </summary>
public interface IImportJobHandler
{
    /// <summary>The <c>WorkflowInfo.Type</c> this handler processes (e.g. <c>"MOD_IMPORT"</c>,
    /// <c>"REMOTE_IMPORT"</c>). The actor dispatches a queued job to the handler whose type matches.</summary>
    string JobType { get; }

    /// <summary>Run ONE leg of the job to its next resting point (done / waiting-for-input / failed).
    /// Owns its own DB status writes + persistence. Must honor <paramref name="ct"/> (cancel while
    /// running signals it). The actor calls this on a worker task off its own loop thread.</summary>
    Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct);
}
