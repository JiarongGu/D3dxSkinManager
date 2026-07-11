using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Bounds workflow parallelism to N at once and, when full, admits waiters by PRIORITY rather than
/// arbitrarily. A plain SemaphoreSlim (the previous impl) gives no ordering — so a just-confirmed
/// import could sit behind older unconfirmed previews. This admits the highest-priority queued waiter
/// when a slot frees: confirmed-first, then higher-progress, then earlier-created (see
/// <see cref="WorkflowPriority"/>).
/// </summary>
public class WorkflowConcurrencyManager : IWorkflowConcurrencyManager
{
    private readonly ILogHelper _logger;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, bool> _runningWorkflows = new();
    private readonly PriorityQueue<Waiter, WorkflowPriority> _waiters = new(new PriorityComparer());
    private int _maxConcurrentWorkflows = 5; // compression is CPU intensive
    private int _running;

    public WorkflowConcurrencyManager(ILogHelper logger)
    {
        _logger = logger;
    }

    private sealed class Waiter
    {
        public required string WorkflowId { get; init; }
        public required TaskCompletionSource Tcs { get; init; }
        public bool Settled; // handed a slot OR cancelled — the release loop skips settled waiters
    }

    /// <summary>Orders so PriorityQueue.Dequeue returns the MOST important waiter first.</summary>
    private sealed class PriorityComparer : IComparer<WorkflowPriority>
    {
        public int Compare(WorkflowPriority a, WorkflowPriority b)
        {
            if (a.Confirmed != b.Confirmed) return a.Confirmed ? -1 : 1;      // confirmed first
            if (a.Progress != b.Progress) return b.Progress.CompareTo(a.Progress); // higher progress first
            return a.CreatedAtUtc.CompareTo(b.CreatedAtUtc);                   // earlier created first
        }
    }

    public int MaxConcurrentWorkflows
    {
        get { lock (_gate) return _maxConcurrentWorkflows; }
        set
        {
            if (value < 1)
                throw new ArgumentException("MaxConcurrentWorkflows must be at least 1", nameof(value));
            lock (_gate) { _maxConcurrentWorkflows = value; }
            _logger.Info($"Workflow concurrency limit updated to {value}");
        }
    }

    public int CurrentRunningCount { get { lock (_gate) return _running; } }

    public Task TryAcquireSlotAsync(string workflowId, WorkflowPriority priority, CancellationToken cancellationToken = default)
    {
        Waiter waiter;
        lock (_gate)
        {
            if (_running < _maxConcurrentWorkflows)
            {
                _running++;
                _runningWorkflows[workflowId] = true;
                _logger.Verbose($"Workflow {workflowId} acquired execution slot ({_running}/{_maxConcurrentWorkflows})");
                return Task.CompletedTask;
            }

            waiter = new Waiter { WorkflowId = workflowId, Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
            _waiters.Enqueue(waiter, priority);
            _logger.Info($"Workflow {workflowId} queued (priority: confirmed={priority.Confirmed}, progress={priority.Progress}) — {_running}/{_maxConcurrentWorkflows} running");
        }

        // Cancellation while queued: settle the waiter as cancelled so the release loop skips it.
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                lock (_gate)
                {
                    if (waiter.Settled) return;
                    waiter.Settled = true;
                }
                waiter.Tcs.TrySetCanceled(cancellationToken);
            });
        }

        return AwaitSlotAsync(waiter);
    }

    private async Task AwaitSlotAsync(Waiter waiter)
    {
        await waiter.Tcs.Task.ConfigureAwait(false); // throws if cancelled; otherwise the slot is ours
        lock (_gate) { _runningWorkflows[waiter.WorkflowId] = true; }
    }

    public void ReleaseSlot(string workflowId)
    {
        lock (_gate)
        {
            if (!_runningWorkflows.TryRemove(workflowId, out _))
                return;

            // Hand the freed slot to the highest-priority live waiter (transfer — _running unchanged).
            while (_waiters.TryDequeue(out var next, out _))
            {
                if (next.Settled) continue; // cancelled while queued — its slot was never reserved
                next.Settled = true;
                _logger.Verbose($"Workflow {next.WorkflowId} admitted from queue ({_running}/{_maxConcurrentWorkflows})");
                next.Tcs.TrySetResult();
                return;
            }

            _running--; // nobody waiting — the slot is now free
            _logger.Verbose($"Workflow {workflowId} released execution slot ({_running}/{_maxConcurrentWorkflows})");
        }
    }

    public bool CanStartWorkflow()
    {
        lock (_gate) return _running < _maxConcurrentWorkflows;
    }

    public int GetQueuedCount()
    {
        lock (_gate) return _waiters.Count;
    }
}
