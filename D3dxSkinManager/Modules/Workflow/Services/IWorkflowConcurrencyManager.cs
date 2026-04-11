namespace D3dxSkinManager.Modules.Workflow.Services;

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
    /// Try to acquire a slot for running a workflow.
    /// If at capacity, waits until a slot is available or the token is cancelled.
    /// Throws <see cref="OperationCanceledException"/> if the token is cancelled while waiting.
    /// </summary>
    Task<bool> TryAcquireSlotAsync(string workflowId, CancellationToken cancellationToken = default);

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
