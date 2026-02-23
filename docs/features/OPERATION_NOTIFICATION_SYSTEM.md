# Operation Notification System

**Last Updated:** 2026-02-23
**Status:** Implemented
**Purpose:** Real-time progress tracking for long-running operations

---

## Architecture

```
Backend Operation → IProgressReporter → EventHandler → IPC Push → React Context → UI
```

**IPC Message Format:**
```json
{
  "type": "OPERATION_NOTIFICATION",
  "notification": {
    "type": "OperationStarted|ProgressUpdate|OperationCompleted|OperationFailed",
    "operation": {
      "operationId": "guid",
      "operationName": "Loading: Mod Name",
      "status": "Running|Completed|Failed|Cancelled",
      "percentComplete": 0-100,
      "currentStep": "Extracting files...",
      "errorMessage": "Error if failed",
      "metadata": {}
    }
  }
}
```

---

## Backend Implementation

### 1. Add IProgressReporter to Facade

```csharp
// In ModFacade.cs
public class ModFacade : IModFacade
{
    private readonly IProgressReporter _progressReporter;

    public ModFacade(IModService modService, IProgressReporter progressReporter)
    {
        _modService = modService;
        _progressReporter = progressReporter;
    }
}
```

### 2. Report Progress in Operations

```csharp
public async Task<MessageResponse> LoadModAsync(string sha, string profileId)
{
    var operationId = Guid.NewGuid().ToString();

    try
    {
        // Create operation
        _progressReporter.CreateOperation(operationId, $"Loading mod: {sha}", OperationType.ModLoad);

        // Report progress at key steps
        _progressReporter.UpdateProgress(operationId, 25, "Validating mod...");
        await ValidateModAsync(sha);

        _progressReporter.UpdateProgress(operationId, 50, "Extracting files...");
        await ExtractModAsync(sha);

        _progressReporter.UpdateProgress(operationId, 75, "Updating database...");
        await UpdateDatabaseAsync(sha);

        // Complete
        _progressReporter.CompleteOperation(operationId, "Mod loaded successfully");
        return MessageResponse.Success("Mod loaded");
    }
    catch (Exception ex)
    {
        _progressReporter.FailOperation(operationId, ex.Message);
        return MessageResponse.Error($"Failed to load mod: {ex.Message}");
    }
}
```

### 3. Service Registration

```csharp
// In CoreServiceExtensions.cs
services.AddSingleton<IOperationNotificationService, OperationNotificationService>();
services.AddTransient<IProgressReporter, ProgressReporter>();
```

---

## Frontend Implementation

### 1. Operation Context

```typescript
// src/shared/context/OperationContext.tsx
interface OperationContextValue {
  activeOperations: OperationProgress[];
  completedOperations: OperationProgress[];
  failedOperations: OperationProgress[];
  cancelOperation: (id: string) => void;
  clearHistory: () => void;
}

export const OperationProvider: FC<{ children: ReactNode }> = ({ children }) => {
  const [state, dispatch] = useReducer(operationReducer, initialState);

  useEffect(() => {
    // Subscribe to backend notifications
    const unsubscribe = bridgeService.subscribeToOperationNotifications((notification) => {
      dispatch({ type: 'HANDLE_NOTIFICATION', payload: notification });
    });

    return unsubscribe;
  }, []);

  return (
    <OperationContext.Provider value={{ ...state, dispatch }}>
      {children}
    </OperationContext.Provider>
  );
};
```

### 2. Status Bar Integration

```tsx
// src/shared/components/AppStatusBar.tsx
export const AppStatusBar: FC = () => {
  const { activeOperations } = useOperation();
  const currentOp = activeOperations[0]; // Show first active

  if (!currentOp) return null;

  return (
    <div className="status-bar" onClick={openOperationMonitor}>
      <Progress percent={currentOp.percentComplete} size="small" />
      <span>{currentOp.currentStep}</span>
    </div>
  );
};
```

### 3. Operation Monitor

```tsx
// src/shared/components/OperationMonitorScreen.tsx
export const OperationMonitorScreen: FC<{ visible: boolean; onClose: () => void }> = ({ visible, onClose }) => {
  const { activeOperations, completedOperations, failedOperations } = useOperation();

  return (
    <Modal
      open={visible}
      onCancel={onClose}
      width="80%"
      title="Operations Monitor (Ctrl+Shift+O)"
    >
      <Tabs defaultActiveKey="active">
        <TabPane tab={`Active (${activeOperations.length})`} key="active">
          {activeOperations.map(op => (
            <OperationCard key={op.operationId} operation={op} />
          ))}
        </TabPane>
        <TabPane tab={`Completed (${completedOperations.length})`} key="completed">
          {/* List completed operations */}
        </TabPane>
        <TabPane tab={`Failed (${failedOperations.length})`} key="failed">
          {/* List failed operations */}
        </TabPane>
      </Tabs>
    </Modal>
  );
};
```

---

## Usage Patterns

### Pattern 1: Simple Operation

```csharp
// Backend
_progressReporter.CreateOperation(id, "Quick task", OperationType.Generic);
await DoWork();
_progressReporter.CompleteOperation(id, "Done");
```

### Pattern 2: Multi-Step Operation

```csharp
// Backend
_progressReporter.CreateOperation(id, "Complex task", OperationType.BatchProcess);
foreach (var item in items)
{
    var percent = (items.IndexOf(item) + 1) * 100 / items.Count;
    _progressReporter.UpdateProgress(id, percent, $"Processing {item.Name}");
    await ProcessItem(item);
}
_progressReporter.CompleteOperation(id, $"Processed {items.Count} items");
```

### Pattern 3: Error Handling

```csharp
// Backend
try
{
    _progressReporter.CreateOperation(id, "Risky operation", OperationType.Import);
    await RiskyWork();
    _progressReporter.CompleteOperation(id, "Success");
}
catch (Exception ex)
{
    _progressReporter.FailOperation(id, ex.Message);
    throw; // Re-throw for proper error response
}
```

---

## Operation Types

```csharp
public enum OperationType
{
    Generic,
    ModLoad,
    ModUnload,
    BatchProcess,
    Import,
    Export,
    Migration,
    Validation,
    Cleanup
}
```

---

## Best Practices

1. **Always report progress for operations >1 second**
2. **Use meaningful operation names** (include item name/count)
3. **Report at logical steps** (not too frequent, causes UI jitter)
4. **Include error details** when operations fail
5. **Clean up on cancellation** (dispose resources)

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Operations not appearing | Check IProgressReporter is injected |
| Progress stuck at 0% | Ensure UpdateProgress is called |
| No completion notification | Always call Complete/Fail in finally block |
| Memory leak | Limit history to 50 items (automatic) |

---

## API Reference

### IProgressReporter

```csharp
void CreateOperation(string operationId, string operationName, OperationType type);
void UpdateProgress(string operationId, int percentComplete, string currentStep);
void CompleteOperation(string operationId, string resultMessage);
void FailOperation(string operationId, string errorMessage);
void CancelOperation(string operationId);
```

### Frontend Hooks

```typescript
const { activeOperations, completedOperations, failedOperations } = useOperation();
```

---

**Key Files:**
- Backend: `Modules/Core/Services/ProgressReporter.cs`
- Frontend: `src/shared/context/OperationContext.tsx`
- Status Bar: `src/shared/components/AppStatusBar.tsx`