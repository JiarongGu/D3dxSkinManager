# TaskQueue Module Implementation Summary

**Date:** 2026-02-25
**Status:** ✅ Complete
**Related Documentation:** [features/TASK_QUEUE_SYSTEM.md](../features/TASK_QUEUE_SYSTEM.md)

---

## Overview

This document summarizes the implementation of the TaskQueue module, including the chain-phase workflow system and all critical fixes for real-time event updates.

---

## Features Implemented

### 1. Core Task Queue System

- ✅ Asynchronous task processing with sequential execution
- ✅ Real-time progress tracking via events
- ✅ Task cancellation support
- ✅ Profile-scoped queues
- ✅ Persistent task state

### 2. Chain-Phase Workflow System

- ✅ Multi-step task chains with correlation IDs
- ✅ Automatic task chaining (auto-continue)
- ✅ User-confirmed chaining (pause for input)
- ✅ Shared data passing between phases
- ✅ Phase tracking (current/total)

### 3. Task Types

- ✅ `mod_import` - Direct archive import (single-phase)
- ✅ `compress_folder` - Compress folder to temp (chain phase 1)
- ✅ `import_from_temp` - Import from temp with metadata (chain phase 2)

### 4. Event System

- ✅ Real-time progress updates (`TASK_PROGRESS`)
- ✅ Task lifecycle events (ADDED, STARTED, COMPLETED, FAILED, CANCELLED)
- ✅ Chain pause event (`TASK_AWAITING_CONFIRMATION`)
- ✅ Event forwarding from backend to frontend

---

## Architecture

### Backend Components

```
TaskQueue Module
├── TaskQueueService          - Queue orchestration
├── TaskQueueFacade           - IPC routing
├── EventProgressReporter     - Progress events
├── ITaskProcessor<TIn, TOut> - Processor interface
├── Task Processors
│   ├── CompressFolderTaskProcessor
│   ├── ImportFromTempTaskProcessor
│   └── ModImportTaskProcessor
└── Models
    ├── TaskInfo
    ├── TaskChainContext
    ├── TaskStatus
    └── Task-specific input/output models
```

### Frontend Components

```
taskQueue Module
├── services/taskQueueService.ts - Backend communication
├── types/task.types.ts          - TypeScript definitions
└── components/
    └── TaskQueueView.tsx        - UI component
```

---

## Critical Issues Fixed

### Issue 1: Events Not Reaching Frontend

**Root Cause:** TaskQueue events were not registered in `CoreEvents.All` array, so `EventBusIpcBridge` didn't subscribe to them.

**Solution:** Added all TaskQueue events to `CoreEvents.All`

**Files Changed:**
- `Modules/Core/Event/CoreEvents.cs` - Added TASK_* constants and included in `All` array
- `Modules/Core/Event/CoreEvents.cs` - Also added DropZone events

**Code:**
```csharp
public static readonly string[] All = new[]
{
    APPLICATION_STARTED,
    APPLICATION_SHUTDOWN,
    // ... existing events ...

    // TaskQueue events
    TASK_ADDED,
    TASK_STARTED,
    TASK_PROGRESS,
    TASK_COMPLETED,
    TASK_FAILED,
    TASK_CANCELLED,
    TASK_REMOVED,
    TASK_AWAITING_CONFIRMATION,

    // DropZone events
    DROP_ZONE_CLICK,
    DROP_ZONE_DRAG_ENTER,
    DROP_ZONE_DRAG_LEAVE,
    DROP_ZONE_FILE_DROP,
    DROP_ZONE_MOUSE_ENTER,
    DROP_ZONE_MOUSE_LEAVE,
};
```

### Issue 2: Frontend State Not Updating

**Root Cause:** Backend wraps event data in `{ eventName, data }` but frontend was passing wrapped data to eventBus.

**Solution:** Unwrap data in `bridgeService.ts` before emitting

**File Changed:** `src/shared/services/bridgeService.ts`

**Code:**
```typescript
} else if (parsed.category === "notification") {
  // Backend wraps data in { eventName, data }, unwrap it
  const actualData = parsed.data?.data ?? parsed.data;
  const eventName = parsed.data?.eventName ?? parsed.eventName;

  eventBus.emit({
    type: parsed.type as EventType,
    eventName: eventName,
    data: actualData,  // ✅ Unwrapped
  });
}
```

### Issue 3: Inconsistent Event Subscription API

**Root Cause:** Frontend had both `.subscribe()` and `.on()` methods, causing confusion.

**Solution:** Unified to single `.subscribe()` method that returns cleanup function

