# Task Queue System

**Version:** 1.0
**Last Updated:** 2026-02-25
**Module:** TaskQueue
**Status:** ✅ Production Ready

---

## Overview

The Task Queue System provides asynchronous background task processing with real-time progress tracking and chain-phase workflow support. It implements a producer-consumer pattern where tasks can be queued, processed sequentially, and monitored through real-time event notifications.

### Key Features

- ✅ **Asynchronous Task Processing** - Non-blocking background task execution
- ✅ **Real-time Progress Updates** - Event-driven progress notifications to frontend
- ✅ **Chain-Phase Workflows** - Multi-step tasks with automatic or user-confirmed continuation
- ✅ **Correlation ID Grouping** - Related tasks grouped by correlation ID
- ✅ **Pause & Resume** - Tasks can pause for user input and resume later
- ✅ **Profile-Scoped** - Each profile has its own isolated task queue
- ✅ **Cancellation Support** - Tasks can be cancelled mid-execution

---

## Architecture

### Backend Structure

```
D3dxSkinManager/Modules/TaskQueue/
├── Models/
│   ├── TaskInfo.cs                      # Task metadata and state
│   ├── TaskStatus.cs                    # Task status enum
│   ├── TaskProgress.cs                  # Progress reporting model
│   ├── TaskChainContext.cs              # Chain workflow configuration
│   ├── CompressFolderTaskInput.cs       # Task-specific input models
│   ├── CompressFolderTaskOutput.cs      # Task-specific output models
│   └── ImportFromTempTaskInput.cs
├── Services/
│   ├── ITaskQueueService.cs             # Core service interface
│   ├── TaskQueueService.cs              # Task queue orchestration
│   ├── ITaskProcessor.cs                # Task processor interface
│   ├── EventProgressReporter.cs         # Progress event emission
│   ├── CompressFolderTaskProcessor.cs   # Folder compression processor
│   ├── ImportFromTempTaskProcessor.cs   # Import from temp processor
│   └── ModImportTaskProcessor.cs        # Direct archive import processor
├── TaskQueueEvents.cs                   # Event type constants
├── TaskQueueFacade.cs                   # IPC routing facade
└── TaskQueueServiceExtensions.cs        # DI registration
```

### Frontend Structure

```
D3dxSkinManager.Client/src/modules/taskQueue/
├── types/
│   └── task.types.ts                    # TypeScript type definitions
├── services/
│   └── taskQueueService.ts              # Frontend service wrapper
└── components/
    └── TaskQueueView.tsx                # UI component (used in ModManagementScreen)
```

---

## Core Concepts

### 1. Task Lifecycle

```
┌─────────┐     ┌────────────┐     ┌───────────┐     ┌───────────┐
│ Pending │ --> │ Processing │ --> │ Completed │     │  Failed   │
└─────────┘     └────────────┘     └───────────┘     └───────────┘
                      │                                     ▲
                      │                                     │
                      └─────────────────────────────────────┘
                             (on error)

                      │
                      ▼
              ┌──────────────────────┐
              │ AwaitingConfirmation │  (chain pause)
              └──────────────────────┘
                      │
                      ▼ (user action)
              ┌────────────┐
              │ Processing │  (next phase)
              └────────────┘
```

**Task Statuses:**
- `Pending` - Task queued, waiting to be processed
- `Processing` - Task currently executing
- `Completed` - Task finished successfully
- `Failed` - Task encountered an error
- `Cancelled` - Task was cancelled by user
- `AwaitingConfirmation` - Task paused in chain, waiting for user input

### 2. Chain-Phase Workflows

Tasks can be linked in chains using **correlation IDs**. Each task in a chain knows about the next task and can either:

1. **Auto-continue**: Automatically create and start the next task
2. **Pause for user input**: Set `RequiresUserAction = true` to pause the chain

**Example: Folder Import Chain**

