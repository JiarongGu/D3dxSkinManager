using System.Collections.Generic;

namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Defines how tasks in a chain should be processed
/// Stored with the task to determine chain behavior
/// </summary>
public class TaskChainContext
{
    /// <summary>
    /// Correlation ID that groups all tasks in this chain
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Current phase number (1-based)
    /// </summary>
    public int CurrentPhase { get; set; } = 1;

    /// <summary>
    /// Total number of phases in this chain
    /// </summary>
    public int TotalPhases { get; set; } = 1;

    /// <summary>
    /// Whether this phase requires user action before continuing to next phase
    /// If true, chain pauses after completion and waits for user to confirm/provide input
    /// </summary>
    public bool RequiresUserAction { get; set; } = false;

    /// <summary>
    /// Next task type to create when this phase completes
    /// Null if this is the final phase or if RequiresUserAction is true
    /// </summary>
    public string? NextTaskType { get; set; }

    /// <summary>
    /// Shared context data passed between chain phases
    /// e.g., temp file paths, intermediate results
    /// </summary>
    public Dictionary<string, object>? SharedData { get; set; }

    /// <summary>
    /// Description of what user action is required (if RequiresUserAction is true)
    /// e.g., "Review and configure metadata before import"
    /// </summary>
    public string? UserActionDescription { get; set; }
}

/// <summary>
/// Request to continue a paused chain after user provides input
/// </summary>
public class ContinueChainRequest
{
    /// <summary>
    /// Correlation ID of the chain to continue
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the task that paused (the last completed task)
    /// </summary>
    public string PausedTaskId { get; set; } = string.Empty;

    /// <summary>
    /// User-provided input data for the next phase
    /// </summary>
    public Dictionary<string, object>? UserInput { get; set; }
}
