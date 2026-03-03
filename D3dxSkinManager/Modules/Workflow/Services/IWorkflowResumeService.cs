namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Service for managing workflow state across application restarts
/// Workflows are NOT auto-resumed on startup - they remain in their current state
/// and can be manually resumed via the UI
/// </summary>
public interface IWorkflowResumeService
{
    /// <summary>
    /// Pause all running workflows (for application shutdown)
    /// Sets Processing workflows to Pending state so they can be resumed later
    /// </summary>
    Task PauseAllWorkflowsAsync();

    /// <summary>
    /// Resume all pending/processing workflows
    /// Can be called manually from UI (e.g., "Resume All" button)
    /// </summary>
    Task ResumeAllWorkflowsAsync();
}
