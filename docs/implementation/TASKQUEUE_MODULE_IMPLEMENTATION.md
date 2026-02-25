# TaskQueue Module Implementation Plan

**Created:** 2026-02-25
**Status:** Design Phase
**Purpose:** Reusable task/queue management system for long-running operations

---

## Module Overview

### Responsibilities
- Manage task queue lifecycle (add, process, cancel, remove)
- Execute tasks with progress tracking
- Emit real-time progress events to frontend
- Thread-safe task processing
- Extensible processor pattern for different task types

### Non-Responsibilities
- Task-specific business logic (delegated to processors)
- UI rendering (handled by frontend modules)
- Direct database operations (processors handle this)

---

## Backend Architecture

### 1. Models

#### TaskInfo.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public class TaskInfo
{
    public string Id { get; set; }              // TASK-{Guid}
    public string Type { get; set; }            // "mod_import", "mod_export", etc.
    public TaskStatus Status { get; set; }
    public int Progress { get; set; }           // 0-100
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string InputData { get; set; }       // JSON serialized input
    public string? OutputData { get; set; }     // JSON serialized output
    public string? ErrorMessage { get; set; }

    public string? OperationId { get; set; }    // For IProgressReporter integration
    public string? ProfileId { get; set; }      // Profile context
}
```

#### TaskStatus.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public enum TaskStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
```

#### TaskProgress.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public class TaskProgress
{
    public string TaskId { get; set; }
    public int Progress { get; set; }           // 0-100
    public string? CurrentStep { get; set; }
    public string? Message { get; set; }
}
```

#### TaskQueueEvents.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue;

public static class TaskQueueEvents
{
    public const string TASK_ADDED = "TASK_ADDED";
    public const string TASK_STARTED = "TASK_STARTED";
    public const string TASK_PROGRESS = "TASK_PROGRESS";
    public const string TASK_COMPLETED = "TASK_COMPLETED";
    public const string TASK_FAILED = "TASK_FAILED";
    public const string TASK_CANCELLED = "TASK_CANCELLED";
    public const string TASK_REMOVED = "TASK_REMOVED";
}
```

### 2. Services

#### ITaskProcessor.cs - Generic Processor Interface
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Services;

