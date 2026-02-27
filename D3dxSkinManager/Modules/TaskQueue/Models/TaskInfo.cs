using System.ComponentModel.DataAnnotations;

namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Represents a single task within a task chain.
/// Simplified model for SQLite storage without foreign key constraints.
/// </summary>
public class TaskInfo
{
    /// <summary>
    /// Unique task identifier
    /// </summary>
    [Key]
    public required string Id { get; init; }

    /// <summary>
    /// Task type (from TaskNames constants)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// ID of the parent TaskChain (no FK constraint for SQLite)
    /// </summary>
    public required string TaskChainId { get; set; }

    /// <summary>
    /// Node ID within the chain configuration
    /// </summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// Current task status
    /// </summary>
    public TaskStatus Status { get; set; }

    /// <summary>
    /// JSON serialized input data
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized output data (if completed)
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the task started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the task completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}


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