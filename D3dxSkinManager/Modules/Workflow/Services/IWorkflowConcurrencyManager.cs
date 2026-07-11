namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Admission priority for a queued workflow. When a slot frees, the waiter with the highest priority
/// wins: CONFIRMED imports (user hit confirm → actually importing) before unconfirmed previews, then
/// HIGHER progress first, then EARLIER-created first. (User request: confirmed + more-progressed +
/// earlier-added float to the front of the queue.)
/// </summary>
public readonly record struct WorkflowPriority(bool Confirmed, int Progress, DateTime CreatedAtUtc);

/// <summary>
/// Interface for managing workflow concurrency
/// Ensures only a limited number of workflows run in parallel
/// </summary>
public interface IWorkflowConcurrencyManager
{
    /// <summary>
    /// Maximum number of concurrent workflows allowed
    /// Default: 10
    /// </summary>
    int MaxConcurrentWorkflows { get; set; }

    /// <summary>
    /// Current number of running workflows
    /// </summary>
    int CurrentRunningCount { get; }

    /// <summary>
    /// Acquire a slot for running a workflow. If at capacity, waits until a slot frees; when it does,
    /// the highest-<see cref="WorkflowPriority"/> waiter is admitted first (not arbitrary/FIFO).
    /// Throws <see cref="OperationCanceledException"/> if the token is cancelled while waiting.
    /// </summary>
    Task TryAcquireSlotAsync(string workflowId, WorkflowPriority priority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a slot when workflow completes, fails, or is paused
    /// </summary>
    void ReleaseSlot(string workflowId);

    /// <summary>
    /// Check if a workflow can start (has available slot)
    /// </summary>
    bool CanStartWorkflow();

    /// <summary>
    /// Get count of workflows waiting in queue
    /// </summary>
    int GetQueuedCount();
}