public interface ITaskProcessor<TInput, TOutput>
{
    /// <summary>
    /// Process a task with progress reporting
    /// </summary>
    Task<TOutput> ProcessAsync(
        TInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validate input before queuing
    /// </summary>
    Task<bool> ValidateInputAsync(TInput input);

    /// <summary>
    /// Task type identifier
    /// </summary>
    string TaskType { get; }
}
```

#### ModImportTaskInput.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public class ModImportTaskInput
{
    public string FilePath { get; set; }        // Archive or folder path
    public bool IsFolder { get; set; }
    public string? ProfileId { get; set; }

    // Optional metadata overrides
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Grading { get; set; }
    public List<string>? Tags { get; set; }
}
```

#### ModImportTaskOutput.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public class ModImportTaskOutput
{
    public string Sha { get; set; }
    public string Name { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

#### ModImportTaskProcessor.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Services;

public class ModImportTaskProcessor : ITaskProcessor<ModImportTaskInput, ModImportTaskOutput>
{
    private readonly IModImportService _importService;
    private readonly IArchiveHelper _archiveHelper;
    private readonly ILogger _logger;

    public string TaskType => "mod_import";

    public ModImportTaskProcessor(
        IModImportService importService,
        IArchiveHelper archiveHelper,
        ILogger logger)
    {
        _importService = importService;
        _archiveHelper = archiveHelper;
        _logger = logger;
    }

    public async Task<bool> ValidateInputAsync(ModImportTaskInput input)
    {
        if (input.IsFolder)
        {
            return Directory.Exists(input.FilePath);
        }
        else
        {
            return File.Exists(input.FilePath);
        }
    }

    public async Task<ModImportTaskOutput> ProcessAsync(
        ModImportTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        try
        {
            ModInfo? mod = null;

            // Step 1: Compress folder if needed (0-30%)
            if (input.IsFolder)
            {
                await progressReporter.ReportProgressAsync(10, "Compressing folder...");
                var tempArchive = await CompressFolderAsync(input.FilePath, cancellationToken);
                input.FilePath = tempArchive;
                await progressReporter.ReportProgressAsync(30, "Folder compressed");
            }
            else
            {
                await progressReporter.ReportProgressAsync(10, "Validating archive...");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Import mod (30-90%)
            await progressReporter.ReportProgressAsync(40, "Importing mod...");
            mod = await _importService.ImportAsync(input.FilePath);

            if (mod == null)
            {
                throw new Exception("Import returned null");
            }

            await progressReporter.ReportProgressAsync(80, "Mod imported");

            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Update metadata if provided (90-100%)
            if (input.Name != null || input.Author != null || input.Tags != null)
            {
                await progressReporter.ReportProgressAsync(90, "Updating metadata...");
                // Update metadata via ModManagementService
            }

            await progressReporter.ReportProgressAsync(100, "Import completed");

            return new ModImportTaskOutput
            {
                Sha = mod.Sha,
                Name = mod.Name,
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            await progressReporter.ReportCancellationAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Import task failed: {ex.Message}", "ModImportTaskProcessor", ex);
            await progressReporter.ReportFailureAsync(ex.Message);

            return new ModImportTaskOutput
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> CompressFolderAsync(string folderPath, CancellationToken ct)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"mod_import_{Guid.NewGuid():N}.zip"
        );

        await _archiveHelper.CompressFolderAsync(folderPath, tempPath, ArchiveFormat.Zip);
        return tempPath;
    }
}
```

#### ITaskQueueService.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Services;

public interface ITaskQueueService
{
    /// <summary>
    /// Add a task to the queue
    /// </summary>
    Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null);

    /// <summary>
    /// Start processing the next pending task
    /// </summary>
    Task ProcessNextTaskAsync();

    /// <summary>
    /// Cancel a running task
    /// </summary>
    Task CancelTaskAsync(string taskId);

    /// <summary>
    /// Remove a task from queue
    /// </summary>
    Task RemoveTaskAsync(string taskId);

    /// <summary>
    /// Get all tasks
    /// </summary>
    Task<List<TaskInfo>> GetAllTasksAsync();

    /// <summary>
    /// Get task by ID
    /// </summary>
    Task<TaskInfo?> GetTaskAsync(string taskId);

    /// <summary>
    /// Clear completed/failed tasks
    /// </summary>
    Task ClearCompletedTasksAsync();
}
```

#### TaskQueueService.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Services;

public class TaskQueueService : ITaskQueueService
{
    private readonly ConcurrentDictionary<string, TaskInfo> _tasks;
    private readonly SemaphoreSlim _processorLock;
    private readonly IEventEmitter _eventEmitter;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _currentTaskCts;

    public TaskQueueService(
        IEventEmitter eventEmitter,
        ILogger logger,
        IServiceProvider serviceProvider)
    {
        _tasks = new ConcurrentDictionary<string, TaskInfo>();
        _processorLock = new SemaphoreSlim(1, 1);
        _eventEmitter = eventEmitter;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> AddTaskAsync<TInput>(string taskType, TInput input, string? profileId = null)
    {
        var taskId = $"TASK-{Guid.NewGuid():N}";

        var task = new TaskInfo
        {
            Id = taskId,
            Type = taskType,
            Status = TaskStatus.Pending,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            InputData = JsonSerializer.Serialize(input),
            ProfileId = profileId
        };

        if (!_tasks.TryAdd(taskId, task))
        {
            throw new InvalidOperationException($"Failed to add task {taskId}");
        }

        _logger.Info($"Task added: {taskId} (Type: {taskType})", "TaskQueueService");

        // Emit event
        await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_ADDED, data: task);

        return taskId;
    }

    public async Task ProcessNextTaskAsync()
    {
        if (!await _processorLock.WaitAsync(0))
        {
            _logger.Debug("Processor already running", "TaskQueueService");
            return;
        }

        try
        {
            // Find next pending task
            var nextTask = _tasks.Values
                .Where(t => t.Status == TaskStatus.Pending)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefault();

            if (nextTask == null)
            {
                _logger.Debug("No pending tasks", "TaskQueueService");
                return;
            }

            await ProcessTaskAsync(nextTask);
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
            task.Status = TaskStatus.Processing;
            task.StartedAt = DateTime.UtcNow;
            task.OperationId = Guid.NewGuid().ToString();

            _logger.Info($"Processing task: {task.Id}", "TaskQueueService");

            // Emit TASK_STARTED event
            await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_STARTED, data: task);

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

            // Get processor and process task
            dynamic? output = task.Type switch
            {
                "mod_import" => await ProcessModImportTaskAsync(task, progressReporter, _currentTaskCts.Token),
                _ => throw new NotSupportedException($"Task type not supported: {task.Type}")
            };

            // Mark completed
            task.Status = TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.Progress = 100;
            task.OutputData = output != null ? JsonSerializer.Serialize(output) : null;

            _logger.Info($"Task completed: {task.Id}", "TaskQueueService");

            // Emit TASK_COMPLETED event
            await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_COMPLETED, data: task);
        }
        catch (OperationCanceledException)
        {
            task.Status = TaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;

            _logger.Warn($"Task cancelled: {task.Id}", "TaskQueueService");

            await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_CANCELLED, data: task);
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.ErrorMessage = ex.Message;

            _logger.Error($"Task failed: {task.Id} - {ex.Message}", "TaskQueueService", ex);

            await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_FAILED, data: task);
        }
        finally
        {
            _currentTaskCts?.Dispose();
            _currentTaskCts = null;
        }
    }

    private async Task<ModImportTaskOutput> ProcessModImportTaskAsync(
        TaskInfo task,
        IProgressReporter progressReporter,
        CancellationToken ct)
    {
        var processor = _serviceProvider.GetRequiredService<ModImportTaskProcessor>();
        var input = JsonSerializer.Deserialize<ModImportTaskInput>(task.InputData)
            ?? throw new InvalidOperationException("Failed to deserialize task input");

        return await processor.ProcessAsync(input, progressReporter, ct);
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
            await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_REMOVED, data: task);
        }
    }

    public Task<List<TaskInfo>> GetAllTasksAsync()
    {
        return Task.FromResult(_tasks.Values.OrderBy(t => t.CreatedAt).ToList());
    }

    public Task<TaskInfo?> GetTaskAsync(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public async Task ClearCompletedTasksAsync()
    {
        var completedTasks = _tasks.Values
            .Where(t => t.Status == TaskStatus.Completed || t.Status == TaskStatus.Failed)
            .ToList();

        foreach (var task in completedTasks)
        {
            await RemoveTaskAsync(task.Id);
        }
    }
}
```

#### EventProgressReporter.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Services;

public class EventProgressReporter : IProgressReporter
{
    private readonly string _taskId;
    private readonly IEventEmitter _eventEmitter;
    private readonly ILogger _logger;
    private readonly Action<int, string?> _onProgress;

    public EventProgressReporter(
        string taskId,
        IEventEmitter eventEmitter,
        ILogger logger,
        Action<int, string?> onProgress)
    {
        _taskId = taskId;
        _eventEmitter = eventEmitter;
        _logger = logger;
        _onProgress = onProgress;
    }

    public bool IsCancelled { get; private set; }

    public async Task ReportProgressAsync(int percentComplete, string? currentStep = null)
    {
        _onProgress(percentComplete, currentStep);

        var progress = new TaskProgress
        {
            TaskId = _taskId,
            Progress = percentComplete,
            CurrentStep = currentStep,
            Message = currentStep
        };

        await _eventEmitter.EmitAsync(TaskQueueEvents.TASK_PROGRESS, data: progress);

        _logger.Debug($"Task {_taskId} progress: {percentComplete}% - {currentStep}", "EventProgressReporter");
    }

    public async Task ReportCompletionAsync()
    {
        await ReportProgressAsync(100, "Completed");
    }

    public async Task ReportFailureAsync(string errorMessage)
    {
        _logger.Error($"Task {_taskId} failed: {errorMessage}", "EventProgressReporter");
        await Task.CompletedTask;
    }

    public async Task ReportCancellationAsync()
    {
        IsCancelled = true;
        _logger.Warn($"Task {_taskId} cancelled", "EventProgressReporter");
        await Task.CompletedTask;
    }
}
```

