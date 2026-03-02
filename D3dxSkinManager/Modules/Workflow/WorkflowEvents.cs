namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Event type constants for Workflow module
/// </summary>
public static class WorkflowEvents
{
    /// <summary>
    /// Workflow was created
    /// Payload: Workflow
    /// </summary>
    public const string CREATED = "CREATED";

    /// <summary>
    /// Workflow status changed
    /// Payload: Workflow
    /// </summary>
    public const string STATUS_CHANGED = "STATUS_CHANGED";

    /// <summary>
    /// Workflow completed successfully
    /// Payload: Workflow
    /// </summary>
    public const string COMPLETED = "COMPLETED";

    /// <summary>
    /// Workflow failed
    /// Payload: Workflow
    /// </summary>
    public const string FAILED = "FAILED";

    /// <summary>
    /// Workflow was cancelled
    /// Payload: Workflow
    /// </summary>
    public const string CANCELLED = "CANCELLED";

    /// <summary>
    /// Workflow progress updated
    /// Payload: { workflowId: string, progress: int, step: string }
    /// </summary>
    public const string PROGRESS = "PROGRESS";

    /// <summary>
    /// Workflow was deleted
    /// Payload: workflowId (string)
    /// </summary>
    public const string DELETED = "DELETED";
}
