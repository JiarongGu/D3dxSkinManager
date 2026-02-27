using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Processors;
using D3dxSkinManager.Modules.TaskQueue.Repositories;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = D3dxSkinManager.Modules.TaskQueue.Models.TaskStatus;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

/// <summary>
/// Service for managing task queue operations
/// </summary>
public interface ITaskQueueService
{
    /// <summary>
    /// Add a task to the queue
    /// </summary>
    /// <typeparam name="TInput">Task input type</typeparam>
    /// <param name="taskType">Task type identifier</param>
    /// <param name="input">Task input data</param>
    /// <param name="profileId">Profile context (optional)</param>
    /// <param name="chainId">Chain ID if part of a chain (optional)</param>
    /// <param name="nodeId">Node ID within the chain (optional)</param>
    /// <returns>Task ID</returns>
    Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null, string? chainId = null, string? nodeId = null);

    /// <summary>
    /// Start processing the next pending task
    /// </summary>
    Task ProcessNextTaskAsync();

    /// <summary>
    /// Cancel a running task
    /// </summary>
    /// <param name="taskId">Task ID to cancel</param>
    Task CancelTaskAsync(string taskId);

    /// <summary>
    /// Remove a task from queue
    /// </summary>
    /// <param name="taskId">Task ID to remove</param>
    Task RemoveTaskAsync(string taskId);

    /// <summary>
    /// Get all tasks
    /// </summary>
    /// <returns>List of all tasks</returns>
    Task<List<TaskInfo>> GetAllTasksAsync();

    /// <summary>
    /// Get task by ID
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <returns>Task info or null if not found</returns>
    Task<TaskInfo?> GetTaskAsync(string taskId);

    /// <summary>
    /// Clear completed and failed tasks
    /// </summary>
    Task ClearCompletedTasksAsync();

    /// <summary>
    /// Continue a paused chain after user provides input
    /// </summary>
    /// <param name="request">Continue chain request with user input</param>
    /// <returns>ID of the next task created in the chain</returns>
    Task<string> ContinueChainAsync(ContinueChainRequest request);
}

/// <summary>
/// Service for managing and processing task queue
/// Thread-safe implementation with single-threaded processing
/// </summary>
public class TaskQueueService : ITaskQueueService
{
    private readonly ConcurrentDictionary<string, TaskInfo> _tasks;
    private readonly SemaphoreSlim _processorLock;
    private readonly IEventEmitter _eventEmitter;
    private readonly ILogHelper _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITaskChainRepository _chainRepository;
    private readonly ITaskInfoRepository _taskRepository;
    private readonly IRoutingConditionEvaluator _routingEvaluator;
    private CancellationTokenSource? _currentTaskCts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public TaskQueueService(
        IEventEmitter eventEmitter,
        ILogHelper logger,
        IServiceProvider serviceProvider,
        ITaskChainRepository chainRepository,
        ITaskInfoRepository taskRepository,
        IRoutingConditionEvaluator routingEvaluator)
    {
        _tasks = new ConcurrentDictionary<string, TaskInfo>();
        _processorLock = new SemaphoreSlim(1, 1);
        _eventEmitter = eventEmitter;
        _logger = logger;
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _chainRepository = chainRepository ?? throw new ArgumentNullException(nameof(chainRepository));
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _routingEvaluator = routingEvaluator ?? throw new ArgumentNullException(nameof(routingEvaluator));
    }

