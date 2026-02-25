namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Represents progress update for a task
/// </summary>
public class TaskProgress
{
    /// <summary>
    /// Task identifier
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Current processing step
    /// </summary>
    public string? CurrentStep { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string? Message { get; set; }
}
