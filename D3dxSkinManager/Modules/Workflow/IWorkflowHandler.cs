using D3dxSkinManager.Modules.Workflow.Models;

namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Interface for workflow handlers
/// Each workflow type (e.g., ModImport, ModExport) implements this interface
/// </summary>
public interface IWorkflowHandler
{
    /// <summary>
    /// The workflow type this handler manages (e.g., "MOD_IMPORT")
    /// </summary>
    string WorkflowType { get; }

    /// <summary>
    /// Start a new workflow instance
    /// </summary>
    /// <param name="initialData">JSON string containing initial data for the workflow</param>
    /// <returns>The created workflow info</returns>
    Task<WorkflowInfo> StartAsync(string initialData);

    /// <summary>
    /// Continue/Resume a paused workflow
    /// </summary>
    /// <param name="workflowId">The workflow ID to continue</param>
    /// <returns>The updated workflow info</returns>
    Task<WorkflowInfo> ContinueAsync(string workflowId);

    /// <summary>
    /// Pause a running workflow
    /// </summary>
    /// <param name="workflowId">The workflow ID to pause</param>
    /// <returns>The updated workflow info</returns>
    Task<WorkflowInfo> PauseAsync(string workflowId);

    /// <summary>
    /// Cancel a workflow
    /// </summary>
    /// <param name="workflowId">The workflow ID to cancel</param>
    /// <returns>The updated workflow info</returns>
    Task<WorkflowInfo> CancelAsync(string workflowId);

    /// <summary>
    /// Update workflow context (partial update of context fields)
    /// </summary>
    /// <param name="workflowId">The workflow ID to update</param>
    /// <param name="contextUpdate">JSON string containing context fields to update</param>
    /// <returns>The updated workflow info</returns>
    Task<WorkflowInfo> UpdateContextAsync(string workflowId, string contextUpdate);

    /// <summary>
    /// Resume workflow from current step (used for application restart)
    /// Similar to ContinueAsync but doesn't require WaitingForInput status
    /// </summary>
    /// <param name="workflowId">The workflow ID to resume</param>
    /// <returns>The updated workflow info</returns>
    Task<WorkflowInfo> ResumeFromCurrentStepAsync(string workflowId);
}