    public async Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null, string? chainId = null, string? nodeId = null)
    {
        var taskId = $"TASK-{Guid.NewGuid():N}";

        var serializedInput = JsonSerializer.Serialize(input, JsonOptions);
        _logger.Debug($"[AddTask] Received input object: {JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = true })}", "TaskQueueService");
        _logger.Debug($"[AddTask] Serialized Input: {serializedInput}", "TaskQueueService");

        // Create task with or without chain association
        var task = new TaskInfo
        {
            Id = taskId,
            Type = taskType,
            TaskChainId = chainId ?? $"CHAIN-{Guid.NewGuid():N}", // Create standalone chain if not provided
            NodeId = nodeId,
            Status = Models.TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Input = serializedInput
        };

        if (!_tasks.TryAdd(taskId, task))
        {
            throw new InvalidOperationException($"Failed to add task {taskId}");
        }

        _logger.Info($"Task added: {taskId} (Type: {taskType}, Chain: {task.TaskChainId}, Node: {nodeId})", "TaskQueueService");

        // Emit TASK_ADDED event
        await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.ADDED, task).ConfigureAwait(false);

        return taskId;
    }

    public async Task ProcessNextTaskAsync()
    {
        // Try to acquire lock (non-blocking)
        if (!await _processorLock.WaitAsync(0).ConfigureAwait(false))
        {
            _logger.Debug("Processor already running", "TaskQueueService");
            return;
        }

        try
        {
            // Find next pending task
            var nextTask = _tasks.Values
                .Where(t => t.Status == Models.TaskStatus.Pending)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefault();

            if (nextTask == null)
            {
                _logger.Debug("No pending tasks", "TaskQueueService");
                return;
            }

            await ProcessTaskAsync(nextTask).ConfigureAwait(false);
        }
        finally
        {
            _processorLock.Release();
        }
    }

    private async Task ProcessTaskAsync(TaskInfo task)
    {
        _currentTaskCts = new CancellationTokenSource();

        try
        {
            // Update task status
            task.Status = Models.TaskStatus.Processing;
            task.StartedAt = DateTime.UtcNow;
            // OperationId removed (duplicated with TaskChainId)

            _logger.Info($"Processing task: {task.Id}", "TaskQueueService");

            // Emit TASK_STARTED event
            await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.STARTED, task).ConfigureAwait(false);

            // Create progress reporter that emits events
            var progressReporter = new EventProgressReporter(
                task.Id,
                _eventEmitter,
                _logger,
                (progress, message) =>
                {
                    // Progress and Message are runtime-only, not stored in DB
                    // These would be emitted as events for real-time updates
                }
            );

            // Process task using direct processor execution
            _logger.Debug($"[ProcessNext] Processing task type '{task.Type}'", "TaskQueueService");
            object? output = await ProcessTaskAsync(
                task.Type,
                task.Input,
                progressReporter,
                _currentTaskCts.Token
            ).ConfigureAwait(false);

            // Store output data
            task.CompletedAt = DateTime.UtcNow;
            // Progress is runtime-only, not stored
            task.Output = output != null ? JsonSerializer.Serialize(output, JsonOptions) : null;

            // TODO: Get chain context from TaskChainInfo repository
            _logger.Info($"Task completed: {task.Id}", "TaskQueueService");

            // TODO: Handle chain continuation properly with TaskChainInfo
            // This entire section needs to be refactored to work with TaskChainInfo/TaskInfo relationship
            /*
            if (chainContext != null)
            {
                if (chainContext.RequiresUserAction)
                {
                    // Task completed but requires user action before continuing
                    task.Status = Models.TaskStatus.AwaitingConfirmation;
                    _logger.Info($"Chain paused - awaiting user action: {chainContext.UserActionDescription}", "TaskQueueService");

                    // Emit AWAITING_CONFIRMATION event for frontend to show modal
                    await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.AWAITING_CONFIRMATION, task).ConfigureAwait(false);
                }
                else if (!string.IsNullOrEmpty(chainContext.NextTaskType))
                {
                    // Mark as completed and auto-continue to next phase
                    task.Status = Models.TaskStatus.Completed;
                    await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, task).ConfigureAwait(false);

                    await CreateNextChainTaskAsync(task, chainContext, output).ConfigureAwait(false);
                }
                else
                {
                    // Final task in chain - mark completed
                    task.Status = Models.TaskStatus.Completed;
                    _logger.Info($"Chain completed: {task.CorrelationId}", "TaskQueueService");
                    await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, task).ConfigureAwait(false);
                }
            }
            else
            */
            {
                // Standalone task (no chain) - mark completed
                task.Status = Models.TaskStatus.Completed;
                await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, task).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            task.Status = Models.TaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;

            _logger.Warn($"Task cancelled: {task.Id}", "TaskQueueService");

            await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.CANCELLED, task).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            task.Status = Models.TaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.ErrorMessage = ex.Message;

            _logger.Error($"Task failed: {task.Id} - {ex.Message}", "TaskQueueService", ex);

            await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.FAILED, task).ConfigureAwait(false);
        }
        finally
        {
            _currentTaskCts?.Dispose();
            _currentTaskCts = null;
        }
    }


    /// <summary>
    /// Create the next task in a chain automatically
    /// </summary>
    /* TODO: Refactor to work with TaskChainInfo
    private async Task CreateNextChainTaskAsync(TaskInfo completedTask, TaskChainContext chainContext, dynamic? output)
    {
        _logger.Info($"Auto-creating next chain task: {chainContext.NextTaskType} (Phase {chainContext.CurrentPhase + 1})", "TaskQueueService");

        // Update shared data with output from previous phase
        if (chainContext.SharedData == null)
        {
            chainContext.SharedData = new Dictionary<string, object>();
        }

        // Store previous phase output
        chainContext.SharedData[$"phase{chainContext.CurrentPhase}_output"] = completedTask.Output ?? string.Empty;

        // Create next task with updated chain context
        var nextChainContext = new TaskChainContext
        {
            CorrelationId = chainContext.CorrelationId,
            CurrentPhase = chainContext.CurrentPhase + 1,
            TotalPhases = chainContext.TotalPhases,
            SharedData = chainContext.SharedData
            // NextTaskType and RequiresUserAction will be set by the task creator
        };

        // The input for the next task should be derived from the previous task's output
        // This will be handled by the specific task type creation logic
        _logger.Info($"Next task in chain will be created: {chainContext.NextTaskType} with correlation: {chainContext.CorrelationId}", "TaskQueueService");

        // Note: Actual task creation will be handled by frontend/facade based on NextTaskType
        // This is just logging for now - full implementation needs task-specific logic
    }
    */

    /// <summary>
    /// Continue a paused chain after user provides input
    /// </summary>
    public async Task<string> ContinueChainAsync(ContinueChainRequest request)
    {
        // Find the paused task
        if (!_tasks.TryGetValue(request.PausedTaskId, out var pausedTask))
        {
            throw new KeyNotFoundException($"Paused task not found: {request.PausedTaskId}");
        }

        if (pausedTask.Status != TaskStatus.AwaitingConfirmation)
        {
            throw new InvalidOperationException($"Task {request.PausedTaskId} is not awaiting confirmation");
        }

        // Get chain from repository
        var chain = await _chainRepository.GetByIdAsync(pausedTask.TaskChainId).ConfigureAwait(false);
        if (chain == null)
        {
            throw new InvalidOperationException($"Chain not found: {pausedTask.TaskChainId}");
        }

        _logger.Info($"Continuing chain {chain.Id} from paused task {request.PausedTaskId}", "TaskQueueService");

        // Parse chain configuration
        TaskChainConfiguration? config = null;
        if (!string.IsNullOrEmpty(chain.ChainConfiguration))
        {
            config = JsonSerializer.Deserialize<TaskChainConfiguration>(chain.ChainConfiguration, JsonOptions);
        }

        if (config == null)
        {
            throw new InvalidOperationException("No chain configuration found");
        }

        // Get the current node
        TaskChainNode? currentNode = null;
        if (!string.IsNullOrEmpty(pausedTask.NodeId) && config.Nodes.ContainsKey(pausedTask.NodeId))
        {
            currentNode = config.Nodes[pausedTask.NodeId];
        }

        if (currentNode == null)
        {
            throw new InvalidOperationException($"Current node not found: {pausedTask.NodeId}");
        }

        // Parse shared data first for routing evaluation
        var sharedData = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(chain.Context))
        {
            sharedData = JsonSerializer.Deserialize<Dictionary<string, object>>(chain.Context, JsonOptions) ?? new Dictionary<string, object>();
        }

        // Evaluate routing rules to determine next node
        // For now, use simple logic - in production, inject IRoutingConditionEvaluator via DI
        string? nextNodeId = null;

        // If there are routing rules, we would evaluate them here
        if (currentNode.RoutingRules?.Any() == true)
        {
            // TODO: Inject IRoutingConditionEvaluator and use it
            // var evaluator = _routingEvaluator;
            // nextNodeId = evaluator.EvaluateRoutingRules(currentNode, pausedTask, sharedData);

            // For now, just use the default
            nextNodeId = currentNode.DefaultNextNode;
        }
        else
        {
            // No routing rules, use default
            nextNodeId = currentNode.DefaultNextNode;
        }

        if (string.IsNullOrEmpty(nextNodeId) || !config.Nodes.TryGetValue(nextNodeId, out var nextNode))
        {
            // Chain completed - no more nodes to execute
            _logger.Info($"Chain {chain.Id} completed - no more nodes to execute", "TaskQueueService");

            // Update chain status to completed
            chain.Status = TaskChainStatus.Completed;
            chain.CompletedAt = DateTime.UtcNow;
            await _chainRepository.UpdateAsync(chain).ConfigureAwait(false);

            // Update the paused task as the final task
            pausedTask.Status = TaskStatus.Completed;
            pausedTask.CompletedAt = DateTime.UtcNow;
            await _taskRepository.UpdateAsync(pausedTask).ConfigureAwait(false);
            await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, pausedTask).ConfigureAwait(false);

            return pausedTask.Id; // Return the last task ID
        }

        var nextTaskType = nextNode.TaskType;

        // Merge user input into shared data
        if (request.UserInput != null)
        {
            foreach (var kvp in request.UserInput)
            {
                // Store user input with "user_" prefix to distinguish from other data
                sharedData[$"user_{kvp.Key}"] = kvp.Value;
            }

            // Update chain context in repository
            chain.Context = JsonSerializer.Serialize(sharedData, JsonOptions);
            await _chainRepository.UpdateAsync(chain).ConfigureAwait(false);
        }

        // Update paused task to completed now that user has confirmed
        pausedTask.Status = TaskStatus.Completed;
        pausedTask.CompletedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(pausedTask).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, pausedTask).ConfigureAwait(false);

        _logger.Info($"Creating next task in chain: {nextTaskType} (Node: {nextNode.NodeId})", "TaskQueueService");

        // Build input for next task based on node's input mapping
        object nextTaskInput = BuildNextTaskInput(nextTaskType, pausedTask, sharedData, request.UserInput);

        // Create the next task
        var nextTaskId = await AddTaskAsync(nextTaskType, nextTaskInput, null, chain.Id, nextNode.NodeId).ConfigureAwait(false);

        _logger.Info($"Created next task in chain: {nextTaskId} (type: {nextTaskType})", "TaskQueueService");

        // Automatically start processing if not already running
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessNextTaskAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to auto-start processing after chain continuation: {ex.Message}", "TaskQueueService");
            }
        });

        return nextTaskId;
    }

    /// <summary>
    /// Build input for the next task in a chain based on previous output and user input
    /// </summary>
    private object BuildNextTaskInput(string nextTaskType, TaskInfo previousTask, Dictionary<string, object> sharedData, Dictionary<string, object>? userInput)
    {
        _logger.Debug($"Building input for next task type: {nextTaskType}", "TaskQueueService");

        // Parse previous task output if available
        object? previousOutput = null;
        if (!string.IsNullOrEmpty(previousTask.Output))
        {
            try
            {
                // Try to deserialize as generic object
                previousOutput = JsonSerializer.Deserialize<Dictionary<string, object>>(previousTask.Output, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to deserialize previous output: {ex.Message}", "TaskQueueService");
            }
        }

        // Map input based on specific task type transitions
        // This is a simplified version - ideally would use TaskChainNode.InputMapping
        return nextTaskType switch
        {
            "import_from_temp" => BuildImportFromTempInput(previousOutput, sharedData, userInput),
            _ => throw new NotSupportedException($"No input mapping defined for task type: {nextTaskType}")
        };
    }

    /// <summary>
    /// Build input for import_from_temp task (Phase 2 of folder import)
    /// </summary>
    private ImportFromTempTaskInput BuildImportFromTempInput(object? previousOutput, Dictionary<string, object>? sharedData, Dictionary<string, object>? userInput)
    {
        // Previous output should be CompressFolderTaskOutput
        var compressOutput = previousOutput as CompressFolderTaskOutput;
        if (compressOutput == null)
        {
            throw new InvalidOperationException("Previous task output is not CompressFolderTaskOutput");
        }

        // Build input combining compress output and user metadata
        return new ImportFromTempTaskInput
        {
            TempArchivePath = compressOutput.TempArchivePath,
            Name = userInput?.GetValueOrDefault("name")?.ToString() ??
                   sharedData?.GetValueOrDefault("metadata_name")?.ToString() ??
                   compressOutput.FolderName,
            Author = userInput?.GetValueOrDefault("author")?.ToString() ??
                     sharedData?.GetValueOrDefault("metadata_author")?.ToString(),
            Description = userInput?.GetValueOrDefault("description")?.ToString() ??
                          sharedData?.GetValueOrDefault("metadata_description")?.ToString(),
            Grading = userInput?.GetValueOrDefault("grading")?.ToString() ??
                      sharedData?.GetValueOrDefault("metadata_grading")?.ToString() ?? "G",
            Category = userInput?.GetValueOrDefault("category")?.ToString() ??
                       sharedData?.GetValueOrDefault("metadata_category")?.ToString(),
            Tags = ParseTags(userInput?.GetValueOrDefault("tags") ?? sharedData?.GetValueOrDefault("metadata_tags"))
        };
    }

    /// <summary>
    /// Parse tags from various input formats
    /// </summary>
    private List<string>? ParseTags(object? tagsInput)
    {
        if (tagsInput == null) return null;

        return tagsInput switch
        {
            List<string> list => list,
            string[] array => array.ToList(),
            string str => str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => null
        };
    }

    public async Task CancelTaskAsync(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new KeyNotFoundException($"Task not found: {taskId}");
        }

        if (task.Status == TaskStatus.Processing)
        {
            _currentTaskCts?.Cancel();
            _logger.Info($"Cancelling task: {taskId}", "TaskQueueService");
        }

        await Task.CompletedTask;
    }

    public async Task RemoveTaskAsync(string taskId)
    {
        if (_tasks.TryRemove(taskId, out var task))
        {
            _logger.Info($"Task removed: {taskId}", "TaskQueueService");
            await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.REMOVED, task).ConfigureAwait(false);
        }
    }

    public Task<List<TaskInfo>> GetAllTasksAsync()
    {
        var taskList = _tasks.Values.OrderBy(t => t.CreatedAt).ToList();
        return Task.FromResult(taskList);
    }

    public Task<TaskInfo?> GetTaskAsync(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public async Task ClearCompletedTasksAsync()
    {
        var completedTasks = _tasks.Values
            .Where(t => t.Status == Models.TaskStatus.Completed || t.Status == Models.TaskStatus.Failed)
            .ToList();

        foreach (var task in completedTasks)
        {
            await RemoveTaskAsync(task.Id).ConfigureAwait(false);
        }

        _logger.Info($"Cleared {completedTasks.Count} completed tasks", "TaskQueueService");
    }

    /// <summary>
    /// Process a task based on its type
    /// </summary>
    private async Task<object?> ProcessTaskAsync(
        string taskType,
        string? inputJson,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        // Get the appropriate processor based on task type
        switch (taskType)
        {
            case TaskNames.MOD_IMPORT:
                {
                    var processor = _serviceProvider.GetService<ModImportTaskProcessor>();
                    if (processor == null)
                        throw new InvalidOperationException($"Processor not found for task type: {taskType}");

                    var input = string.IsNullOrEmpty(inputJson)
                        ? new ModImportTaskInput()
                        : JsonSerializer.Deserialize<ModImportTaskInput>(inputJson, JsonOptions);

                    if (input == null)
                        throw new InvalidOperationException($"Failed to deserialize input for task type: {taskType}");

                    return await processor.ProcessAsync(input, progressReporter, cancellationToken).ConfigureAwait(false);
                }

            case TaskNames.COMPRESS_FOLDER:
                {
                    var processor = _serviceProvider.GetService<CompressFolderTaskProcessor>();
                    if (processor == null)
                        throw new InvalidOperationException($"Processor not found for task type: {taskType}");

                    var input = string.IsNullOrEmpty(inputJson)
                        ? new CompressFolderTaskInput()
                        : JsonSerializer.Deserialize<CompressFolderTaskInput>(inputJson, JsonOptions);

                    if (input == null)
                        throw new InvalidOperationException($"Failed to deserialize input for task type: {taskType}");

                    return await processor.ProcessAsync(input, progressReporter, cancellationToken).ConfigureAwait(false);
                }

            case TaskNames.IMPORT_FROM_TEMP:
                {
                    var processor = _serviceProvider.GetService<ImportFromTempTaskProcessor>();
                    if (processor == null)
                        throw new InvalidOperationException($"Processor not found for task type: {taskType}");

                    var input = string.IsNullOrEmpty(inputJson)
                        ? new ImportFromTempTaskInput()
                        : JsonSerializer.Deserialize<ImportFromTempTaskInput>(inputJson, JsonOptions);

                    if (input == null)
                        throw new InvalidOperationException($"Failed to deserialize input for task type: {taskType}");

                    return await processor.ProcessAsync(input, progressReporter, cancellationToken).ConfigureAwait(false);
                }

            default:
                throw new NotSupportedException($"Task type '{taskType}' is not supported");
        }
    }
}
