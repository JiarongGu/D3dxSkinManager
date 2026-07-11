# Workflow Architecture

> **Authoritative quick-ref: `.claude/knowledge/mod-import-workflow.md`** (priority admission, crash-resume,
> temp cleanup — current). This doc is the deep expansion and may lag; where they disagree, the rule wins.

**Last Updated**: 2026-03-05
**Status**: Active

## Overview

The Workflow module provides a **simple, stateless workflow engine** for managing long-running multi-step processes. Unlike the previous TaskQueue system, this design focuses on simplicity with type-specific handlers managing their own state machines.

## Core Concepts

### 1. Workflow Model

A workflow is a simple entity with:
- **Id**: Unique identifier (e.g., `WF-{guid}`)
- **Type**: Workflow type (e.g., `MOD_IMPORT`)
- **Status**: Current status (Pending, Processing, WaitingForInput, Paused, Completed, Failed, Cancelled, Deleting)
- **Context**: JSON string containing workflow-specific state
- **Timestamps**: CreatedAt, CompletedAt

#### Status Definitions

- **Pending**: Waiting in queue to start
- **Processing**: Currently executing
- **WaitingForInput**: Paused for user metadata confirmation
- **Paused**: Manually paused by user (won't auto-resume)
- **Completed**: Successfully finished
- **Failed**: Failed with error
- **Cancelled**: Cancelled by user
- **Deleting**: Being deleted (cleanup in progress)

### 2. Type-Specific Handlers

Each workflow type has its own handler that:
- Manages workflow lifecycle
- Implements step-based state machine
- Stores state in Context JSON
- Emits events for UI updates

### 3. Stateless Design

- No complex routing or node configurations
- UI reads state via IPC, sends commands
- Backend manages state transitions
- Event-driven real-time updates

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Frontend                               │
│                                                                 │
│  ┌──────────────────┐         ┌──────────────────┐            │
│  │ FolderImportBtn  │────────>│ ModImportWorkflow│            │
│  │  (Trigger)       │         │    Screen         │            │
│  └──────────────────┘         │  (UI + Logic)     │            │
│                               └─────────┬──────────┘            │
│                                         │                        │
└─────────────────────────────────────────┼────────────────────────┘
                                          │ IPC
                                          │ START_MOD_IMPORT
                                          │ PROVIDE_METADATA
                                          │ CANCEL_MOD_IMPORT
┌─────────────────────────────────────────┼────────────────────────┐
│                       Backend           │                        │
│                                         ▼                        │
│                               ┌──────────────────┐              │
│                               │ WorkflowFacade   │              │
│                               │  (IPC Router)    │              │
│                               └────────┬─────────┘              │
│                                        │                        │
│                  ┌─────────────────────┴──────────────────┐    │
│                  │                                         │    │
│         ┌────────▼────────┐                    ┌──────────▼──────┐
│         │ WorkflowRepo    │                    │ ModImportWorkflow│
│         │  (CRUD)         │<───────────────────│    Handler       │
│         └─────────────────┘                    │ (State Machine)  │
│                                                └──────────────────┘
│                                                         │
│                                                         │ Events
│                                                         ▼
│                                                 ┌───────────────┐
│                                                 │  EventBus     │
│                                                 └───────┬───────┘
└─────────────────────────────────────────────────────────┼────────┘
                                                          │
                                                          ▼
                                                  Frontend (Events)
```

## Example: MOD_IMPORT Workflow

### Step Flow

1. **extract_metadata**: Extract metadata from folder/archive, pre-fill form with detected values
2. **compress_folder** (background): Compress folder while user reviews metadata (if folder, not archive)
3. **waiting_for_user_confirmation**: User reviews and edits metadata, then confirms
4. **import_mod**: Import the mod with user-edited metadata

### Context Structure

```csharp
public class ModImportWorkflowContext
{
    public required string Step { get; set; }
    public string? FolderPath { get; set; }
    public string? TempArchivePath { get; set; }
    public string? FolderName { get; set; }
    public int? FileCount { get; set; }

    // Metadata fields (user can edit these)
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Grading { get; set; } = "G";

    public string? ImportedModSha { get; set; }
}
```

### Handler Logic

```csharp
public class ModImportWorkflowHandler
{
    // Start workflow - extract metadata first
    public async Task<WorkflowInfo> StartImportAsync(string folderPath)
    {
        var workflow = new WorkflowInfo
        {
            Id = $"WF-{Guid.NewGuid()}",
            Type = "MOD_IMPORT",
            Status = WorkflowStatus.Processing,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext
            {
                Step = ModImportWorkflowSteps.ExtractMetadata,
                FolderPath = folderPath
            })
        };

        await _workflowRepository.AddAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CREATED, workflow);
        await ProcessStepAsync(workflow);
        return workflow;
    }

    // Continue workflow after user confirms
    public async Task<WorkflowInfo> ContinueAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);

        // User confirmed, move to import step
        context.Step = ModImportWorkflowSteps.ImportMod;
        workflow.Status = WorkflowStatus.Processing;
        await ProcessStepAsync(workflow);
        return workflow;
    }

    // Process each step
    private async Task ProcessStepAsync(WorkflowInfo workflow)
    {
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);

        switch (context.Step)
        {
            case ModImportWorkflowSteps.ExtractMetadata:
                // Extract metadata, show to user
                // Auto-start compression in background if folder
                break;
            case ModImportWorkflowSteps.CompressFolder:
                // Compress folder (runs in background)
                break;
            case ModImportWorkflowSteps.ImportMod:
                // Import with user-edited metadata
                break;
        }
    }
}
```

## Events

Workflow events are emitted via EventBus:

```csharp
// Defined in: Modules/Workflow/WorkflowEvents.cs
public static class WorkflowEvents
{
    public const string CREATED = "CREATED";
    public const string STATUS_CHANGED = "STATUS_CHANGED";
    public const string COMPLETED = "COMPLETED";
    public const string FAILED = "FAILED";
    public const string CANCELLED = "CANCELLED";
}
```

Frontend subscribes to these events for real-time updates:

```typescript
eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.STATUS_CHANGED, (event) => {
  const { workflowId, status } = event.payload;
  // Update UI
});
```

## File Structure

### Backend

```
D3dxSkinManager/Modules/Workflow/
├── Entities/
│   ├── WorkflowEntity.cs           # Database entity
│   └── WorkflowEntityMappers.cs    # Entity ↔ Model mappers
├── Models/
│   ├── Workflow.cs                 # WorkflowInfo model
│   └── ModImportWorkflowContext.cs # MOD_IMPORT context
├── Repositories/
│   ├── IWorkflowRepository.cs      # Repository interface
│   └── WorkflowRepository.cs       # In-memory implementation
├── Handlers/
│   └── ModImportWorkflowHandler.cs # MOD_IMPORT handler
├── IWorkflowFacade.cs              # Facade interface
├── WorkflowFacade.cs               # IPC routing
├── WorkflowEvents.cs               # Event constants
└── WorkflowServiceExtensions.cs    # DI registration
```

### Frontend

```
D3dxSkinManager.Client/src/modules/workflow/
├── components/
│   ├── ModImportQueueScreen.tsx    # Download manager style queue UI
│   ├── WorkflowQueueTable.tsx      # Table showing all workflows
│   ├── FolderImportButton.tsx      # Trigger import button
│   └── README.md                   # Usage documentation
├── hooks/
│   ├── useWorkflowQueue.ts         # Queue management with event subscriptions
│   └── useModImportWorkflow.ts     # Legacy (kept for compatibility)
├── services/
│   └── workflowService.ts          # IPC service
└── types/
    └── workflow.types.ts           # TypeScript types
