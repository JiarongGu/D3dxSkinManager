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

        return await _workflowRepository.GetByTypeAsync(type);
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
}
