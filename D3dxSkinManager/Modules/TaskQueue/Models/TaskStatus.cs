namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Task execution status
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task is queued and waiting to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Task is currently being processed
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Task completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Task failed with an error
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Task was cancelled by user
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Task completed preparation phase and is awaiting user confirmation
    /// (e.g., folder compressed, awaiting import confirmation)
    /// </summary>
    AwaitingConfirmation = 5
}
