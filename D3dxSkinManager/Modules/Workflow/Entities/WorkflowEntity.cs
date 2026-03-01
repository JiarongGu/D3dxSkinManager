using System.ComponentModel.DataAnnotations;

namespace D3dxSkinManager.Modules.Workflow.Entities;

/// <summary>
/// Database entity for workflow information
/// Simple, generic storage for all workflow types
/// </summary>
public class WorkflowEntity
{
    /// <summary>
    /// Unique identifier for the workflow
    /// </summary>
    [Key]
    public required string Id { get; set; }

    /// <summary>
    /// Type of workflow (e.g., "MOD_IMPORT", "BATCH_EXPORT")
    /// Each type has its own handler with specific logic
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Current status of the workflow
    /// </summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>
    /// Context data for workflow execution (JSON serialized)
    /// Structure is specific to each workflow type
    /// </summary>
    public string Context { get; set; } = "{}";

    /// <summary>
    /// Error message if workflow failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the workflow was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the workflow completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Status of a workflow
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// Workflow is waiting to start
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Workflow is currently processing
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Workflow is waiting for user input
    /// </summary>
    WaitingForInput = 2,

    /// <summary>
    /// Workflow completed successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Workflow failed with error
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Workflow was cancelled by user
    /// </summary>
    Cancelled = 5
}