```
Phase 1: compress_folder
  ├─ Input: { folderPath }
  ├─ Output: { tempArchivePath, folderName }
  ├─ Action: Compress folder to temp directory
  └─ RequiresUserAction: true (pause for metadata)
       │
       ▼ (user provides metadata)
       │
Phase 2: import_from_temp
  ├─ Input: { tempArchivePath, name, author, tags, ... }
  ├─ Output: { sha, name, success }
  ├─ Action: Import mod and apply metadata
  └─ Cleanup: Delete temp file
```

### 3. TaskChainContext

Controls chain behavior:

```csharp
public class TaskChainContext
{
    public string CorrelationId { get; set; }        // Groups related tasks
    public int CurrentPhase { get; set; }             // Current phase number
    public int TotalPhases { get; set; }              // Total phases in chain
    public bool RequiresUserAction { get; set; }      // Pause for user input?
    public string? NextTaskType { get; set; }         // Next task type to create
    public Dictionary<string, object>? SharedData { get; set; }  // Data passed between phases
    public string? UserActionDescription { get; set; } // Description for user
}
```

---

## Event System Integration

### Event Flow

```
Backend                          IPC Bridge              Frontend
────────                         ──────────              ────────
TaskQueueService
    │
    ├─ EmitAsync(TASK_ADDED)
    │      │
    │      └──> EventBus ──────> EventBusIpcBridge ──> WebView2 ──> bridgeService
    │                                                                     │
    │                                                                     └──> eventBus.emit()
    │                                                                              │
    │                                                                              └──> TaskQueueView
    │                                                                                      │
    │                                                                                      └──> setState()
    │
    ├─ EmitAsync(TASK_STARTED)
    ├─ EmitAsync(TASK_PROGRESS)
    ├─ EmitAsync(TASK_COMPLETED)
    ├─ EmitAsync(TASK_FAILED)
    └─ EmitAsync(TASK_AWAITING_CONFIRMATION)
```

### Event Types

**Defined in:** `Modules/TaskQueue/TaskQueueEvents.cs`

```csharp
public static class TaskQueueEvents
{
    public const string TASK_ADDED = "TASK_ADDED";
    public const string TASK_STARTED = "TASK_STARTED";
    public const string TASK_PROGRESS = "TASK_PROGRESS";
    public const string TASK_COMPLETED = "TASK_COMPLETED";
    public const string TASK_FAILED = "TASK_FAILED";
    public const string TASK_CANCELLED = "TASK_CANCELLED";
    public const string TASK_REMOVED = "TASK_REMOVED";
    public const string TASK_AWAITING_CONFIRMATION = "TASK_AWAITING_CONFIRMATION";
}
```

**⚠️ CRITICAL:** All TaskQueue events MUST be registered in `CoreEvents.All` array for IPC bridge forwarding!

**File:** `Modules/Core/Event/CoreEvents.cs`

```csharp
public static readonly string[] All = new[]
{
    APPLICATION_STARTED,
    APPLICATION_SHUTDOWN,
    // ... other events ...

    // TaskQueue events - REQUIRED for IPC forwarding
    TASK_ADDED,
    TASK_STARTED,
    TASK_PROGRESS,
    TASK_COMPLETED,
    TASK_FAILED,
    TASK_CANCELLED,
    TASK_REMOVED,
    TASK_AWAITING_CONFIRMATION,
};
```

### Frontend Event Subscription

**TypeScript Event Types:** `src/shared/services/eventBus.ts`

```typescript
export enum EventType {
  TaskAdded = 'TASK_ADDED',
  TaskStarted = 'TASK_STARTED',
  TaskProgress = 'TASK_PROGRESS',
  TaskCompleted = 'TASK_COMPLETED',
  TaskFailed = 'TASK_FAILED',
  TaskCancelled = 'TASK_CANCELLED',
  TaskRemoved = 'TASK_REMOVED',
  TaskAwaitingConfirmation = 'TASK_AWAITING_CONFIRMATION',
}
```

**Subscription Pattern:**

```typescript
// Subscribe to task events
useEffect(() => {
  const unsubscribe = eventBus.subscribe<TaskInfo>(EventType.TaskCompleted, (event) => {
    if (event?.data) {
      // Update state with completed task
      setTasks(prev => prev.map(t =>
        t.id === event.data!.id ? { ...event.data! } : t
      ));
    }
  });

  return unsubscribe; // Cleanup on unmount
}, []);
```

