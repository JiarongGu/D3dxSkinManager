using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Handlers;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// Service for managing workflow state across application restarts
/// Workflows persist across restarts and can be manually resumed via UI
/// </summary>
public class WorkflowResumeService : IWorkflowResumeService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IEnumerable<IWorkflowHandler> _handlers;
    private readonly ILogHelper _logger;

    public WorkflowResumeService(
        IWorkflowRepository workflowRepository,
        IEnumerable<IWorkflowHandler> handlers,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task ResumeAllWorkflowsAsync()
    {
        try
        {
            _logger.Info("Resuming all pending/processing workflows...");

            // Get all workflow types
            var handlerDict = _handlers.ToDictionary(h => h.WorkflowType, h => h);

            foreach (var workflowType in handlerDict.Keys)
            {
                // Get all active workflows (Pending, Processing)
                // Note: WaitingForInput workflows should not auto-resume as they need user action
                var workflows = await _workflowRepository.GetActiveByTypeAsync(workflowType);

                var pendingOrProcessing = workflows
                    .Where(w => w.Status == WorkflowStatus.Pending || w.Status == WorkflowStatus.Processing)
                    .ToList();

                if (pendingOrProcessing.Count == 0)
                    continue;

                _logger.Info($"Found {pendingOrProcessing.Count} {workflowType} workflow(s) to resume");

                var handler = handlerDict[workflowType];

                foreach (var workflow in pendingOrProcessing)
                {
                    try
                    {
                        _logger.Info($"Resuming workflow {workflow.Id} (Type: {workflow.Type}, Status: {workflow.Status})");

                        // Resume workflow from current step
                        // This will trigger async processing with concurrency control
                        await handler.ResumeFromCurrentStepAsync(workflow.Id);
                        _logger.Info($"Successfully triggered resume for workflow {workflow.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to resume workflow {workflow.Id}: {ex.Message}", "WorkflowResumeService", ex);

                        // Mark as failed
                        workflow.Status = WorkflowStatus.Failed;
                        workflow.ErrorMessage = $"Failed to resume: {ex.Message}";
                        workflow.CompletedAt = DateTime.UtcNow;
                        await _workflowRepository.UpdateAsync(workflow);
                    }
                }
            }

            _logger.Info("Workflow resume completed");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to resume workflows: {ex.Message}", "WorkflowResumeService", ex);
        }
    }

    public async Task PauseAllWorkflowsAsync()
    {
        try
        {
            _logger.Info("Pausing all running workflows for shutdown...");

            var handlerDict = _handlers.ToDictionary(h => h.WorkflowType, h => h);

            foreach (var workflowType in handlerDict.Keys)
            {
                var workflows = await _workflowRepository.GetActiveByTypeAsync(workflowType);

                var processing = workflows
                    .Where(w => w.Status == WorkflowStatus.Processing)
                    .ToList();

                if (processing.Count == 0)
                    continue;

                _logger.Info($"Pausing {processing.Count} {workflowType} workflow(s)");

                foreach (var workflow in processing)
                {
                    try
                    {
                        // Set to Pending so it can be resumed later via UI
                        workflow.Status = WorkflowStatus.Pending;
                        await _workflowRepository.UpdateAsync(workflow);

                        _logger.Verbose($"Paused workflow {workflow.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to pause workflow {workflow.Id}: {ex.Message}", "WorkflowResumeService", ex);
                    }
                }
            }

            _logger.Info("All workflows paused");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to pause workflows: {ex.Message}", "WorkflowResumeService", ex);
        }
    }
}