### 3. Facade

#### TaskQueueFacade.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue;

public class TaskQueueFacade : BaseFacade
{
    private readonly ITaskQueueService _taskQueueService;

    public TaskQueueFacade(
        ITaskQueueService taskQueueService,
        IPayloadHelper payloadHelper,
        ILogger logger)
        : base(payloadHelper, logger)
    {
        _taskQueueService = taskQueueService;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "ADD_TASK" => await AddTaskAsync(request),
            "PROCESS_NEXT" => await ProcessNextTaskAsync(),
            "CANCEL_TASK" => await CancelTaskAsync(request),
            "REMOVE_TASK" => await RemoveTaskAsync(request),
            "GET_ALL_TASKS" => await GetAllTasksAsync(),
            "GET_TASK" => await GetTaskAsync(request),
            "CLEAR_COMPLETED" => await ClearCompletedTasksAsync(),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    private async Task<string> AddTaskAsync(IpcRequest request)
    {
        var taskType = GetRequiredValue<string>(request.Payload, "taskType");
        var inputJson = GetRequiredValue<string>(request.Payload, "input");
        var profileId = GetOptionalValue<string>(request.Payload, "profileId");

        // Deserialize based on task type
        dynamic input = taskType switch
        {
            "mod_import" => JsonSerializer.Deserialize<ModImportTaskInput>(inputJson),
            _ => throw new NotSupportedException($"Task type not supported: {taskType}")
        };

        return await _taskQueueService.AddTaskAsync(taskType, input, profileId);
    }

    private async Task<object> ProcessNextTaskAsync()
    {
        await _taskQueueService.ProcessNextTaskAsync();
        return new { success = true };
    }

    private async Task<object> CancelTaskAsync(IpcRequest request)
    {
        var taskId = GetRequiredValue<string>(request.Payload, "taskId");
        await _taskQueueService.CancelTaskAsync(taskId);
        return new { success = true };
    }

    private async Task<object> RemoveTaskAsync(IpcRequest request)
    {
        var taskId = GetRequiredValue<string>(request.Payload, "taskId");
        await _taskQueueService.RemoveTaskAsync(taskId);
        return new { success = true };
    }

    private async Task<List<TaskInfo>> GetAllTasksAsync()
    {
        return await _taskQueueService.GetAllTasksAsync();
    }

    private async Task<TaskInfo?> GetTaskAsync(IpcRequest request)
    {
        var taskId = GetRequiredValue<string>(request.Payload, "taskId");
        return await _taskQueueService.GetTaskAsync(taskId);
    }

    private async Task<object> ClearCompletedTasksAsync()
    {
        await _taskQueueService.ClearCompletedTasksAsync();
        return new { success = true };
    }
}
```

### 4. Service Registration

#### TaskQueueServiceExtensions.cs
```csharp
namespace D3dxSkinManager.Modules.TaskQueue;

public static class TaskQueueServiceExtensions
{
    public static IServiceCollection AddTaskQueueServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ITaskQueueService, TaskQueueService>();