**⚠️ Important:** The `eventBus.subscribe()` method returns a cleanup function directly (not a subscription object). Always return it from useEffect.

---

## Implementation Guide

### Creating a New Task Type

#### 1. Define Input/Output Models

**File:** `Modules/TaskQueue/Models/YourTaskInput.cs`

```csharp
namespace D3dxSkinManager.Modules.TaskQueue.Models;

public class YourTaskInput
{
    public string RequiredField { get; set; } = string.Empty;
    public string? OptionalField { get; set; }
}

public class YourTaskOutput
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultData { get; set; }
}
```

#### 2. Create Task Processor

**File:** `Modules/TaskQueue/Services/YourTaskProcessor.cs`

```csharp
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

public class YourTaskProcessor : ITaskProcessor<YourTaskInput, YourTaskOutput>
{
    private readonly ILogHelper _logger;
    // Inject any dependencies you need

    public string TaskType => "your_task_type";

    public YourTaskProcessor(ILogHelper logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateInputAsync(YourTaskInput input)
    {
        // Validate input before processing
        if (string.IsNullOrEmpty(input.RequiredField))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task<YourTaskOutput> ProcessAsync(
        YourTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        try
        {
            await progressReporter.ReportProgressAsync(10, "Starting task...").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Do your work here
            // Report progress as you go
            await progressReporter.ReportProgressAsync(50, "Halfway done...").ConfigureAwait(false);

            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            await progressReporter.ReportProgressAsync(100, "Completed").ConfigureAwait(false);

            return new YourTaskOutput
            {
                Success = true,
                ResultData = "Success!"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Task cancelled", "YourTaskProcessor");
            await progressReporter.ReportCancellationAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Task failed: {ex.Message}", "YourTaskProcessor", ex);
            await progressReporter.ReportFailureAsync(ex.Message).ConfigureAwait(false);

            return new YourTaskOutput
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
```

#### 3. Register Processor in DI

**File:** `Modules/TaskQueue/TaskQueueServiceExtensions.cs`

```csharp
public static IServiceCollection AddTaskQueueModule(this IServiceCollection services)
{
    // ... existing registrations ...

    // Task processors
    services.TryAddSingleton<YourTaskProcessor>();

    return services;
}
```

#### 4. Add Processing Logic to TaskQueueService

**File:** `Modules/TaskQueue/Services/TaskQueueService.cs`

```csharp
// In ProcessTaskAsync method, add case to switch statement:
dynamic? output = task.Type switch
{
    "mod_import" => await ProcessModImportTaskAsync(task, progressReporter, _currentTaskCts.Token).ConfigureAwait(false),
    "your_task_type" => await ProcessYourTaskAsync(task, progressReporter, _currentTaskCts.Token).ConfigureAwait(false),
    _ => throw new NotSupportedException($"Task type not supported: {task.Type}")
};

// Add processing method:
private async Task<YourTaskOutput> ProcessYourTaskAsync(
    TaskInfo task,
    EventProgressReporter progressReporter,
    CancellationToken ct)
{
    var processor = _serviceProvider.GetService(typeof(YourTaskProcessor)) as YourTaskProcessor;

    if (processor == null)
    {
        throw new InvalidOperationException("YourTaskProcessor not registered in DI container");
    }

    var input = JsonSerializer.Deserialize<YourTaskInput>(task.InputData, JsonOptions);
    if (input == null)
    {
        throw new InvalidOperationException("Failed to deserialize task input");
    }

    return await processor.ProcessAsync(input, progressReporter, ct).ConfigureAwait(false);
}
```

#### 5. Add Facade Method for IPC

**File:** `Modules/TaskQueue/TaskQueueFacade.cs`

