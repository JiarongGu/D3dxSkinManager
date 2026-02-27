namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Request to continue a paused task chain with user input
/// </summary>
public class ContinueChainRequest
{
    /// <summary>
    /// The correlation ID of the chain to continue
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// The ID of the task that's currently paused
    /// </summary>
    public required string PausedTaskId { get; init; }

    /// <summary>
    /// User-provided input data (key-value pairs)
    /// </summary>
    public Dictionary<string, object>? UserInput { get; init; }
}