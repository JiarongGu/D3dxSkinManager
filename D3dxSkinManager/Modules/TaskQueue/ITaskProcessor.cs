using D3dxSkinManager.Modules.TaskQueue.Services;

namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Generic interface for task processors
/// Implement this interface to create custom task types
/// </summary>
/// <typeparam name="TInput">Input data type</typeparam>
/// <typeparam name="TOutput">Output data type</typeparam>
public interface ITaskProcessor<TInput, TOutput>
{
    /// <summary>
    /// Process the task with progress reporting and cancellation support
    /// </summary>
    /// <param name="input">Task input data</param>
    /// <param name="progressReporter">Progress reporter for status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task output data</returns>
    Task<TOutput> ProcessAsync(
        TInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validate input data before queuing the task
    /// </summary>
    /// <param name="input">Task input data</param>
    /// <returns>True if input is valid</returns>
    Task<bool> ValidateInputAsync(TInput input);

    /// <summary>
    /// Task type identifier (e.g., "mod_import", "mod_export")
    /// </summary>
    string TaskType { get; }
}