```

## Key Design Decisions

### Why Simple Stateless Design?

1. **Easier to understand**: No complex routing conditions or node graphs
2. **Easier to implement**: Each workflow type is self-contained
3. **Easier to maintain**: Changes to one workflow don't affect others
4. **Easier to test**: Simple state machines are easy to test

### Why SQLite Repository?

- Workflows persist across application restarts
- Uses raw SQLite (following ModRepository pattern)
- Profile-scoped storage (each profile has isolated workflows)
- Simple CRUD operations with direct SQL commands

### Why JSON Context?

- Flexibility: Each workflow type defines its own context structure
- Simple: No need for separate step entities
- Type-safe: Deserialize to strongly-typed classes

## Adding a New Workflow Type

1. **Define Context Model** (`Modules/Workflow/Models/YourWorkflowContext.cs`):
   ```csharp
   public class YourWorkflowContext
   {
       public required string Step { get; set; }
       // Add workflow-specific fields
   }
   ```

2. **Create Handler** (`Modules/Workflow/Handlers/YourWorkflowHandler.cs`):
   ```csharp
   public class YourWorkflowHandler
   {
       public const string WorkflowType = "YOUR_TYPE";

       public async Task<WorkflowInfo> StartAsync(...)
       {
           // Create workflow with initial context
           // Start processing
       }
   }
   ```

3. **Register in DI** (`WorkflowServiceExtensions.cs`):
   ```csharp
   services.AddSingleton<YourWorkflowHandler>();
   ```

4. **Add IPC Routes** (`WorkflowFacade.cs`):
   ```csharp
   "START_YOUR_WORKFLOW" => await StartYourWorkflowAsync(request)
   ```

5. **Create Frontend Service** (`workflowService.ts`):
   ```typescript
   async startYourWorkflow(input: YourInput): Promise<WorkflowInfo> {
     return bridgeService.sendMessage({
       module: 'WORKFLOW',
       type: 'START_YOUR_WORKFLOW',
       payload: input,
     });
   }
   ```

6. **Create UI Components** as needed

## Migration from TaskQueue

The old TaskQueue system was over-engineered with:
- Complex node-based routing
- Predefined chain configurations
- Separate TaskInfo and TaskChainInfo entities
- Generic task processors

The new Workflow system simplifies to:
- Type-specific handlers
- Single WorkflowInfo entity
- Step-based state machines
- No routing configuration needed

**Migration Path**: Create new workflow handlers for each use case, delete old TaskQueue code.

## Pause/Resume System

### User-Initiated Pause

Workflows can be paused manually by the user:

```csharp
public async Task<WorkflowInfo> PauseAsync(string workflowId)
{
    // 1. Cancel running task (if exists)
    if (_cancellationTokens.TryRemove(workflowId, out var cts))
    {
        cts.Cancel();
        cts.Dispose();
    }

    // 2. Release concurrency slot
    _concurrencyManager.ReleaseSlot(workflowId);

    // 3. Update status to Paused
    workflow.Status = WorkflowStatus.Paused;
    await _workflowRepository.UpdateAsync(workflow);
}
```

### Resume from Paused State

Paused workflows can be resumed:

```csharp
public async Task<WorkflowInfo> ResumeFromCurrentStepAsync(string workflowId)
{
    // 1. Validate status (Pending, Processing, or Paused)
    if (workflow.Status != WorkflowStatus.Paused) { /* validate */ }

    // 2. Set to Pending
    workflow.Status = WorkflowStatus.Pending;

    // 3. Create new cancellation token
    var cts = new CancellationTokenSource();
    _cancellationTokens.TryAdd(workflow.Id, cts);

    // 4. Resume from current step
    await ProcessStepAsync(workflow, cts.Token);
}
```

### Post-Reboot Behavior

After application restart:
- All in-memory state (CancellationTokenSource) is lost
- Workflows with `Pending` or `Processing` status remain in database
- **No auto-resume** - user must manually restart queue
- "Start Queue" button appears to resume all stopped workflows

#### Start Queue Feature

The "Start Queue" button only appears when workflows are stuck after reboot:

**Detection Logic:**
1. Frontend checks `GET_ACTIVE_WORKFLOW_COUNT` on mount
2. Checks if any workflows have `Pending/Processing` status
3. Shows button ONLY when: `hasPendingWorkflows && activeWorkflowCount === 0`

**Resume All Stuck Workflows:**
```csharp
// Backend identifies and resumes ALL stuck workflows at once
public async Task<BatchOperationResult> ResumeAllStuckWorkflowsByTypeAsync(string type)
{
    // 1. Find all stuck workflows (Pending or Processing status)
    var stuckWorkflows = await _workflowRepository.GetByTypeAsync(type)
        .Where(w => w.Status == Pending || w.Status == Processing)
        .OrderBy(w => w.CreatedAt);

    // 2. Reset progress to beginning of current step for each
    // 3. Resume all workflows
    // 4. Concurrency manager queues them and processes N in parallel
}
```

**Progress Reset on Resume:**
When resuming a stuck workflow, progress is reset to the beginning of its current step:
- `ExtractMetadata` → 0%
- `CompressFolder` → 1% (after metadata extraction)
- `ImportMod` → 90% (after compression)

This ensures the UI shows accurate progress when restarting interrupted workflows.

## Performance Optimizations

### 1. Compression Speed vs Size Trade-off

```csharp
// Use Fast compression instead of Normal
CompressionLevel = CompressionLevel.Fast
```

**Impact**:
- 2-3x faster compression
- ~10-15% larger file size
- Minimal impact on mod files (textures/models already compressed)

### 2. Progress Event Throttling

```csharp
// Throttle to 2 events/sec (500ms) instead of 10 events/sec (100ms)
var eventThrottle = TimeSpan.FromMilliseconds(500);

