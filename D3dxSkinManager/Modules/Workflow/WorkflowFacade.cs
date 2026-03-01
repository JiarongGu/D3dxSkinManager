using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Handlers;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;

namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Facade interface for Workflow module
/// </summary>
public interface IWorkflowFacade : IModuleFacade { }

/// <summary>
/// Facade for Workflow module IPC operations
/// </summary>
public class WorkflowFacade : IWorkflowFacade
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ModImportWorkflowHandler _modImportHandler;
    private readonly ILogHelper _logger;

    public WorkflowFacade(
        IWorkflowRepository workflowRepository,
        ModImportWorkflowHandler modImportHandler,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _modImportHandler = modImportHandler;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleMessageAsync(IpcRequest request)
    {
        try
        {
            object? responseData = request.Type switch
            {
                // Generic workflow operations
                "GET_WORKFLOW" => await GetWorkflowAsync(request),
                "GET_WORKFLOWS_BY_TYPE" => await GetWorkflowsByTypeAsync(request),
                "DELETE_WORKFLOW" => await DeleteWorkflowAsync(request),
                "UPDATE_WORKFLOW_CONTEXT" => await UpdateWorkflowContextAsync(request),

                // MOD_IMPORT specific operations
                "START_MOD_IMPORT" => await StartModImportAsync(request),
                "CONTINUE_WORKFLOW" => await ContinueWorkflowAsync(request),
                "CANCEL_MOD_IMPORT" => await CancelModImportAsync(request),

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

    private async Task<Models.WorkflowInfo> StartModImportAsync(IpcRequest request)
    {
        var folderPath = request.Payload?.ToString();
        if (string.IsNullOrEmpty(folderPath))
            throw new ArgumentException("Folder path is required");

        return await _modImportHandler.StartImportAsync(folderPath);
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

        // Deserialize current context
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);
        if (context == null)
            throw new InvalidOperationException("Invalid workflow context");

        // Update context fields from partial update
        if (data.Context.TryGetValue("Name", out var name))
            context.Name = name?.ToString();
        if (data.Context.TryGetValue("Author", out var author))
            context.Author = author?.ToString();
        if (data.Context.TryGetValue("Description", out var description))
            context.Description = description?.ToString();
        if (data.Context.TryGetValue("Category", out var category))
            context.Category = category?.ToString();
        if (data.Context.TryGetValue("Tags", out var tags))
            context.Tags = JsonHelper.Deserialize<List<string>>(tags?.ToString() ?? "[]") ?? new();
        if (data.Context.TryGetValue("Grading", out var grading))
            context.Grading = grading?.ToString() ?? "G";

        // Serialize and save
        workflow.Context = JsonHelper.Serialize(context);
        await _workflowRepository.UpdateAsync(workflow);

        return workflow;
    }

    private async Task<Models.WorkflowInfo> ContinueWorkflowAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        return await _modImportHandler.ContinueAsync(workflowId);
    }

    private async Task<Models.WorkflowInfo> CancelModImportAsync(IpcRequest request)
    {
        var workflowId = request.Payload?.ToString();
        if (string.IsNullOrEmpty(workflowId))
            throw new ArgumentException("Workflow ID is required");

        return await _modImportHandler.CancelAsync(workflowId);
    }

    private class UpdateContextRequest
    {
        public required string WorkflowId { get; set; }
        public required Dictionary<string, object?> Context { get; set; }
    }
}