```csharp
// Add case to switch in RouteMessageAsync:
var taskId = taskType switch
{
    "mod_import" => await AddModImportTaskAsync(request, profileId).ConfigureAwait(false),
    "your_task_type" => await AddYourTaskAsync(request, profileId).ConfigureAwait(false),
    _ => throw new NotSupportedException($"Task type not supported: {taskType}")
};

// Add handler method:
private async Task<string> AddYourTaskAsync(IpcRequest request, string? profileId)
{
    var input = _payloadHelper.GetRequiredValue<YourTaskInput>(request.Payload, "input");
    return await _taskQueueService.AddTaskAsync("your_task_type", input, profileId).ConfigureAwait(false);
}
```

#### 6. Frontend TypeScript Types

**File:** `src/modules/taskQueue/types/task.types.ts`

```typescript
export interface YourTaskInput {
  requiredField: string;
  optionalField?: string;
}

export interface YourTaskOutput {
  success: boolean;
  errorMessage?: string;
  resultData?: string;
}
```

#### 7. Frontend Service Method

**File:** `src/modules/taskQueue/services/taskQueueService.ts`

```typescript
async addYourTask(profileId: string, input: YourTaskInput): Promise<string> {
  return bridgeService.sendMessage<string>({
    module: 'TASK_QUEUE',
    type: 'ADD_TASK',
    profileId,
    payload: {
      taskType: 'your_task_type',
      input,
    },
  });
}
```

### Creating Chain-Phase Tasks

#### Example: Two-Phase Import Workflow

**Phase 1: Compress Folder**

```csharp
var chainContext = new TaskChainContext
{
    CorrelationId = $"CORR-{Guid.NewGuid():N}",
    CurrentPhase = 1,
    TotalPhases = 2,
    RequiresUserAction = true,  // Pause for user metadata input
    NextTaskType = "import_from_temp",
    UserActionDescription = "Please provide metadata for the mod import",
    SharedData = new Dictionary<string, object>
    {
        ["originalFolderPath"] = folderPath,
        ["metadata_name"] = providedName ?? string.Empty,
        // Store any data needed by next phase
    }
};

var taskId = await _taskQueueService.AddTaskAsync(
    "compress_folder",
    compressInput,
    profileId,
    chainContext
);
```

**Phase 2: Resume Chain**

Frontend sends continue request after user provides metadata:

```typescript
await taskQueueService.continueChain(
  correlationId,
  pausedTaskId,
  {
    name: 'User Provided Name',
    author: 'User Provided Author',
    tags: ['tag1', 'tag2'],
  }
);
```

Backend creates next task in chain:

```csharp
public async Task<string> ContinueChainAsync(ContinueChainRequest request)
{
    // Find paused task
    var pausedTask = await GetTaskAsync(request.PausedTaskId).ConfigureAwait(false);
    var chainContext = pausedTask.ChainContext;

    // Merge user input with shared data
    if (request.UserInput != null)
    {
        foreach (var kvp in request.UserInput)
        {
            chainContext.SharedData[kvp.Key] = kvp.Value;
        }
    }

    // Create next task input from shared data
    var nextInput = CreateNextTaskInput(chainContext);

    // Update chain context for next phase
    chainContext.CurrentPhase++;
    chainContext.RequiresUserAction = false; // Auto-continue

    // Add next task
    return await AddTaskAsync(
        chainContext.NextTaskType!,
        nextInput,
        pausedTask.ProfileId,
        chainContext
    );
}
```

---

## Common Patterns

### Pattern 1: Single-Phase Task (No Chain)

```csharp
// Just add task without chain context
var taskId = await _taskQueueService.AddTaskAsync(
    "mod_import",
    new ModImportTaskInput { FilePath = archivePath },
    profileId
);
```

### Pattern 2: Auto-Continuing Chain

```csharp
var chainContext = new TaskChainContext
{
    CorrelationId = correlationId,
    CurrentPhase = 1,
    TotalPhases = 3,
    RequiresUserAction = false,  // Auto-continue
    NextTaskType = "next_task_type"
};

await _taskQueueService.AddTaskAsync("first_task", input, profileId, chainContext);
// Next task will be created automatically when first task completes
```

### Pattern 3: User-Confirmed Chain

