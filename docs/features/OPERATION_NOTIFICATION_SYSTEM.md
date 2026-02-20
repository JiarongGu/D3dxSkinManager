# Operation Notification System

**Version:** 1.0
**Last Updated:** 2026-02-21
**Status:** Implemented
**Feature Type:** Infrastructure / Real-time Progress Tracking

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Backend Components](#backend-components)
4. [Frontend Components](#frontend-components)
5. [Usage Guide](#usage-guide)
6. [API Reference](#api-reference)
7. [Examples](#examples)
8. [Troubleshooting](#troubleshooting)

---

## Overview

### What It Does

The Operation Notification System provides real-time progress tracking and monitoring for long-running backend operations. It enables:

- **Real-time Progress Updates**: Backend operations report progress to the frontend via push notifications
- **Status Bar Integration**: Current operation displayed in the footer status bar
- **Operation Monitor Screen**: Full-screen monitor showing active, completed, and failed operations
- **Non-intrusive Reporting**: Operations can optionally report progress without tight coupling

### Key Features

- Backend → Frontend push notification architecture
- IProgressReporter pattern for operations to report progress
- Global operation state management with React Context
- Keyboard shortcut (Ctrl+Shift+O) to open operation monitor
- Automatic history management (last 50 completed/failed operations)
- Operation metadata support for custom data

### Use Cases

- Mod loading operations with multi-step progress
- Batch processing tasks
- File extraction and archive operations
- Database migrations
- Any long-running backend task that benefits from user feedback

---

## Architecture

### High-Level Flow

```
Backend Operation
    ↓
IOperationNotificationService.CreateOperation()
    ↓
IProgressReporter.ReportProgressAsync()
    ↓
EventHandler<OperationNotification> (OperationNotificationReceived)
    ↓
Program.cs: OnOperationNotificationReceived()
    ↓
Photino.SendWebMessage() (IPC Push)
    ↓
photinoService.subscribeToOperationNotifications()
    ↓
OperationContext (React Context + useReducer)
    ↓
UI Components (AppStatusBar, OperationMonitorScreen)
```

### Communication Protocol

**Backend → Frontend IPC Message:**

```json
{
  "type": "OPERATION_NOTIFICATION",
  "notification": {
    "type": "OperationStarted" | "ProgressUpdate" | "OperationCompleted" | "OperationFailed" | "OperationCancelled",
    "operation": {
      "operationId": "guid",
      "operationName": "Loading: Mod Name",
      "status": "Running" | "Completed" | "Failed" | "Cancelled",
      "percentComplete": 0-100,
      "currentStep": "Extracting files...",
      "startedAt": "ISO 8601 date string",
      "completedAt": "ISO 8601 date string or null",
      "errorMessage": "Error details if failed",
      "metadata": { /* custom data */ }
    },
    "timestamp": "ISO 8601 date string"
  }
}
```

---

## Backend Components

### 1. OperationProgress.cs

**Location:** `D3dxSkinManager/Modules/Core/Models/OperationProgress.cs`

**Purpose:** Data models for operation tracking

**Key Types:**

```csharp
public enum OperationStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public class OperationProgress
{
    public string OperationId { get; set; }
    public string OperationName { get; set; }
    public OperationStatus Status { get; set; }
    public int PercentComplete { get; set; }
    public string? CurrentStep { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Metadata { get; set; }
}

public enum OperationNotificationType
{
    OperationStarted,
    ProgressUpdate,
    OperationCompleted,
    OperationFailed,
    OperationCancelled
}

public class OperationNotification
{
    public OperationNotificationType Type { get; set; }
    public OperationProgress Operation { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 2. IProgressReporter.cs

**Location:** `D3dxSkinManager/Modules/Core/Services/IProgressReporter.cs`

**Purpose:** Interface for operations to report progress

**Interface:**

```csharp
public interface IProgressReporter
{
    Task ReportProgressAsync(int percentComplete, string? currentStep = null);
    Task ReportCompletionAsync();
    Task ReportFailureAsync(string errorMessage);
    Task ReportCancellationAsync();
    bool IsCancelled { get; }
}
```

**Null Object Pattern:**

```csharp
public class NullProgressReporter : IProgressReporter
{
    public static readonly NullProgressReporter Instance = new();
    // No-op implementation for operations that don't need progress reporting
}
```

### 3. OperationNotificationService.cs

**Location:** `D3dxSkinManager/Modules/Core/Services/OperationNotificationService.cs`

**Purpose:** Manages active operations and emits notifications

**Key Features:**

- Thread-safe operation tracking using `ConcurrentDictionary`
- Event emission for frontend push notifications
- Nested `OperationProgressReporter` class implementing `IProgressReporter`
- Automatic operation lifecycle management

**Interface:**

```csharp
public interface IOperationNotificationService
{
    IProgressReporter CreateOperation(string operationName, object? metadata = null);
    Task CancelOperationAsync(string operationId);
    OperationProgress[] GetActiveOperations();
    event EventHandler<OperationNotification>? OperationNotificationReceived;
}
```

**Usage:**

```csharp
// Create an operation
var progressReporter = _operationService.CreateOperation(
    "Loading: Mod Name",
    metadata: new { sha = "abc123" }
);

// Report progress
await progressReporter.ReportProgressAsync(50, "Extracting files...");

// Report completion
await progressReporter.ReportCompletionAsync();
```

### 4. Program.cs Integration

**Location:** `D3dxSkinManager/Program.cs`

**Key Changes:**

```csharp
private static IOperationNotificationService? _operationNotificationService;

// In initialization:
_operationNotificationService = globalServices.GetRequiredService<IOperationNotificationService>();
_operationNotificationService.OperationNotificationReceived += OnOperationNotificationReceived;

// Event handler:
private static void OnOperationNotificationReceived(object? sender, OperationNotification notification)
{
    var pushMessage = new
    {
        type = "OPERATION_NOTIFICATION",
        notification = new
        {
            type = notification.Type.ToString(),
            operation = new
            {
                operationId = notification.Operation.OperationId,
                operationName = notification.Operation.OperationName,
                status = notification.Operation.Status.ToString(),
                percentComplete = notification.Operation.PercentComplete,
                currentStep = notification.Operation.CurrentStep,
                startedAt = notification.Operation.StartedAt.ToString("o"),
                completedAt = notification.Operation.CompletedAt?.ToString("o"),
                errorMessage = notification.Operation.ErrorMessage,
                metadata = notification.Operation.Metadata
            },
            timestamp = notification.Timestamp.ToString("o")
        }
    };

    _mainWindow.SendWebMessage(JsonSerializer.Serialize(pushMessage, JsonOptions));
}
```

### 5. Service Registration

**Location:** `D3dxSkinManager/Modules/Core/CoreServiceExtensions.cs:46`

```csharp
// Operation notification service for progress reporting and operation monitoring
services.AddSingleton<IOperationNotificationService, OperationNotificationService>();
```

---

## Frontend Components

### 1. operation.types.ts

**Location:** `D3dxSkinManager.Client/src/shared/types/operation.types.ts`

**Purpose:** TypeScript interfaces matching backend models

**Key Types:**

```typescript
export type OperationStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export type OperationNotificationType =
  | 'OperationStarted'
  | 'ProgressUpdate'
  | 'OperationCompleted'
  | 'OperationFailed'
  | 'OperationCancelled';

export interface OperationProgress {
  operationId: string;
  operationName: string;
  status: OperationStatus;
  percentComplete: number;
  currentStep?: string;
  startedAt: Date;
  completedAt?: Date;
  errorMessage?: string;
  metadata?: unknown;
}

export interface OperationNotificationMessage {
  type: 'OPERATION_NOTIFICATION';
  notification: {
    type: string;
    operation: {
      operationId: string;
      operationName: string;
      status: string;
      percentComplete: number;
      currentStep?: string;
      startedAt: string; // ISO 8601 string from IPC
      completedAt?: string;
      errorMessage?: string;
      metadata?: unknown;
    };
    timestamp: string;
  };
}
```

### 2. photinoService.ts

**Location:** `D3dxSkinManager.Client/src/shared/services/photinoService.ts`

**Key Additions:**

```typescript
private operationNotificationHandlers: Array<
  (notification: OperationNotificationMessage['notification']) => void
> = [];

subscribeToOperationNotifications(
  handler: (notification: OperationNotificationMessage['notification']) => void
): () => void {
  this.operationNotificationHandlers.push(handler);
  return () => {
    const index = this.operationNotificationHandlers.indexOf(handler);
    if (index > -1) {
      this.operationNotificationHandlers.splice(index, 1);
    }
  };
}

// In receiveMessage:
if (parsed.type === 'OPERATION_NOTIFICATION') {
  const operationNotification = parsed as OperationNotificationMessage;
  this.operationNotificationHandlers.forEach(handler =>
    handler(operationNotification.notification)
  );
  return;
}
```

### 3. OperationContext.tsx

**Location:** `D3dxSkinManager.Client/src/shared/context/OperationContext.tsx`

**Purpose:** Global state management for operations using React Context and useReducer

**State Structure:**

```typescript
interface OperationState {
  activeOperations: OperationProgress[];
  completedOperations: OperationProgress[]; // Last 50
  failedOperations: OperationProgress[]; // Last 50
  currentOperation: OperationProgress | null; // Most recent for status bar
}
```

**Key Features:**

- Subscribes to operation notifications from `photinoService`
- Converts date strings to Date objects before state updates
- Maintains operation history (last 50 completed/failed)
- Provides actions for clearing completed/failed operations
- Automatic currentOperation tracking for status bar

**Usage:**

```typescript
import { useOperation } from 'shared/context/OperationContext';

const { state, actions } = useOperation();
const { currentOperation, activeOperations, completedOperations, failedOperations } = state;
```

**Critical Implementation Detail:**

Date strings from IPC must be converted to Date objects before dispatching to state:

```typescript
const operation: OperationProgress = {
  ...notification.operation,
  startedAt: new Date(notification.operation.startedAt),
  completedAt: notification.operation.completedAt
    ? new Date(notification.operation.completedAt)
    : undefined,
} as OperationProgress;
```

### 4. AppStatusBar.tsx

**Location:** `D3dxSkinManager.Client/src/modules/core/components/layout/AppStatusBar.tsx`

**Key Additions:**

```typescript
interface AppStatusBarProps {
  // ... existing props
  operationName?: string;
  activeOperationCount?: number;
  onProgressClick?: () => void;
}

// In render:
{operationName && activeOperationCount > 0 ? (
  <Space size="small" style={{ cursor: 'pointer' }} onClick={onProgressClick}>
    <LoadingOutlined />
    <span>{operationName}</span>
    {activeOperationCount > 1 && (
      <Tag color="blue">+{activeOperationCount - 1} more</Tag>
    )}
  </Space>
) : statusMessage ? (
  <span>{statusMessage}</span>
) : null}
```

### 5. OperationMonitorScreen.tsx

**Location:** `D3dxSkinManager.Client/src/shared/components/operation/OperationMonitorScreen.tsx`

**Purpose:** Full-screen operation monitor with tabs for active/completed/failed operations

**Features:**

- Three tabs: Active, Completed, Failed
- Real-time progress bars for active operations
- Duration display for all operations
- Error message display for failed operations
- Clear All buttons for completed/failed tabs
- Metadata display (for debugging)

**Usage:**

```typescript
import OperationMonitorScreen from 'shared/components/operation/OperationMonitorScreen';

const handleOpen = () => {
  const screenId = openScreen({
    title: 'Operation Monitor',
    content: <OperationMonitorScreen onClose={() => closeScreen(screenId)} />,
  });
};
```

### 6. App.tsx Integration

**Location:** `D3dxSkinManager.Client/src/App.tsx`

**Key Changes:**

```typescript
import { OperationProvider, useOperation } from './shared/context/OperationContext';
import OperationMonitorScreen from './shared/components/operation/OperationMonitorScreen';

// In AppContent:
const { state: operationState } = useOperation();
const { currentOperation, activeOperations } = operationState;

const handleOperationMonitorClick = () => {
  const screenId = openScreen({
    title: 'Operation Monitor',
    content: <OperationMonitorScreen onClose={() => closeScreen(screenId)} />,
  });
};

// Keyboard shortcut (Ctrl+Shift+O):
keyboardManager.register('operation-monitor', {
  key: 'o',
  ctrlKey: true,
  shiftKey: true,
  description: 'Open operation monitor',
  callback: handleOperationMonitorClick,
});

// Status bar props:
<AppStatusBar
  operationName={currentOperation?.operationName}
  activeOperationCount={activeOperations.length}
  onProgressClick={handleOperationMonitorClick}
  // ... other props
/>

// Provider wrapping (outer layer):
<OperationProvider>
  <AppInitializer>
    <ModsProvider>
      <AppContent />
    </ModsProvider>
  </AppInitializer>
</OperationProvider>
```

---

## Usage Guide

### Adding Progress Tracking to an Operation

#### Step 1: Update Service Method Signature

Add optional `IProgressReporter?` parameter:

```csharp
public async Task<bool> LoadAsync(string sha, IProgressReporter? progressReporter = null)
{
    progressReporter ??= NullProgressReporter.Instance;
    // ... rest of method
}
```

#### Step 2: Report Progress at Key Steps

```csharp
await progressReporter.ReportProgressAsync(0, "Checking archive...");
// ... do work
await progressReporter.ReportProgressAsync(20, "Extracting files...");
// ... do work
await progressReporter.ReportProgressAsync(80, "Updating metadata...");
// ... do work
await progressReporter.ReportProgressAsync(100, "Complete");
```

#### Step 3: Handle Success/Failure

```csharp
try
{
    // ... operation logic with progress reporting
    await progressReporter.ReportCompletionAsync();
    return true;
}
catch (Exception ex)
{
    await progressReporter.ReportFailureAsync(ex.Message);
    throw;
}
```

#### Step 4: Create Operation in Facade

```csharp
public async Task<bool> LoadModAsync(string sha)
{
    var mod = await _repository.GetByIdAsync(sha);
    var modName = mod?.Name ?? $"Mod {sha.Substring(0, 8)}";

    var progressReporter = _operationService.CreateOperation(
        $"Loading: {modName}",
        metadata: new { sha }
    );

    var success = await _fileService.LoadAsync(sha, progressReporter);

    // ReportCompletionAsync or ReportFailureAsync called inside LoadAsync

    return success;
}
```

### Frontend: Displaying Operation Status

#### Using Current Operation in Status Bar

Already implemented in [AppStatusBar.tsx:AppStatusBar.tsx](D3dxSkinManager.Client/src/modules/core/components/layout/AppStatusBar.tsx)

#### Accessing Operation State

```typescript
import { useOperation } from 'shared/context/OperationContext';

const MyComponent = () => {
  const { state, actions } = useOperation();
  const { activeOperations, completedOperations, failedOperations, currentOperation } = state;

  // Use operation data
  return (
    <div>
      <p>Active: {activeOperations.length}</p>
      <p>Current: {currentOperation?.operationName}</p>
    </div>
  );
};
```

---

## API Reference

### Backend

#### IOperationNotificationService

```csharp
public interface IOperationNotificationService
{
    // Create a new operation and return a progress reporter
    IProgressReporter CreateOperation(string operationName, object? metadata = null);

    // Cancel an active operation
    Task CancelOperationAsync(string operationId);

    // Get all active operations
    OperationProgress[] GetActiveOperations();

    // Event fired when any operation state changes
    event EventHandler<OperationNotification>? OperationNotificationReceived;
}
```

#### IProgressReporter

```csharp
public interface IProgressReporter
{
    // Report progress (0-100) with optional step description
    Task ReportProgressAsync(int percentComplete, string? currentStep = null);

    // Mark operation as completed successfully
    Task ReportCompletionAsync();

    // Mark operation as failed with error message
    Task ReportFailureAsync(string errorMessage);

    // Mark operation as cancelled
    Task ReportCancellationAsync();

    // Check if operation has been cancelled (for graceful shutdown)
    bool IsCancelled { get; }
}
```

### Frontend

#### useOperation Hook

```typescript
interface OperationContextValue {
  state: {
    activeOperations: OperationProgress[];
    completedOperations: OperationProgress[];
    failedOperations: OperationProgress[];
    currentOperation: OperationProgress | null;
  };
  actions: {
    clearCompleted: () => void;
    clearFailed: () => void;
  };
}

const { state, actions } = useOperation();
```

#### photinoService

```typescript
// Subscribe to operation notifications
const unsubscribe = photinoService.subscribeToOperationNotifications(
  (notification) => {
    console.log('Operation notification:', notification);
  }
);

// Unsubscribe when done
unsubscribe();
```

---

## Examples

### Example 1: Simple Operation with Progress

```csharp
public async Task<bool> ProcessFilesAsync(IProgressReporter? progressReporter = null)
{
    progressReporter ??= NullProgressReporter.Instance;

    try
    {
        await progressReporter.ReportProgressAsync(0, "Starting...");

        // Step 1
        await DoStep1();
        await progressReporter.ReportProgressAsync(33, "Step 1 complete");

        // Step 2
        await DoStep2();
        await progressReporter.ReportProgressAsync(66, "Step 2 complete");

        // Step 3
        await DoStep3();
        await progressReporter.ReportProgressAsync(100, "All steps complete");

        await progressReporter.ReportCompletionAsync();
        return true;
    }
    catch (Exception ex)
    {
        await progressReporter.ReportFailureAsync(ex.Message);
        throw;
    }
}
```

### Example 2: Operation with Metadata

```csharp
var progressReporter = _operationService.CreateOperation(
    "Batch Processing",
    metadata: new
    {
        totalFiles = 100,
        batchId = "batch-123",
        startTime = DateTime.UtcNow
    }
);

await ProcessBatchAsync(progressReporter);
```

### Example 3: Frontend Custom Operation Display

```typescript
import { useOperation } from 'shared/context/OperationContext';
import { Progress, List } from 'antd';

const CustomOperationList = () => {
  const { state } = useOperation();

  return (
    <List
      dataSource={state.activeOperations}
      renderItem={(op) => (
        <List.Item>
          <div style={{ width: '100%' }}>
            <div>{op.operationName}</div>
            <Progress percent={op.percentComplete} />
            {op.currentStep && <small>{op.currentStep}</small>}
          </div>
        </List.Item>
      )}
    />
  );
};
```

---

## Troubleshooting

### Issue: Operation notifications not appearing

**Symptoms:** Backend operations complete but no progress shown in frontend

**Solutions:**

1. **Check OperationProvider is wrapped around App:**
   ```typescript
   <OperationProvider>
     <AppContent />
   </OperationProvider>
   ```

2. **Verify event subscription in Program.cs:**
   ```csharp
   _operationNotificationService.OperationNotificationReceived += OnOperationNotificationReceived;
   ```

3. **Check browser console for IPC errors:**
   - Open DevTools (F12)
   - Look for `[OperationContext] Received notification:` logs
   - Verify notification structure matches expected format

### Issue: TypeScript errors with date types

**Symptoms:** `Type 'string' is not assignable to type 'Date'`

**Solution:** Ensure date conversion in OperationContext:

```typescript
const operation: OperationProgress = {
  ...notification.operation,
  startedAt: new Date(notification.operation.startedAt),
  completedAt: notification.operation.completedAt
    ? new Date(notification.operation.completedAt)
    : undefined,
} as OperationProgress;
```

### Issue: Operation stuck in "Running" state

**Symptoms:** Operations never complete, always show as active

**Solutions:**

1. **Ensure ReportCompletionAsync or ReportFailureAsync is called:**
   ```csharp
   try
   {
     // ... operation logic
     await progressReporter.ReportCompletionAsync(); // Must call this!
   }
   catch (Exception ex)
   {
     await progressReporter.ReportFailureAsync(ex.Message); // Or this!
   }
   ```

2. **Check for exceptions that prevent completion:**
   - Add logging around completion calls
   - Verify exception handling is correct

### Issue: Too many completed operations in memory

**Symptoms:** Performance degradation over time

**Solution:** History is already limited to 50 entries per category (completed/failed) in OperationContext:

```typescript
const MAX_HISTORY = 50;
const newCompleted = [action.payload, ...state.completedOperations].slice(0, MAX_HISTORY);
```

Use "Clear All" buttons to manually clear history if needed.

### Issue: Progress percentage not updating

**Symptoms:** Progress bar stuck at 0% or doesn't update smoothly

**Solutions:**

1. **Ensure percentComplete is between 0-100:**
   ```csharp
   await progressReporter.ReportProgressAsync(50, "Half done"); // Not 0.5!
   ```

2. **Check for race conditions in async operations:**
   - Operations must report progress sequentially
   - Use `await` for all progress reports

3. **Verify PROGRESS_UPDATE dispatch in OperationContext:**
   ```typescript
   case 'ProgressUpdate':
     dispatch({ type: 'PROGRESS_UPDATE', payload: operation });
     break;
   ```

---

## Related Documentation

- [CURRENT_ARCHITECTURE.md](../architecture/CURRENT_ARCHITECTURE.md) - Overall system architecture
- [DOMAIN_DESIGN.md](../architecture/DOMAIN_DESIGN.md) - Service layer design principles
- [FRONTEND_CONTEXT_ARCHITECTURE.md](../architecture/FRONTEND_CONTEXT_ARCHITECTURE.md) - React Context patterns

---

**Last Updated:** 2026-02-21
**Maintained By:** AI Assistant
**Status:** Complete and tested