        // Task processors
        services.AddSingleton<ModImportTaskProcessor>();

        // Facade
        services.AddSingleton<TaskQueueFacade>();

        return services;
    }
}
```

---

## Frontend Architecture

### 1. Service

#### taskQueueService.ts
```typescript
import { BaseModuleService } from '@/shared/services/baseModuleService';

export interface TaskInfo {
  id: string;
  type: string;
  status: 'pending' | 'processing' | 'completed' | 'failed' | 'cancelled';
  progress: number;
  message?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  inputData: string;
  outputData?: string;
  errorMessage?: string;
  operationId?: string;
  profileId?: string;
}

export interface TaskProgress {
  taskId: string;
  progress: number;
  currentStep?: string;
  message?: string;
}

class TaskQueueService extends BaseModuleService {
  constructor() {
    super('TASKQUEUE');
  }

  async addTask<TInput>(taskType: string, input: TInput, profileId?: string): Promise<string> {
    return this.sendMessage<string>('ADD_TASK', profileId, {
      taskType,
      input: JSON.stringify(input),
      profileId
    });
  }

  async processNext(): Promise<void> {
    await this.sendMessage('PROCESS_NEXT');
  }

  async cancelTask(taskId: string): Promise<void> {
    await this.sendMessage('CANCEL_TASK', undefined, { taskId });
  }

