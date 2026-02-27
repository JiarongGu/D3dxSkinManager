using System.Text.Json;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.TaskQueue.Configuration;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Repositories;
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
    private readonly ITaskChainRepository _chainRepository;
    protected override string ModuleName => "TASK_QUEUE";

    public TaskQueueFacade(
        ITaskQueueService taskQueueService,
        IPayloadHelper payloadHelper,
        ITaskChainRepository chainRepository,
        ILogHelper logger)
        : base(logger)
    {
        _taskQueueService = taskQueueService;
        _payloadHelper = payloadHelper;
        _chainRepository = chainRepository;
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

            // New metadata query endpoints
            "GET_TASK_METADATA" => GetTaskMetadata(request),
            "GET_ALL_TASK_METADATA" => GetAllTaskMetadata(),
            "GET_CHAIN_CONFIG" => await GetChainConfig(request).ConfigureAwait(false),
            "GET_ALL_CHAINS" => await GetAllChains().ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    private async Task<string> AddTaskAsync(IpcRequest request)
    {
        _logger.Debug($"[AddTaskAsync] Full request payload: {JsonSerializer.Serialize(request.Payload)}", "TaskQueueFacade");

        var taskType = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskType");
        var profileId = _payloadHelper.GetOptionalValue<string>(request.Payload, "profileId");

        _logger.Debug($"[AddTaskAsync] taskType: {taskType}, profileId: {profileId}", "TaskQueueFacade");

        // Check if task type is valid
        if (!IsValidTaskType(taskType))
        {
            throw new NotSupportedException($"Task type not supported: {taskType}");
        }

        // Special handling for mod_import with folder (convert to chain)
        if (taskType == TaskTypes.MOD_IMPORT)
        {
            var input = _payloadHelper.GetRequiredValue<ModImportTaskInput>(request.Payload, "input");
            if (input.IsFolder)
            {
                _logger.Info($"Folder import detected - using chain-phase approach", "TaskQueueFacade");
                return await CreateFolderImportChain(input, profileId).ConfigureAwait(false);
            }
        }

        // For all other tasks, use generic approach
        var taskId = await AddGenericTaskAsync(request, taskType, profileId).ConfigureAwait(false);
        return taskId;
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

    /// <summary>
    /// Add a generic task by deserializing input based on task type
    /// </summary>
    private async Task<string> AddGenericTaskAsync(IpcRequest request, string taskType, string? profileId)
    {
        // Deserialize input based on task type
        var inputJson = _payloadHelper.GetRequiredValue<JsonElement>(request.Payload, "input");
        var inputJsonString = inputJson.GetRawText();

        object? input = taskType switch
        {
            TaskTypes.MOD_IMPORT => JsonSerializer.Deserialize<ModImportTaskInput>(inputJsonString),
            TaskTypes.COMPRESS_FOLDER => JsonSerializer.Deserialize<CompressFolderTaskInput>(inputJsonString),
            TaskTypes.IMPORT_FROM_TEMP => JsonSerializer.Deserialize<ImportFromTempTaskInput>(inputJsonString),
            _ => throw new NotSupportedException($"Task type not supported: {taskType}")
        };

        if (input == null)
        {
            throw new InvalidOperationException($"Failed to deserialize input for task type: {taskType}");
        }

        _logger.Debug($"[AddGenericTaskAsync] Adding task of type '{taskType}'", "TaskQueueFacade");

        // Add task to queue
        return await _taskQueueService.AddTaskAsync(taskType, input, profileId).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a folder import chain (compress_folder -> import_from_temp)
    /// </summary>
    private async Task<string> CreateFolderImportChain(ModImportTaskInput input, string? profileId)
    {
        // Create a chain for folder import workflow
        var chainId = $"CHAIN-{Guid.NewGuid():N}";
        var chain = new TaskChainInfo
        {
            Id = chainId,
            ChainType = ChainTypes.FOLDER_IMPORT,
            Status = TaskChainStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            // Store initial metadata in context as shared data
            Context = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["originalFolderPath"] = input.FilePath,
                ["metadata_name"] = input.Name ?? string.Empty,
                ["metadata_author"] = input.Author ?? string.Empty,
                ["metadata_description"] = input.Description ?? string.Empty,
                ["metadata_grading"] = input.Grading ?? string.Empty,
                ["metadata_category"] = input.Category ?? string.Empty,
                ["metadata_tags"] = input.Tags ?? new List<string>()
            })
        };

        // Add chain to repository
        await _chainRepository.AddAsync(chain).ConfigureAwait(false);

        // Create compress_folder task as first node
        var compressInput = new CompressFolderTaskInput
        {
            FolderPath = input.FilePath
        };

        // For now, use a simple node ID - in future this would come from chain configuration
        var nodeId = "compress_folder_node";

        return await _taskQueueService.AddTaskAsync(TaskTypes.COMPRESS_FOLDER, compressInput, profileId, chainId, nodeId).ConfigureAwait(false);
    }

    /// <summary>
    /// Get metadata for a specific task type
    /// </summary>
    private object? GetTaskMetadata(IpcRequest request)
    {
        var taskType = _payloadHelper.GetRequiredValue<string>(request.Payload, "taskType");

        var metadata = GetTaskTypeMetadata(taskType);
        if (metadata == null)
        {
            return new { success = false, error = $"Task type not found: {taskType}" };
        }

        return new
        {
            success = true,
            metadata
        };
    }

    /// <summary>
    /// Get metadata for all registered task types
    /// </summary>
    private object GetAllTaskMetadata()
    {
        var allMetadata = new[]
        {
            GetTaskTypeMetadata(TaskTypes.MOD_IMPORT),
            GetTaskTypeMetadata(TaskTypes.COMPRESS_FOLDER),
            GetTaskTypeMetadata(TaskTypes.IMPORT_FROM_TEMP)
        }.Where(m => m != null);

        return new { success = true, metadata = allMetadata };
    }

    /// <summary>
    /// Get metadata for a specific task type
    /// </summary>
    private object? GetTaskTypeMetadata(string taskType)
    {
        return taskType switch
        {
            TaskTypes.MOD_IMPORT => new
            {
                taskType = TaskTypes.MOD_IMPORT,
                displayName = "Import Mod",
                description = "Import a mod from file or folder",
                estimatedDurationSeconds = 30,
                supportsCancellation = true,
                supportsChaining = true
            },
            TaskTypes.COMPRESS_FOLDER => new
            {
                taskType = TaskTypes.COMPRESS_FOLDER,
                displayName = "Compress Folder",
                description = "Compress a folder to temporary archive",
                estimatedDurationSeconds = 60,
                supportsCancellation = true,
                supportsChaining = true
            },
            TaskTypes.IMPORT_FROM_TEMP => new
            {
                taskType = TaskTypes.IMPORT_FROM_TEMP,
                displayName = "Import from Temp",
                description = "Import mod from temporary archive with metadata",
                estimatedDurationSeconds = 30,
                supportsCancellation = true,
                supportsChaining = true
            },
            _ => null
        };
    }

    /// <summary>
    /// Get configuration for a specific chain
    /// </summary>
    private async Task<object?> GetChainConfig(IpcRequest request)
    {
        var chainId = _payloadHelper.GetRequiredValue<string>(request.Payload, "chainId");
        var chain = await _chainRepository.GetByIdAsync(chainId).ConfigureAwait(false);

        if (chain == null)
        {
            return new { success = false, error = $"Chain not found: {chainId}" };
        }

        // Deserialize configuration from JSON
        TaskChainConfiguration? config = null;
        if (!string.IsNullOrEmpty(chain.ChainConfiguration))
        {
            config = JsonSerializer.Deserialize<TaskChainConfiguration>(chain.ChainConfiguration);
        }

        if (config == null)
        {
            return new { success = false, error = "Invalid chain configuration" };
        }

        return new
        {
            success = true,
            config = new
            {
                chainId = chain.Id,
                chainType = chain.ChainType,
                startNodeId = config.StartNodeId,
                nodes = config.Nodes.Select(kvp => new
                {
                    nodeId = kvp.Value.NodeId,
                    taskType = kvp.Value.TaskType,
                    inputMapping = kvp.Value.InputMapping,
                    outputMapping = kvp.Value.OutputMapping,
                    routingRules = kvp.Value.RoutingRules?.Select(r => new
                    {
                        name = r.Name,
                        priority = r.Priority,
                        nextNodeId = r.NextNodeId,
                        condition = new
                        {
                            type = r.Condition.Type.ToString(),
                            field = r.Condition.Field,
                            op = r.Condition.Operator.ToString(),
                            value = r.Condition.Value
                        }
                    }),
                    defaultNextNode = kvp.Value.DefaultNextNode,
                    metadata = kvp.Value.Metadata
                })
            }
        };
    }

    /// <summary>
    /// Get all registered chain configurations
    /// </summary>
    private async Task<object> GetAllChains()
    {
        // For now, return empty list since chains are created dynamically
        // In the future, we might want to return predefined chain templates
        var allChains = PredefinedTaskChains.GetAllChains().Select(kvp => new
        {
            chainType = kvp.Key,
            displayName = GetChainDisplayName(kvp.Key),
            description = GetChainDescription(kvp.Key),
            tags = GetChainTags(kvp.Key)
        });

        return new { success = true, chains = allChains };
    }

    /// <summary>
    /// Check if task type is valid
    /// </summary>
    private bool IsValidTaskType(string taskType)
    {
        return taskType switch
        {
            TaskTypes.MOD_IMPORT => true,
            TaskTypes.COMPRESS_FOLDER => true,
            TaskTypes.IMPORT_FROM_TEMP => true,
            _ => false
        };
    }

    private string GetChainDisplayName(string chainType)
    {
        return chainType switch
        {
            ChainTypes.FOLDER_IMPORT => "Folder Import",
            ChainTypes.QUICK_FOLDER_IMPORT => "Quick Folder Import",
            ChainTypes.VALIDATED_IMPORT => "Validated Import",
            ChainTypes.BATCH_PROCESSING => "Batch Processing",
            _ => chainType
        };
    }

    private string GetChainDescription(string chainType)
    {
        return chainType switch
        {
            ChainTypes.FOLDER_IMPORT => "Import a folder as a mod with user metadata input",
            ChainTypes.QUICK_FOLDER_IMPORT => "Quick import with default metadata",
            ChainTypes.VALIDATED_IMPORT => "Import with validation step",
            ChainTypes.BATCH_PROCESSING => "Process multiple items in batch",
            _ => ""
        };
    }

    private List<string> GetChainTags(string chainType)
    {
        return chainType switch
        {
            ChainTypes.FOLDER_IMPORT => new List<string> { "import", "folder", "interactive" },
            ChainTypes.QUICK_FOLDER_IMPORT => new List<string> { "import", "folder", "quick" },
            ChainTypes.VALIDATED_IMPORT => new List<string> { "import", "validation" },
            ChainTypes.BATCH_PROCESSING => new List<string> { "batch", "bulk" },
            _ => new List<string>()
        };
    }
}