// Report every 10% instead of 5%
var shouldReport = scaledProgress >= lastReportedProgress + 10 ||
                 scaledProgress >= 90 ||
                 (now - lastEventTime) >= eventThrottle;
```

**Impact**:
- 10 workflows × 2 events/sec = 20 IPC messages/sec (80% reduction from 100)
- Reduces IPC channel saturation
- Improves UI responsiveness

### 3. Direct Event Emission

```csharp
// Direct emit (no Task.Run wrapper)
_ = _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, payload);
```

**Impact**:
- Reduces thread pool allocation overhead
- Lower GC pressure
- Simpler code path

### Performance Comparison

**Before Optimization**:
- 100 IPC events/sec (10 workflows × 10 events/sec)
- Normal compression (high CPU)
- Task.Run for each event

**After Optimization**:
- 20 IPC events/sec (10 workflows × 2 events/sec)
- Fast compression (2-3x faster)
- Direct event emission

**Result**: Significantly improved UI responsiveness during heavy workflow processing.

## See Also

- [AI_GUIDE.md - Workflow System](../AI_GUIDE.md#workflow-system-simple-stateless-workflows)
- [EVENT_HUB_ARCHITECTURE.md](EVENT_HUB_ARCHITECTURE.md)
- [MODULE_ARCHITECTURE.md](MODULE_ARCHITECTURE.md)
