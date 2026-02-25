namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Event constants for task queue notifications.
/// Used with ModuleNames.TASK_QUEUE as the module identifier.
/// Example: EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.ADDED, payload)
/// </summary>
public static class TaskQueueEvents
{
    /// <summary>
    /// Emitted when a task is added to the queue
    /// Payload: TaskInfo
    /// </summary>
    public const string ADDED = "ADDED";

    /// <summary>
    /// Emitted when a task starts processing
    /// Payload: TaskInfo
    /// </summary>
    public const string STARTED = "STARTED";

    /// <summary>
    /// Emitted when task progress updates
    /// Payload: TaskProgress
    /// </summary>
    public const string PROGRESS = "PROGRESS";

    /// <summary>
    /// Emitted when a task completes successfully
    /// Payload: TaskInfo
    /// </summary>
    public const string COMPLETED = "COMPLETED";

    /// <summary>
    /// Emitted when a task fails
    /// Payload: TaskInfo
    /// </summary>
    public const string FAILED = "FAILED";

    /// <summary>
    /// Emitted when a task is cancelled
    /// Payload: TaskInfo
    /// </summary>
    public const string CANCELLED = "CANCELLED";

    /// <summary>
    /// Emitted when a task is removed from queue
    /// Payload: TaskInfo
    /// </summary>
    public const string REMOVED = "REMOVED";

    /// <summary>
    /// Emitted when a prepare_import task completes and is awaiting user confirmation
    /// Payload: TaskInfo (with PrepareImportOutput in OutputData)
    /// </summary>
    public const string AWAITING_CONFIRMATION = "AWAITING_CONFIRMATION";
}
