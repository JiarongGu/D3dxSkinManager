using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Repositories;

namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Facade interface for Workflow module
/// </summary>
public interface IWorkflowFacade : IModuleFacade { }

/// <summary>
/// Facade for Workflow module IPC operations
/// Uses a handler registry to route workflow operations to the appropriate handler
/// </summary>
public class WorkflowFacade : IWorkflowFacade
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly Dictionary<string, IWorkflowHandler> _handlers;
    private readonly ILogHelper _logger;

    public WorkflowFacade(
        IWorkflowRepository workflowRepository,
        IEnumerable<IWorkflowHandler> handlers,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _logger = logger;

        // Build handler registry indexed by workflow type
        _handlers = handlers.ToDictionary(h => h.WorkflowType, h => h);

        _logger.Info($"Workflow facade initialized with {_handlers.Count} handler(s): {string.Join(", ", _handlers.Keys)}");
    }

    /// <summary>
    /// Get handler for a specific workflow type
    /// </summary>
    private IWorkflowHandler GetHandler(string workflowType)
    {
        if (!_handlers.TryGetValue(workflowType, out var handler))
            throw new InvalidOperationException($"No handler registered for workflow type: {workflowType}");

        return handler;
    }

    public async Task<IpcResponse> HandleMessageAsync(IpcRequest request)
    {
        try
        {
            object? responseData = request.Type switch
            {
                // Generic workflow operations
                "CREATE_WORKFLOW" => await CreateWorkflowAsync(request),
                "GET_WORKFLOW" => await GetWorkflowAsync(request),
                "GET_WORKFLOWS_BY_TYPE" => await GetWorkflowsByTypeAsync(request),
                "DELETE_WORKFLOW" => await DeleteWorkflowAsync(request),
                "UPDATE_WORKFLOW_CONTEXT" => await UpdateWorkflowContextAsync(request),
                "PAUSE_WORKFLOW" => await PauseWorkflowAsync(request),
                "RESUME_WORKFLOW" => await ResumeWorkflowAsync(request),

                // Batch operations
                "BATCH_DELETE_WORKFLOWS" => await BatchDeleteWorkflowsAsync(request),
                "BATCH_RESUME_WORKFLOWS" => await BatchResumeWorkflowsAsync(request),

                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return IpcResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            _logger.Error($"Workflow message handling failed: {ex.Message}", "WorkflowFacade", ex);
            return IpcResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<Models.WorkflowInfo?> GetWorkflowAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        return await _workflowRepository.GetByIdAsync(workflowId);
    }

    private async Task<List<Models.WorkflowInfo>> GetWorkflowsByTypeAsync(IpcRequest request)
    {
        var type = request.Payload?.ToString();
        if (string.IsNullOrEmpty(type))
            throw new ArgumentException("Workflow type is required");

        var workflows = await _workflowRepository.GetByTypeAsync(type);

        // Populate category names for MOD_IMPORT workflows (batch operation to avoid N+1 queries)
        if (type == "MOD_IMPORT" && workflows.Any())
        {
            var handler = GetHandler(type);
            if (handler is Handlers.ModImportWorkflowHandler modImportHandler)
            {
                await modImportHandler.PopulateCategoryNamesInContextsBulkAsync(workflows);
            }
        }

        return workflows;
    }

    private async Task<bool> DeleteWorkflowAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        await _workflowRepository.DeleteAsync(workflowId);
        return true;
    }

    private async Task<Models.WorkflowInfo> CreateWorkflowAsync(IpcRequest request)
    {
        var json = request.Payload?.ToString();
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("Workflow creation data is required");

        var data = JsonHelper.Deserialize<CreateWorkflowRequest>(json);
        if (data == null)
            throw new ArgumentException("Invalid workflow creation format");

        // Route to appropriate handler based on workflow type
        var handler = GetHandler(data.Type);
        return await handler.StartAsync(data.InitialData);
    }

    private async Task<Models.WorkflowInfo> UpdateWorkflowContextAsync(IpcRequest request)
    {
        var json = request.Payload?.ToString();
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("Update context data is required");

        var data = JsonHelper.Deserialize<UpdateContextRequest>(json);
        if (data == null)
            throw new ArgumentException("Invalid context format");

        var workflow = await _workflowRepository.GetByIdAsync(data.WorkflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow {data.WorkflowId} not found");

        // Convert the update dictionary to JSON for the handler
        var updateJson = JsonHelper.Serialize(data.Context);

        // Route to appropriate handler based on workflow type
        var handler = GetHandler(workflow.Type);
        return await handler.UpdateContextAsync(data.WorkflowId, updateJson);
    }

    private async Task<Models.WorkflowInfo> ResumeWorkflowAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // Route to appropriate handler based on workflow type
        var handler = GetHandler(workflow.Type);
        return await handler.ContinueAsync(workflowId);
    }

    private async Task<Models.WorkflowInfo> PauseWorkflowAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // Route to appropriate handler based on workflow type
        var handler = GetHandler(workflow.Type);
        return await handler.PauseAsync(workflowId);
    }

    private async Task<BatchOperationResult> BatchDeleteWorkflowsAsync(IpcRequest request)
    {
        var json = request.Payload?.ToString();
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("Workflow IDs are required");

        var data = JsonHelper.Deserialize<BatchWorkflowRequest>(json);
        if (data == null || data.WorkflowIds == null || data.WorkflowIds.Count == 0)
            throw new ArgumentException("Invalid or empty workflow IDs");

        _logger.Info($"Batch deleting {data.WorkflowIds.Count} workflows");

        var result = new BatchOperationResult
        {
            TotalRequested = data.WorkflowIds.Count,
            Successful = new List<string>(),
            Failed = new List<FailedWorkflow>()
        };

        // Fetch all workflows first
        var workflows = await _workflowRepository.GetByIdsAsync(data.WorkflowIds);
        var workflowDict = workflows.ToDictionary(w => w.Id, w => w);

        // Process each workflow deletion - cleanup temp files via handler
        foreach (var workflowId in data.WorkflowIds)
        {
            try
            {
                if (!workflowDict.TryGetValue(workflowId, out var workflow))
                {
                    result.Failed.Add(new FailedWorkflow
                    {
                        WorkflowId = workflowId,
                        Error = "Workflow not found"
                    });
                    continue;
                }

                // Get handler for cleanup (this will clean temp files)
                var handler = GetHandler(workflow.Type);
                await handler.CancelAsync(workflowId);

                // Delete from database
                await _workflowRepository.DeleteAsync(workflowId);

                result.Successful.Add(workflowId);
                _logger.Info($"Successfully deleted workflow: {workflowId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete workflow {workflowId}: {ex.Message}", "WorkflowFacade", ex);
                result.Failed.Add(new FailedWorkflow
                {
                    WorkflowId = workflowId,
                    Error = ex.Message
                });
            }
        }

        _logger.Info($"Batch delete completed: {result.Successful.Count} successful, {result.Failed.Count} failed");
        return result;
    }

    private async Task<BatchOperationResult> BatchResumeWorkflowsAsync(IpcRequest request)
    {
        var json = request.Payload?.ToString();
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("Workflow IDs are required");

        var data = JsonHelper.Deserialize<BatchWorkflowRequest>(json);
        if (data == null || data.WorkflowIds == null || data.WorkflowIds.Count == 0)
            throw new ArgumentException("Invalid or empty workflow IDs");

        _logger.Info($"Batch resuming {data.WorkflowIds.Count} workflows");

        var result = new BatchOperationResult
        {
            TotalRequested = data.WorkflowIds.Count,
            Successful = new List<string>(),
            Failed = new List<FailedWorkflow>()
        };

        // Fetch all workflows first
        var workflows = await _workflowRepository.GetByIdsAsync(data.WorkflowIds);
        var workflowDict = workflows.ToDictionary(w => w.Id, w => w);

        // Process each workflow resume
        foreach (var workflowId in data.WorkflowIds)
        {
            try
            {
                if (!workflowDict.TryGetValue(workflowId, out var workflow))
                {
                    result.Failed.Add(new FailedWorkflow
                    {
                        WorkflowId = workflowId,
                        Error = "Workflow not found"
                    });
                    continue;
                }

                // Only resume workflows in WaitingForInput status
                if (workflow.Status != Entities.WorkflowStatus.WaitingForInput)
                {
                    result.Failed.Add(new FailedWorkflow
                    {
                        WorkflowId = workflowId,
                        Error = $"Workflow is not waiting for input (status: {workflow.Status})"
                    });
                    continue;
                }

                // Get handler and resume
                var handler = GetHandler(workflow.Type);
                await handler.ContinueAsync(workflowId);

                result.Successful.Add(workflowId);
                _logger.Info($"Successfully resumed workflow: {workflowId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to resume workflow {workflowId}: {ex.Message}", "WorkflowFacade", ex);
                result.Failed.Add(new FailedWorkflow
                {
                    WorkflowId = workflowId,
                    Error = ex.Message
                });
            }
        }

        _logger.Info($"Batch resume completed: {result.Successful.Count} successful, {result.Failed.Count} failed");
        return result;
    }

    private class CreateWorkflowRequest
    {
        public required string Type { get; set; }
        public required string InitialData { get; set; }
    }

    private class UpdateContextRequest
    {
        public required string WorkflowId { get; set; }
        public required Dictionary<string, object?> Context { get; set; }
    }

    private class BatchWorkflowRequest
    {
        public required List<string> WorkflowIds { get; set; }
    }

    private class BatchOperationResult
    {
        public int TotalRequested { get; set; }
        public required List<string> Successful { get; set; }
        public required List<FailedWorkflow> Failed { get; set; }
    }

    private class FailedWorkflow
    {
        public required string WorkflowId { get; set; }
        public required string Error { get; set; }
    }
}