**File Changed:** `src/shared/services/eventBus.ts`

**Code:**
```typescript
// Removed .on() method
// Simplified .subscribe() to return cleanup function directly

subscribe<T = unknown>(
  eventType: EventType | string,
  handler: EventHandler<T>
): () => void {
  // ... registration logic ...

  // Return cleanup function for useEffect
  return () => {
    // ... cleanup logic ...
  };
}
```

**All components updated to use:**
```typescript
const unsubscribe = eventBus.subscribe(EventType.TaskCompleted, handler);
return unsubscribe; // In useEffect cleanup
```

### Issue 4: Missing i18n Keys

**Solution:** Added missing translation keys

**Files Changed:**
- `Languages/en.json`
- `Languages/cn.json`

**Keys Added:**
- `settings.global.resetWindowStateTooltip`
- `importQueue.status.awaitingConfirmation`

### Issue 5: Database Foreign Key Constraints

**Solution:** Removed all foreign key constraints per user request

**Files Changed:**
- `Modules/Mods/Services/ModRepository.cs` - Removed FK constraints
- `Modules/Mods/Services/ClassificationRepository.cs` - Now owns Classifications table

**Added:** `CreatedAt` and `UpdatedAt` timestamps to:
- `Mods` table
- `Classifications` table
- `ModInfo` model
- `ClassificationNode` model

### Issue 6: File Dialog Crashes (0x80000003)

**Solution:** All dialogs now use dedicated STA thread to avoid WebView2 conflicts

**File Changed:** `Modules/System/Services/SystemFileDialogService.cs`

**Code:**
```csharp
public async Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null)
{
    var initialPath = await GetInitialPathAsync(options).ConfigureAwait(false);

    // ALWAYS use RunInStaThread to avoid WebView2 threading conflicts
    return await RunInStaThread(() => ShowFolderDialog(options, initialPath));
}
```

### Issue 7: Empty FilePath in Backend

**Root Cause:** `PayloadHelper` wasn't using camelCase deserialization

**Solution:** Added JsonSerializerOptions with camelCase to PayloadHelper

**File Changed:** `Modules/Core/Helpers/PayloadHelper.cs`

**Code:**
```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

public static T GetRequiredValue<T>(JsonElement payload, string key)
{
    // ... get value ...
    return JsonSerializer.Deserialize<T>(value.GetRawText(), JsonOptions)!;
}
```

---

## Logging Strategy

### Backend Logging Levels

**Event System Logs:** Changed from DEBUG to VERBOSE

- `EventEmitter.cs` - Event emission
- `EventBus.cs` - Handler invocation
- `EventBusIpcBridge.cs` - Frontend forwarding

**Rationale:** Event logs are high-frequency and should only appear when explicitly debugging event flow.

### Frontend Logging

**Removed all debug logs** from:
- `bridgeService.ts` - Notification reception
- `eventBus.ts` - Subscribe/emit operations

**Kept:** Error logs in event handlers

---

## Key Architectural Decisions

### 1. Profile-Scoped Services

TaskQueue is registered as profile-scoped service via `ProfileServiceRouter`, ensuring each profile has isolated task queue.

### 2. Chain-Phase Over Two-Phase

Initial design was "two-phase" (prepare + execute). Changed to "chain-phase" to support variable-length workflows:

- Folder import: 2+ phases (compress → metadata → import → ...)
- Archive import: 1 phase (import)
- Future: Could have 3+ phase workflows

### 3. Event-Driven Updates

Frontend uses event subscriptions instead of polling for:
- Real-time progress updates
- Task state changes
- Chain pause notifications

### 4. Temporary File Management

Temp files stored in profile-specific temp directory:
```
{profilePath}/temp/mod_import_{guid}_{name}.zip
```

Cleanup handled automatically in `ImportFromTempTaskProcessor` finally block.

---

## Testing Checklist

- [x] Single-phase task (archive import)
- [x] Two-phase chain (folder compress + import)
- [x] Auto-continue chain
- [x] User-confirmed chain (pause/resume)
- [x] Task cancellation
- [x] Progress event updates
- [x] Multiple tasks in queue
- [x] Task failure handling
- [x] Temp file cleanup
- [x] Profile switching with active tasks
- [x] Frontend state synchronization
- [x] i18n translations (en/cn)

---

## Future Enhancements

### Potential Features

1. **Task Priority** - High/normal/low priority queue
2. **Parallel Processing** - Multiple tasks simultaneously (with thread pool)
3. **Task Dependencies** - Task B waits for Task A completion
4. **Retry Logic** - Auto-retry failed tasks with backoff
5. **Task Scheduling** - Delayed execution, cron-like scheduling
6. **Progress Persistence** - Resume tasks after app restart
7. **Task History** - Completed task log with filters
8. **Batch Operations** - Group operations (import multiple folders)