  async removeTask(taskId: string): Promise<void> {
    await this.sendMessage('REMOVE_TASK', undefined, { taskId });
  }

  async getAllTasks(): Promise<TaskInfo[]> {
    return this.sendArrayMessage<TaskInfo>('GET_ALL_TASKS');
  }

  async getTask(taskId: string): Promise<TaskInfo | undefined> {
    return this.sendOptionalMessage<TaskInfo>('GET_TASK', undefined, { taskId });
  }

  async clearCompleted(): Promise<void> {
    await this.sendMessage('CLEAR_COMPLETED');
  }
}

export const taskQueueService = new TaskQueueService();
```

### 2. Event Types

#### eventBus.ts - Add new events
```typescript
export enum EventType {
  // Existing events...

  // TaskQueue events
  TaskAdded = 'TASK_ADDED',
  TaskStarted = 'TASK_STARTED',
  TaskProgress = 'TASK_PROGRESS',
  TaskCompleted = 'TASK_COMPLETED',
  TaskFailed = 'TASK_FAILED',
  TaskCancelled = 'TASK_CANCELLED',
  TaskRemoved = 'TASK_REMOVED',
}
```

---

## Implementation Steps

### Phase 1: Backend Core (Single Task Processing)
1. ✅ Create TaskQueue module structure
2. ✅ Implement TaskInfo, TaskStatus, TaskProgress models
3. ✅ Implement TaskQueueEvents constants
4. ✅ Implement ITaskProcessor interface
5. ✅ Implement EventProgressReporter
6. ✅ Implement TaskQueueService (single task processing only)
7. ✅ Implement TaskQueueFacade
8. ✅ Register services in DI container
9. ✅ Add to AppFacade routing

### Phase 2: Mod Import Integration
1. ✅ Implement ModImportTaskProcessor
2. ✅ Add folder compression to ArchiveHelper
3. ✅ Update error codes
4. ✅ Test single mod import with progress

### Phase 3: Frontend Integration
1. ✅ Create taskQueueService.ts
2. ✅ Add TaskQueue event types
3. ✅ Update TaskQueueView to use new service
4. ✅ Listen to TASK_PROGRESS events
5. ✅ Test end-to-end single task flow

### Phase 4: Batch Processing (Future)
1. Add batch processing support to TaskQueueService
2. Implement parallel processing with configurable concurrency
3. Add batch operations UI
4. Test batch import workflow

---

## Testing Plan

### Single Task Import Test
1. User selects archive file
2. Frontend calls `taskQueueService.addTask('mod_import', input)`
3. Backend adds task to queue, emits TASK_ADDED
4. Frontend calls `taskQueueService.processNext()`
5. Backend processes task:
   - Emits TASK_STARTED
   - Emits TASK_PROGRESS (10%, 40%, 80%, 100%)
   - Emits TASK_COMPLETED
6. Frontend updates UI in real-time
7. Verify task status = completed, progress = 100

### Folder Import Test
1. User selects folder
2. Task processor compresses folder (progress 0-30%)
3. Task processor imports archive (progress 30-100%)
4. Verify compressed archive cleanup
5. Verify mod imported successfully

---

## Benefits

✅ **Separation of Concerns**: Queue management separate from business logic
✅ **Reusability**: Can process any task type via ITaskProcessor
✅ **Progress Tracking**: Real-time events to frontend
✅ **Thread Safety**: SemaphoreSlim ensures single-threaded processing
✅ **Cancellation**: CancellationToken support
✅ **Extensibility**: Easy to add new task types
✅ **Testing**: Each component testable independently

---

## Next Steps

1. Review this design with user
2. Get approval to proceed with implementation
3. Start with Phase 1: Backend Core
4. Iterate through phases with testing at each step
