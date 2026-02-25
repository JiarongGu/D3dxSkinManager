using System.Text.Json;
using D3dxSkinManager.Composition;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Services;

namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Interface for TaskQueue facade
/// </summary>
public interface ITaskQueueFacade : IModuleFacade
{
}

/// <summary>
/// IPC message facade for TaskQueue module
/// Handles communication between frontend and TaskQueue service
/// </summary>
public class TaskQueueFacade : BaseFacade, ITaskQueueFacade
{
    private readonly ITaskQueueService _taskQueueService;
    private readonly IPayloadHelper _payloadHelper;
    protected override string ModuleName => "TASKQUEUE";

    public TaskQueueFacade(
        ITaskQueueService taskQueueService,
        IPayloadHelper payloadHelper,
        ILogHelper logger)
        : base(logger)
    {
        _taskQueueService = taskQueueService;
        _payloadHelper = payloadHelper;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "ADD_TASK" => await AddTaskAsync(request).ConfigureAwait(false),
            "PROCESS_NEXT" => await ProcessNextTaskAsync().ConfigureAwait(false),
            "CANCEL_TASK" => await CancelTaskAsync(request).ConfigureAwait(false),
            "REMOVE_TASK" => await RemoveTaskAsync(request).ConfigureAwait(false),
            "GET_ALL_TASKS" => await GetAllTasksAsync().ConfigureAwait(false),
            "GET_TASK" => await GetTaskAsync(request).ConfigureAwait(false),
            "CLEAR_COMPLETED" => await ClearCompletedTasksAsync().ConfigureAwait(false),
            "CONTINUE_CHAIN" => await ContinueChainAsync(request).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    private async Task<string> AddTaskAsync(IpcRequest request)
    {
        _logger.Debug($"[AddTaskAsync] Full request payload: {JsonSerializer.Serialize(request.Payload)}", "TaskQueueFacade");

        var taskType = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskType");
        var profileId = _payloadHelper.GetOptionalValue<string>(request.Payload, "profileId");

        _logger.Debug($"[AddTaskAsync] taskType: {taskType}, profileId: {profileId}", "TaskQueueFacade");

        // Deserialize based on task type using PayloadHelper (handles camelCase automatically)
        var taskId = taskType switch
        {
            "mod_import" => await AddModImportTaskAsync(request, profileId).ConfigureAwait(false),
            "compress_folder" => await AddCompressFolderTaskAsync(request, profileId).ConfigureAwait(false),
            "import_from_temp" => await AddImportFromTempTaskAsync(request, profileId).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Task type not supported: {taskType}")
        };

        return taskId;
    }

    private async Task<string> AddModImportTaskAsync(IpcRequest request, string? profileId)
    {
        // Use PayloadHelper to deserialize with proper camelCase handling
        _logger.Debug($"[AddModImportTaskAsync] Getting input from payload...", "TaskQueueFacade");
        var input = _payloadHelper.GetRequiredValue<ModImportTaskInput>(request.Payload, "input");
        _logger.Debug($"[AddModImportTaskAsync] Got input - FilePath: '{input.FilePath}', IsFolder: {input.IsFolder}", "TaskQueueFacade");

        // If folder, use chain-phase approach
        if (input.IsFolder)
        {
            _logger.Info($"Folder import detected - using chain-phase approach", "TaskQueueFacade");

            // Create compress_folder task with chain context
            var compressInput = new CompressFolderTaskInput
            {
                FolderPath = input.FilePath,
                ProfileId = profileId
            };

            var chainContext = new TaskChainContext
            {
                CorrelationId = $"CORR-{Guid.NewGuid():N}",
                CurrentPhase = 1,
                TotalPhases = 2,
                RequiresUserAction = true,
                NextTaskType = "import_from_temp",
                UserActionDescription = "Please provide metadata for the mod import",
                SharedData = new Dictionary<string, object>
                {
                    ["originalFolderPath"] = input.FilePath,
                    ["metadata_name"] = input.Name ?? string.Empty,
                    ["metadata_author"] = input.Author ?? string.Empty,
                    ["metadata_description"] = input.Description ?? string.Empty,
                    ["metadata_grading"] = input.Grading ?? string.Empty,
                    ["metadata_category"] = input.Category ?? string.Empty,
                    ["metadata_tags"] = input.Tags ?? new List<string>()
                }
            };

            return await _taskQueueService.AddTaskAsync("compress_folder", compressInput, profileId, chainContext).ConfigureAwait(false);
        }

        // Archive import uses direct single-phase approach
        return await _taskQueueService.AddTaskAsync("mod_import", input, profileId).ConfigureAwait(false);
    }

    private async Task<string> AddCompressFolderTaskAsync(IpcRequest request, string? profileId)
    {
        _logger.Debug($"[AddCompressFolderTaskAsync] Getting input from payload...", "TaskQueueFacade");
        var input = _payloadHelper.GetRequiredValue<CompressFolderTaskInput>(request.Payload, "input");
        _logger.Debug($"[AddCompressFolderTaskAsync] Got input - FolderPath: '{input.FolderPath}'", "TaskQueueFacade");

        // Get optional chain context if provided
        var chainContextJson = _payloadHelper.GetOptionalValue<string>(request.Payload, "chainContext");
        TaskChainContext? chainContext = null;

        if (!string.IsNullOrEmpty(chainContextJson))
        {
            chainContext = JsonSerializer.Deserialize<TaskChainContext>(chainContextJson);
        }

        return await _taskQueueService.AddTaskAsync("compress_folder", input, profileId, chainContext).ConfigureAwait(false);
    }

    private async Task<string> AddImportFromTempTaskAsync(IpcRequest request, string? profileId)
    {
        _logger.Debug($"[AddImportFromTempTaskAsync] Getting input from payload...", "TaskQueueFacade");
        var input = _payloadHelper.GetRequiredValue<ImportFromTempTaskInput>(request.Payload, "input");
        _logger.Debug($"[AddImportFromTempTaskAsync] Got input - TempArchivePath: '{input.TempArchivePath}'", "TaskQueueFacade");

        // Get optional chain context if provided
        var chainContextJson = _payloadHelper.GetOptionalValue<string>(request.Payload, "chainContext");
        TaskChainContext? chainContext = null;

        if (!string.IsNullOrEmpty(chainContextJson))
        {
            chainContext = JsonSerializer.Deserialize<TaskChainContext>(chainContextJson);
        }

        return await _taskQueueService.AddTaskAsync("import_from_temp", input, profileId, chainContext).ConfigureAwait(false);
    }

    private async Task<object> ProcessNextTaskAsync()
    {
        await _taskQueueService.ProcessNextTaskAsync().ConfigureAwait(false);
        return new { success = true };
    }

    private async Task<object> CancelTaskAsync(IpcRequest request)
    {
        var taskId = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskId");
        await _taskQueueService.CancelTaskAsync(taskId).ConfigureAwait(false);
        return new { success = true };
    }

    private async Task<object> RemoveTaskAsync(IpcRequest request)
    {
        var taskId = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskId");
        await _taskQueueService.RemoveTaskAsync(taskId).ConfigureAwait(false);
        return new { success = true };
    }

    private async Task<List<TaskInfo>> GetAllTasksAsync()
    {
        return await _taskQueueService.GetAllTasksAsync().ConfigureAwait(false);
    }

    private async Task<TaskInfo?> GetTaskAsync(IpcRequest request)
    {
        var taskId = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskId");
        return await _taskQueueService.GetTaskAsync(taskId).ConfigureAwait(false);
    }

    private async Task<object> ClearCompletedTasksAsync()
    {
        await _taskQueueService.ClearCompletedTasksAsync().ConfigureAwait(false);
        return new { success = true };
    }

    private async Task<string> ContinueChainAsync(IpcRequest request)
    {
        _logger.Debug($"[ContinueChainAsync] Processing chain continuation request", "TaskQueueFacade");

        var correlationId = _payloadHelper.GetRequiredValue<string>(request.Payload, "correlationId");
        var pausedTaskId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pausedTaskId");
        var userInputJson = _payloadHelper.GetOptionalValue<string>(request.Payload, "userInput");

        Dictionary<string, object>? userInput = null;
        if (!string.IsNullOrEmpty(userInputJson))
        {
            userInput = JsonSerializer.Deserialize<Dictionary<string, object>>(userInputJson);
        }

        var continueRequest = new ContinueChainRequest
        {
            CorrelationId = correlationId,
            PausedTaskId = pausedTaskId,
            UserInput = userInput
        };

        var result = await _taskQueueService.ContinueChainAsync(continueRequest).ConfigureAwait(false);

        _logger.Info($"Chain continuation initiated: {correlationId}", "TaskQueueFacade");

        return result;
    }
}
