using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Repositories;

/// <summary>
/// In-memory implementation of TaskChainRepository
/// TODO: Replace with Entity Framework implementation when DB is ready
/// </summary>
public class TaskChainRepository : ITaskChainRepository
{
    private readonly Dictionary<string, TaskChainInfo> _chains = new();
    private readonly object _lock = new();

    public Task<TaskChainInfo> AddAsync(TaskChainInfo taskChain)
    {
        lock (_lock)
        {
            _chains[taskChain.Id] = taskChain;
            return Task.FromResult(taskChain);
        }
    }

    public Task<TaskChainInfo?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_chains.TryGetValue(id, out var chain) ? chain : null);
        }
    }

    public Task<List<TaskChainInfo>> GetByProfileAsync(string profileId)
    {
        lock (_lock)
        {
            // Since TaskQueue is under profile context, all chains belong to the current profile
            var chains = _chains.Values.ToList();
            return Task.FromResult(chains);
        }
    }

    public Task<List<TaskChainInfo>> GetActiveAsync(string? profileId = null)
    {
        lock (_lock)
        {
            var activeStatuses = new[] {
                TaskChainStatus.Pending,
                TaskChainStatus.Processing
            };

            // No profile filtering needed - all chains are in the current profile context
            var chains = _chains.Values
                .Where(c => activeStatuses.Contains(c.Status))
                .ToList();

            return Task.FromResult(chains);
        }
    }

    public Task<List<TaskChainInfo>> GetByStatusAsync(TaskChainStatus status, string? profileId = null)
    {
        lock (_lock)
        {
            // No profile filtering needed - all chains are in the current profile context
            var chains = _chains.Values
                .Where(c => c.Status == status)
                .ToList();

            return Task.FromResult(chains);
        }
    }

    public Task UpdateAsync(TaskChainInfo taskChain)
    {
        lock (_lock)
        {
            _chains[taskChain.Id] = taskChain;
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(string id)
    {
        lock (_lock)
        {
            _chains.Remove(id);
            return Task.CompletedTask;
        }
    }

    public Task<int> ClearCompletedAsync(DateTime olderThan, string? profileId = null)
    {
        lock (_lock)
        {
            // No profile filtering needed - all chains are in the current profile context
            var toRemove = _chains.Values
                .Where(c => c.Status == TaskChainStatus.Completed || c.Status == TaskChainStatus.Failed)
                .Where(c => c.CompletedAt < olderThan)
                .Select(c => c.Id)
                .ToList();

            foreach (var id in toRemove)
            {
                _chains.Remove(id);
            }

            return Task.FromResult(toRemove.Count);
        }
    }

    public Task<int> GetActiveCountByTypeAsync(string chainType, string? profileId = null)
    {
        lock (_lock)
        {
            var activeStatuses = new[] {
                TaskChainStatus.Pending,
                TaskChainStatus.Processing
            };

            // No profile filtering needed - all chains are in the current profile context
            var count = _chains.Values
                .Count(c => c.ChainType == chainType &&
                           activeStatuses.Contains(c.Status));

            return Task.FromResult(count);
        }
    }

    public Task<bool> ExistsByCorrelationIdAsync(string correlationId)
    {
        lock (_lock)
        {
            // Since Context is now JSON and we don't have a correlation ID in the simplified model,
            // this always returns false for now
            // TODO: Consider adding a separate CorrelationId field if needed
            return Task.FromResult(false);
        }
    }
}