```csharp
var chainContext = new TaskChainContext
{
    CorrelationId = correlationId,
    CurrentPhase = 1,
    TotalPhases = 2,
    RequiresUserAction = true,  // Pause for user
    NextTaskType = "next_task_type",
    UserActionDescription = "Please review and confirm"
};

await _taskQueueService.AddTaskAsync("first_task", input, profileId, chainContext);
// Task will pause with status = AwaitingConfirmation
// Frontend receives TASK_AWAITING_CONFIRMATION event
// User confirms, frontend calls ContinueChainAsync
```

---

## Troubleshooting

### Events Not Reaching Frontend

**Symptom:** Backend emits events but frontend doesn't update.

**Checklist:**

1. ✅ **Verify event is in CoreEvents.All array**
   ```csharp
   // File: Modules/Core/Event/CoreEvents.cs
   public static readonly string[] All = new[]
   {
       // ... must include your event ...
       TASK_YOUR_EVENT,
   };
   ```

2. ✅ **Check EventType enum in frontend**
   ```typescript
   // File: src/shared/services/eventBus.ts
   export enum EventType {
       YourEvent = 'YOUR_EVENT',  // Must match backend constant
   }
   ```

3. ✅ **Verify subscription in component**
   ```typescript
   useEffect(() => {
     const unsubscribe = eventBus.subscribe<YourData>(EventType.YourEvent, (event) => {
       console.log('Event received:', event?.data);
     });
     return unsubscribe;
   }, []);
   ```

4. ✅ **Check backend logs** (set log level to Verbose)
   ```
   [VERBOSE] [EventEmitter] Emitting event: YOUR_EVENT
   [VERBOSE] [EventBus] Emitting YOUR_EVENT to 1 handler(s)
   [VERBOSE] [EventBridge] Forwarding event to frontend: YOUR_EVENT
   ```

5. ✅ **Verify data unwrapping in bridgeService**

   Backend wraps data in `{ eventName, data }`. BridgeService must unwrap:
   ```typescript
   // File: src/shared/services/bridgeService.ts
   const actualData = parsed.data?.data ?? parsed.data;
   eventBus.emit({ type: parsed.type, data: actualData });
   ```

### Task Not Processing

**Symptom:** Task stuck in "Pending" status.

**Solution:** Call `processNext()` to start processing:

```typescript
await taskQueueService.processNext(profileId);
```

Or enable auto-process in TaskQueueView.

### Chain Not Continuing

**Symptom:** First phase completes but second phase never starts.

**Check:**

1. `RequiresUserAction` set correctly?
2. `NextTaskType` specified?
3. Frontend sent `CONTINUE_CHAIN` message?
4. Check backend logs for chain creation errors

---

## Best Practices

### ✅ DO

- **Use correlation IDs** to group related tasks
- **Report progress frequently** for better UX (every 10-20% is good)
- **Check cancellation tokens** at each major step
- **Clean up resources** in finally blocks
- **Validate inputs** before processing
- **Use specific error messages** for debugging
- **Log at appropriate levels** (Info for completion, Verbose for progress)

### ❌ DON'T

- Don't block the UI thread - tasks run on background threads
- Don't forget to handle cancellation
- Don't create circular chain dependencies
- Don't emit events manually - use EventProgressReporter
- Don't skip input validation
- Don't use Debug logs for high-frequency events (use Verbose)

---

## Performance Considerations

- **One task processes at a time per profile** - Sequential processing prevents race conditions
- **Tasks are profile-scoped** - Each profile has isolated queue
- **Progress events are throttled** - EventProgressReporter is efficient
- **Cancellation is cooperative** - Check CancellationToken frequently for responsive cancellation

---

## Related Documentation

- [IPC Event Notifications](../AI_GUIDE.md#ipc-event-notifications) - Event system patterns
- [Profile System](PROFILE_SYSTEM.md) - Profile-scoped services
- [Error Handling](../AI_GUIDE.md#error-handling-pattern) - Exception patterns

---

## Version History

- **1.0** (2026-02-25) - Initial implementation with chain-phase workflow support
