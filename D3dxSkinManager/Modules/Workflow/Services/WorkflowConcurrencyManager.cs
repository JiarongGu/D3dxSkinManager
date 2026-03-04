using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Manages workflow concurrency using a semaphore-based approach
/// Ensures only N workflows run in parallel, queuing others
/// </summary>
public class WorkflowConcurrencyManager : IWorkflowConcurrencyManager
{
    private readonly ILogHelper _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<string, bool> _runningWorkflows;
    private int _maxConcurrentWorkflows;

    public WorkflowConcurrencyManager(ILogHelper logger)
    {
        _logger = logger;
        _maxConcurrentWorkflows = 10; // Default: 10 concurrent workflows
        _semaphore = new SemaphoreSlim(_maxConcurrentWorkflows, _maxConcurrentWorkflows);
        _runningWorkflows = new ConcurrentDictionary<string, bool>();
    }

    public int MaxConcurrentWorkflows
    {
        get => _maxConcurrentWorkflows;
        set
        {
            if (value < 1)
                throw new ArgumentException("MaxConcurrentWorkflows must be at least 1", nameof(value));

            _maxConcurrentWorkflows = value;
            _logger.Info($"Workflow concurrency limit updated to {value}");
        }
    }

    public int CurrentRunningCount => _runningWorkflows.Count;

    public async Task<bool> TryAcquireSlotAsync(string workflowId)
    {
        // Try to acquire semaphore without blocking
        if (!await _semaphore.WaitAsync(0))
        {
            _logger.Info($"Workflow {workflowId} queued - concurrency limit reached ({CurrentRunningCount}/{MaxConcurrentWorkflows})");
            // Wait for available slot
            await _semaphore.WaitAsync();
        }

        _runningWorkflows.TryAdd(workflowId, true);
        _logger.Verbose($"Workflow {workflowId} acquired execution slot ({CurrentRunningCount}/{MaxConcurrentWorkflows})");
        return true;
    }

    public void ReleaseSlot(string workflowId)
    {
        if (_runningWorkflows.TryRemove(workflowId, out _))
        {
            _semaphore.Release();
            _logger.Verbose($"Workflow {workflowId} released execution slot ({CurrentRunningCount}/{MaxConcurrentWorkflows})");
        }
    }

    public bool CanStartWorkflow()
    {
        return _semaphore.CurrentCount > 0;
    }

    public int GetQueuedCount()
    {
        // Queued count = workflows waiting for semaphore
        var availableSlots = _semaphore.CurrentCount;
        var maxSlots = MaxConcurrentWorkflows;
        return Math.Max(0, _runningWorkflows.Count - availableSlots);
    }
}

