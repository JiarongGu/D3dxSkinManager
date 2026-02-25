using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using D3dxSkinManager.Composition;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.TaskQueue.Models;
using TaskStatus = D3dxSkinManager.Modules.TaskQueue.Models.TaskStatus;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

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
        IServiceProvider serviceProvider)
    {
        _tasks = new ConcurrentDictionary<string, TaskInfo>();
        _processorLock = new SemaphoreSlim(1, 1);
        _eventEmitter = eventEmitter;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null, TaskChainContext? chainContext = null)
    {
        var taskId = $"TASK-{Guid.NewGuid():N}";

        // If no correlation ID provided, generate one
        if (chainContext == null)
        {
            chainContext = new TaskChainContext
            {
                CorrelationId = $"CORR-{Guid.NewGuid():N}",
                CurrentPhase = 1,
                TotalPhases = 1
            };
        }

        var serializedInput = JsonSerializer.Serialize(input, JsonOptions);
        _logger.Debug($"[AddTask] Received input object: {JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = true })}", "TaskQueueService");
        _logger.Debug($"[AddTask] Serialized InputData: {serializedInput}", "TaskQueueService");

        var task = new TaskInfo
        {
            Id = taskId,
            Type = taskType,
            Status = Models.TaskStatus.Pending,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            InputData = serializedInput,
            ProfileId = profileId,
            CorrelationId = chainContext.CorrelationId,
            ChainContext = chainContext
        };

        if (!_tasks.TryAdd(taskId, task))
        {
            throw new InvalidOperationException($"Failed to add task {taskId}");
        }

        _logger.Info($"Task added: {taskId} (Type: {taskType}, Correlation: {chainContext.CorrelationId}, Phase: {chainContext.CurrentPhase}/{chainContext.TotalPhases})", "TaskQueueService");

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
            task.OperationId = Guid.NewGuid().ToString();

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
                    task.Progress = progress;
                    task.Message = message;
                }
            );

            // Process task based on type
            dynamic? output = task.Type switch
            {
                "mod_import" => await ProcessModImportTaskAsync(task, progressReporter, _currentTaskCts.Token).ConfigureAwait(false),
                "compress_folder" => await ProcessCompressFolderTaskAsync(task, progressReporter, _currentTaskCts.Token).ConfigureAwait(false),
                "import_from_temp" => await ProcessImportFromTempTaskAsync(task, progressReporter, _currentTaskCts.Token).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Task type not supported: {task.Type}")
            };

            // Store output data
            task.CompletedAt = DateTime.UtcNow;
            task.Progress = 100;
            task.OutputData = output != null ? JsonSerializer.Serialize(output, JsonOptions) : null;

            var chainContext = task.ChainContext;
            _logger.Info($"Task completed: {task.Id} (Phase {chainContext?.CurrentPhase ?? 1}/{chainContext?.TotalPhases ?? 1})", "TaskQueueService");

            // Handle chain continuation
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

    private async Task<ModImportTaskOutput> ProcessModImportTaskAsync(
        TaskInfo task,
        EventProgressReporter progressReporter,
        CancellationToken ct)
    {
        // Get processor from DI container
        var processor = _serviceProvider.GetService(typeof(ModImportTaskProcessor)) as ModImportTaskProcessor;

        if (processor == null)
        {
            throw new InvalidOperationException("ModImportTaskProcessor not registered in DI container");
        }

        // Deserialize input
        _logger.Debug($"[ProcessModImport] Raw InputData: {task.InputData}", "TaskQueueService");
        var input = JsonSerializer.Deserialize<ModImportTaskInput>(task.InputData, JsonOptions);
        if (input == null)
        {
            throw new InvalidOperationException("Failed to deserialize task input");
        }
        _logger.Debug($"[ProcessModImport] Deserialized - FilePath: '{input.FilePath}', IsFolder: {input.IsFolder}", "TaskQueueService");

        // Process task
        return await processor.ProcessAsync(input, progressReporter, ct).ConfigureAwait(false);
    }

    private async Task<CompressFolderTaskOutput> ProcessCompressFolderTaskAsync(
        TaskInfo task,
        EventProgressReporter progressReporter,
        CancellationToken ct)
    {
        // Get processor from DI container
        var processor = _serviceProvider.GetService(typeof(CompressFolderTaskProcessor)) as CompressFolderTaskProcessor;

        if (processor == null)
        {
            throw new InvalidOperationException("CompressFolderTaskProcessor not registered in DI container");
        }

        // Deserialize input
        _logger.Debug($"[ProcessCompressFolder] Raw InputData: {task.InputData}", "TaskQueueService");
        var input = JsonSerializer.Deserialize<CompressFolderTaskInput>(task.InputData, JsonOptions);
        if (input == null)
        {
            throw new InvalidOperationException("Failed to deserialize task input");
        }
        _logger.Debug($"[ProcessCompressFolder] Deserialized - FolderPath: '{input.FolderPath}'", "TaskQueueService");

        // Process task
        return await processor.ProcessAsync(input, progressReporter, ct).ConfigureAwait(false);
    }

    private async Task<ModImportTaskOutput> ProcessImportFromTempTaskAsync(
        TaskInfo task,
        EventProgressReporter progressReporter,
        CancellationToken ct)
    {
        // Get processor from DI container
        var processor = _serviceProvider.GetService(typeof(ImportFromTempTaskProcessor)) as ImportFromTempTaskProcessor;

        if (processor == null)
        {
            throw new InvalidOperationException("ImportFromTempTaskProcessor not registered in DI container");
        }

        // Deserialize input
        _logger.Debug($"[ProcessImportFromTemp] Raw InputData: {task.InputData}", "TaskQueueService");
        var input = JsonSerializer.Deserialize<ImportFromTempTaskInput>(task.InputData, JsonOptions);
        if (input == null)
        {
            throw new InvalidOperationException("Failed to deserialize task input");
        }
        _logger.Debug($"[ProcessImportFromTemp] Deserialized - TempArchivePath: '{input.TempArchivePath}'", "TaskQueueService");

        // Process task
        return await processor.ProcessAsync(input, progressReporter, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Create the next task in a chain automatically
    /// </summary>
    private async Task CreateNextChainTaskAsync(TaskInfo completedTask, TaskChainContext chainContext, dynamic? output)
    {
        _logger.Info($"Auto-creating next chain task: {chainContext.NextTaskType} (Phase {chainContext.CurrentPhase + 1})", "TaskQueueService");

        // Update shared data with output from previous phase
        if (chainContext.SharedData == null)
        {
            chainContext.SharedData = new Dictionary<string, object>();
        }

        // Store previous phase output
        chainContext.SharedData[$"phase{chainContext.CurrentPhase}_output"] = completedTask.OutputData ?? string.Empty;

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

        var chainContext = pausedTask.ChainContext;
        if (chainContext == null || string.IsNullOrEmpty(chainContext.NextTaskType))
        {
            throw new InvalidOperationException("Task does not have a next phase defined");
        }

        _logger.Info($"Continuing chain {request.CorrelationId} from paused task {request.PausedTaskId}", "TaskQueueService");

        // Merge user input into shared data
        if (request.UserInput != null)
        {
            if (chainContext.SharedData == null)
            {
                chainContext.SharedData = new Dictionary<string, object>();
            }

            foreach (var kvp in request.UserInput)
            {
                chainContext.SharedData[kvp.Key] = kvp.Value;
            }
        }

        // Update paused task to completed now that user has confirmed
        pausedTask.Status = TaskStatus.Completed;
        await _eventEmitter.EmitAsync(ModuleNames.TASK_QUEUE, TaskQueueEvents.COMPLETED, pausedTask).ConfigureAwait(false);

        // Create next task in chain
        // This will be implemented based on the specific task type
        _logger.Info($"Creating next task type: {chainContext.NextTaskType}", "TaskQueueService");

        // Return placeholder - actual implementation will create the task via facade
        return "Chain continuation initiated";
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
}
