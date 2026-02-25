using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

/// <summary>
/// Service for managing task queue operations
/// </summary>
public interface ITaskQueueService
{
    /// <summary>
    /// Add a task to the queue
    /// </summary>
    /// <typeparam name="TInput">Task input type</typeparam>
    /// <param name="taskType">Task type identifier</param>
    /// <param name="input">Task input data</param>
    /// <param name="profileId">Profile context (optional)</param>
    /// <param name="chainContext">Chain context for multi-phase tasks (optional)</param>
    /// <returns>Task ID</returns>
    Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null, TaskChainContext? chainContext = null);

    /// <summary>
    /// Start processing the next pending task
    /// </summary>
    Task ProcessNextTaskAsync();

    /// <summary>
    /// Cancel a running task
    /// </summary>
    /// <param name="taskId">Task ID to cancel</param>
    Task CancelTaskAsync(string taskId);

    /// <summary>
    /// Remove a task from queue
    /// </summary>
    /// <param name="taskId">Task ID to remove</param>
    Task RemoveTaskAsync(string taskId);

    /// <summary>
    /// Get all tasks
    /// </summary>
    /// <returns>List of all tasks</returns>
    Task<List<TaskInfo>> GetAllTasksAsync();

    /// <summary>
    /// Get task by ID
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <returns>Task info or null if not found</returns>
    Task<TaskInfo?> GetTaskAsync(string taskId);

    /// <summary>
    /// Clear completed and failed tasks
    /// </summary>
    Task ClearCompletedTasksAsync();

    /// <summary>
    /// Continue a paused chain after user provides input
    /// </summary>
    /// <param name="request">Continue chain request with user input</param>
    /// <returns>ID of the next task created in the chain</returns>
    Task<string> ContinueChainAsync(ContinueChainRequest request);
}