### Chain Workflow Ideas

- **Mod Update Chain**: Download → Backup → Extract → Apply → Verify
- **Migration Chain**: Detect → Analyze → Confirm → Migrate → Validate
- **Cleanup Chain**: Scan → Report → Confirm → Delete → Compact

---

## Files Modified/Created

### Backend Files Created

```
Modules/TaskQueue/
├── Models/
│   ├── CompressFolderTaskInput.cs
│   ├── CompressFolderTaskOutput.cs
│   ├── ImportFromTempTaskInput.cs
│   └── TaskChainContext.cs (ContinueChainRequest)
├── Services/
│   ├── CompressFolderTaskProcessor.cs
│   └── ImportFromTempTaskProcessor.cs
└── (Existing files modified)
```

### Backend Files Modified

```
Modules/Core/
├── Event/
│   ├── CoreEvents.cs (Added TaskQueue + DropZone events)
│   ├── EventEmitter.cs (Added logging, made logger optional)
│   └── EventBus.cs (Added logging)
├── Helpers/
│   └── PayloadHelper.cs (Added camelCase JSON options)

Modules/Mods/
├── Models/
│   ├── ModInfo.cs (Added CreatedAt/UpdatedAt)
│   └── ClassificationNode.cs (Added CreatedAt/UpdatedAt)
├── Services/
│   ├── ModRepository.cs (Removed FKs, moved Classifications)
│   └── ClassificationRepository.cs (Added table creation)

Modules/System/Services/
└── SystemFileDialogService.cs (All dialogs use STA thread)

Modules/TaskQueue/
├── Models/
│   ├── TaskInfo.cs (Added correlation + chain context)
│   └── TaskStatus.cs (Added AwaitingConfirmation)
├── Services/
│   ├── ITaskQueueService.cs (Updated signatures)
│   ├── TaskQueueService.cs (Chain logic, new processors)
│   └── ModImportTaskProcessor.cs (Refactored for archives only)
├── TaskQueueEvents.cs (Added TASK_AWAITING_CONFIRMATION)
├── TaskQueueFacade.cs (Chain handlers)
└── TaskQueueServiceExtensions.cs (Registered new processors)

Composition/
└── EventBusIpcBridge.cs (Changed to Verbose logging)

Languages/
├── en.json (Added missing keys)
└── cn.json (Added missing keys)
```

### Frontend Files Modified

```
src/shared/services/
├── bridgeService.ts (Data unwrapping)
└── eventBus.ts (Unified API, removed .on())

src/shared/hooks/
├── useDropZone.ts (Use .subscribe())
├── useEventSubscription.ts (Fixed types)

src/modules/mods/
├── ModsProvider.tsx (Use .subscribe())
└── components/ModManagementScreen/
    └── TaskQueueView.tsx (Event-driven updates)
```

### Documentation Created

```
docs/
├── features/
│   └── TASK_QUEUE_SYSTEM.md (Comprehensive guide)
├── implementation/
│   └── TASK_QUEUE_IMPLEMENTATION.md (This file)
├── keywords/
│   └── BACKEND.md (Added TaskQueue section)
└── AI_GUIDE.md (Updated event patterns + warnings)
```

---

## References

- **Feature Documentation:** [features/TASK_QUEUE_SYSTEM.md](../features/TASK_QUEUE_SYSTEM.md)
- **Backend Keywords:** [keywords/BACKEND.md](../keywords/BACKEND.md#taskqueue-module)
- **AI Guide:** [AI_GUIDE.md](../AI_GUIDE.md#ipc-event-notifications)

---

## Lessons Learned

1. **Always register new events in CoreEvents.All** - This is the #1 cause of "events not working"
2. **Data unwrapping is critical** - Backend wraps, frontend must unwrap
3. **Unified API reduces confusion** - Single .subscribe() method is clearer than .subscribe() + .on()
4. **Verbose logging for high-frequency events** - Keeps logs clean in production
5. **Chain workflows are more flexible than two-phase** - Variable-length chains support diverse workflows
6. **Profile-scoped services prevent cross-contamination** - Each profile's tasks are isolated
7. **Temp file cleanup in finally blocks** - Ensures cleanup even on errors
8. **Event-driven updates > polling** - Real-time UX with minimal overhead

---

**Implementation Status:** ✅ Production Ready
**Documentation Status:** ✅ Complete
**Testing Status:** ✅ Verified

