using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Repositories;

/// <summary>
/// Repository interface for TaskChain operations
/// </summary>
public interface ITaskChainRepository
{
    /// <summary>
    /// Add a new task chain
    /// </summary>
    Task<TaskChainInfo> AddAsync(TaskChainInfo taskChain);

    /// <summary>
    /// Get a task chain by ID
    /// </summary>
    Task<TaskChainInfo?> GetByIdAsync(string id);

    /// <summary>
    /// Get all task chains for a profile
    /// </summary>
    Task<List<TaskChainInfo>> GetByProfileAsync(string profileId);

    /// <summary>
    /// Get all active task chains (pending, processing, awaiting user action)
    /// </summary>
    Task<List<TaskChainInfo>> GetActiveAsync(string? profileId = null);

    /// <summary>
    /// Get task chains by status
    /// </summary>
    Task<List<TaskChainInfo>> GetByStatusAsync(TaskChainStatus status, string? profileId = null);

    /// <summary>
    /// Update a task chain
    /// </summary>
    Task UpdateAsync(TaskChainInfo taskChain);

    /// <summary>
    /// Delete a task chain and all its tasks
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Clear completed chains older than specified date
    /// </summary>
    Task<int> ClearCompletedAsync(DateTime olderThan, string? profileId = null);

    /// <summary>
    /// Get count of active chains of a specific type
    /// </summary>
    Task<int> GetActiveCountByTypeAsync(string chainType, string? profileId = null);

    /// <summary>
    /// Check if a chain with correlation ID exists
    /// </summary>
    Task<bool> ExistsByCorrelationIdAsync(string correlationId);
}