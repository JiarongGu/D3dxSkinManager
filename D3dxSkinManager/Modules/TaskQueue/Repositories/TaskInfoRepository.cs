using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Repositories;

/// <summary>
/// In-memory implementation of TaskInfoRepository
/// TODO: Replace with Entity Framework implementation when DB is ready
/// </summary>
public class TaskInfoRepository : ITaskInfoRepository
{
    private readonly Dictionary<string, TaskInfo> _tasks = new();
    private readonly object _lock = new();

    public Task<TaskInfo> AddAsync(TaskInfo task)
    {
        lock (_lock)
        {
            _tasks[task.Id] = task;
            return Task.FromResult(task);
        }
    }

    public Task<TaskInfo?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            return Task.FromResult(_tasks.TryGetValue(id, out var task) ? task : null);
        }
    }

    public Task<List<TaskInfo>> GetByChainIdAsync(string chainId)
    {
        lock (_lock)
        {
            var tasks = _tasks.Values
                .Where(t => t.TaskChainId == chainId)
                .OrderBy(t => t.CreatedAt)
                .ToList();
            return Task.FromResult(tasks);
        }
    }

    public Task<TaskInfo?> GetNextPendingInChainAsync(string chainId)
    {
        lock (_lock)
        {
            var task = _tasks.Values
                .Where(t => t.TaskChainId == chainId && t.Status == Models.TaskStatus.Pending)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefault();
            return Task.FromResult(task);
        }
    }

    public Task<TaskInfo?> GetProcessingInChainAsync(string chainId)
    {
        lock (_lock)
        {
            var task = _tasks.Values
                .FirstOrDefault(t => t.TaskChainId == chainId && t.Status == Models.TaskStatus.Processing);
            return Task.FromResult(task);
        }
    }

    public Task UpdateAsync(TaskInfo task)
    {
        lock (_lock)
        {
            _tasks[task.Id] = task;
            return Task.CompletedTask;
        }
    }

    public Task UpdateStatusAsync(string id, Models.TaskStatus status, string? message = null)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                task.Status = status;
                // Message is no longer stored (handled by frontend for i18n)

                if (status == Models.TaskStatus.Processing && !task.StartedAt.HasValue)
                {
                    task.StartedAt = DateTime.UtcNow;
                }
            }
            return Task.CompletedTask;
        }
    }

    public Task UpdateProgressAsync(string id, float progress, string? message = null)
    {
        // Progress is now a runtime-only field, not stored in DB
        // This method can be used to emit progress events but won't persist
        lock (_lock)
        {
            // In a real implementation, we might emit an event here
            // For now, this is a no-op since Progress isn't stored
            return Task.CompletedTask;
        }
    }

    public Task CompleteAsync(string id, string? outputData, DateTime completedAt)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                task.Status = Models.TaskStatus.Completed;
                task.Output = outputData;
                task.CompletedAt = completedAt;
                // Progress is runtime-only, not stored
            }
            return Task.CompletedTask;
        }
    }

    public Task FailAsync(string id, string errorMessage, DateTime completedAt)
    {
        lock (_lock)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                task.Status = Models.TaskStatus.Failed;
                task.ErrorMessage = errorMessage;
                task.CompletedAt = completedAt;
            }
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(string id)
    {
        lock (_lock)
        {
            _tasks.Remove(id);
            return Task.CompletedTask;
        }
    }

    public Task DeleteByChainIdAsync(string chainId)
    {
        lock (_lock)
        {
            var taskIds = _tasks.Values
                .Where(t => t.TaskChainId == chainId)
                .Select(t => t.Id)
                .ToList();

            foreach (var id in taskIds)
            {
                _tasks.Remove(id);
            }

            return Task.CompletedTask;
        }
    }

    public Task<TaskInfo?> GetByNodeAsync(string chainId, string nodeId)
    {
        lock (_lock)
        {
            var task = _tasks.Values
                .FirstOrDefault(t => t.TaskChainId == chainId && t.NodeId == nodeId);
            return Task.FromResult(task);
        }
    }
}