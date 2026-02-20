using System;

namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Represents the status of a background operation
/// </summary>
public enum OperationStatus
{
    /// <summary>Operation is currently running</summary>
    Running,

    /// <summary>Operation completed successfully</summary>
    Completed,

    /// <summary>Operation failed with an error</summary>
    Failed,

    /// <summary>Operation was cancelled by user</summary>
    Cancelled
}

/// <summary>
/// Represents progress information for a long-running operation
/// Used for streaming progress updates from backend to frontend
/// </summary>
public class OperationProgress
{
    /// <summary>Unique identifier for this operation</summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>Human-readable operation name (e.g., "Loading Mod: Nahida Summer Outfit")</summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>Current status of the operation</summary>
    public OperationStatus Status { get; set; }

    /// <summary>Progress percentage (0-100)</summary>
    public int PercentComplete { get; set; }

    /// <summary>Current step description (e.g., "Extracting archive...", "Detecting format...")</summary>
    public string? CurrentStep { get; set; }

    /// <summary>When the operation started</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>When the operation completed (null if still running)</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if operation failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Additional metadata (JSON-serializable data)</summary>
    public object? Metadata { get; set; }
}

/// <summary>
/// Notification types for operation-related events
/// </summary>
public enum OperationNotificationType
{
    /// <summary>Operation was started</summary>
    OperationStarted,

    /// <summary>Progress update (percentage or step change)</summary>
    ProgressUpdate,

    /// <summary>Operation completed successfully</summary>
    OperationCompleted,

    /// <summary>Operation failed with error</summary>
    OperationFailed,

    /// <summary>Operation was cancelled</summary>
    OperationCancelled
}

/// <summary>
/// Notification payload for operation events
/// Sent via IPC to frontend
/// </summary>
public class OperationNotification
{
    /// <summary>Type of notification</summary>
    public OperationNotificationType Type { get; set; }

    /// <summary>Operation progress data</summary>
    public OperationProgress Operation { get; set; } = new();

    /// <summary>Timestamp of notification</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
