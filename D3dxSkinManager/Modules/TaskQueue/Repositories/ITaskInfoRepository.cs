using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Repositories;

/// <summary>
/// Repository interface for TaskInfo operations
/// </summary>
public interface ITaskInfoRepository
{
    /// <summary>
    /// Add a new task
    /// </summary>
    Task<TaskInfo> AddAsync(TaskInfo task);

    /// <summary>
    /// Get a task by ID
    /// </summary>
    Task<TaskInfo?> GetByIdAsync(string id);

    /// <summary>
    /// Get all tasks for a chain
    /// </summary>
    Task<List<TaskInfo>> GetByChainIdAsync(string chainId);

    /// <summary>
    /// Get next pending task in a chain
    /// </summary>
    Task<TaskInfo?> GetNextPendingInChainAsync(string chainId);

    /// <summary>
    /// Get the currently processing task in a chain
    /// </summary>
    Task<TaskInfo?> GetProcessingInChainAsync(string chainId);

    /// <summary>
    /// Update a task
    /// </summary>
    Task UpdateAsync(TaskInfo task);

    /// <summary>
    /// Update task status
    /// </summary>
    Task UpdateStatusAsync(string id, Models.TaskStatus status, string? message = null);

    /// <summary>
    /// Update task progress
    /// </summary>
    Task UpdateProgressAsync(string id, float progress, string? message = null);

    /// <summary>
    /// Mark task as completed with output
    /// </summary>
    Task CompleteAsync(string id, string? outputData, DateTime completedAt);

    /// <summary>
    /// Mark task as failed with error
    /// </summary>
    Task FailAsync(string id, string errorMessage, DateTime completedAt);

    /// <summary>
    /// Delete a task
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Delete all tasks for a chain
    /// </summary>
    Task DeleteByChainIdAsync(string chainId);

    /// <summary>
    /// Get task by node ID in a chain
    /// </summary>
    Task<TaskInfo?> GetByNodeAsync(string chainId, string nodeId);
}