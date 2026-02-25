using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Represents a task in the queue
/// </summary>
public class TaskInfo
{
    /// <summary>
    /// Unique task identifier (TASK-{Guid})
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Task type identifier (e.g., "mod_import", "mod_export")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Current task status
    /// </summary>
    public TaskStatus Status { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Current status message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the task started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the task completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// JSON serialized input data
    /// </summary>
    public string InputData { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized output data (if completed successfully)
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Operation ID for progress tracking integration
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Profile context for the task
    /// </summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// Correlation ID - groups related tasks in a chain together
    /// All tasks in the same import workflow share the same correlation ID
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Chain context - defines how this task fits in a multi-phase workflow
    /// Contains information about next steps, user actions required, etc.
    /// Serialized as JSON in the database
    /// </summary>
    public string? ChainContextJson { get; set; }

    /// <summary>
    /// Deserialized chain context (not stored, populated from ChainContextJson)
    /// </summary>
    [JsonIgnore]
    public TaskChainContext? ChainContext
    {
        get
        {
            if (string.IsNullOrEmpty(ChainContextJson)) return null;
            return JsonSerializer.Deserialize<TaskChainContext>(ChainContextJson);
        }
        set
        {
            ChainContextJson = value != null ? JsonSerializer.Serialize(value) : null;
        }
    }
}
