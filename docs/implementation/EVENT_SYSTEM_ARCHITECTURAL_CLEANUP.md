# Event System Architectural Cleanup

**Date:** 2026-02-25
**Version:** 1.0
**Status:** ✅ Complete

## Overview

This document describes the architectural cleanup of the event system to simplify event forwarding from backend to frontend and improve separation of concerns between core and module-specific events.

## Problems Identified

### 1. Double-Wrapping of Event Data

**Issue:** The `EventBusIpcBridge` was wrapping event data in `{ eventName, data }` before sending to frontend, and the frontend `bridgeService` had to unwrap it.

**Example of old flow:**
```csharp
// Backend sends:
{
  category: "notification",
  type: "TASK_ADDED",
  data: {
    eventName: "TASK_ADDED",
    data: { taskId: "123", status: "pending" }  // Nested data
  }
}

// Frontend had to unwrap:
const actualData = parsed.data?.data ?? parsed.data;
```

**Problem:** Unnecessary complexity, data structure inconsistency, harder to debug.

### 2. Useless Event Filtering

**Issue:** The `EventBusIpcBridge.Initialize()` method looped through `CoreEvents.All` array to register handlers for each event type individually.

**Example of old code:**
```csharp
public void Initialize()
{
    foreach (var eventType in CoreEvents.All)
    {
        var registrationId = _eventBus.RegisterHandler(eventType, async (message) =>
        {
            await ForwardEventToFrontend(message);
        });
        _registrationIds.Add(registrationId);
    }
}
```

**Problems:**
- Required manual registration of every event type in `CoreEvents.All`
- New module events had to be added to core events file
- Forgot to add event → event never reached frontend
- Bridge was supposed to forward ALL events anyway, making filtering pointless

### 3. CoreEvents Contains Module Events

**Issue:** `CoreEvents.cs` contained events from multiple modules (MOD_*, TASK_*, DROP_ZONE_*), violating separation of concerns.

**Example of old CoreEvents.cs:**
```csharp
public static class CoreEvents
{
    // Application lifecycle
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";

    // Mod events (should be in ModEvents)
    public const string MOD_LOADED = "MOD_LOADED";
    public const string MOD_UNLOADED = "MOD_UNLOADED";

    // Task events (should be in TaskQueueEvents)
    public const string TASK_ADDED = "TASK_ADDED";
    public const string TASK_STARTED = "TASK_STARTED";

    // DropZone events (should be in DropZoneEvents)
    public const string DROP_ZONE_CLICK = "DROP_ZONE_CLICK";

    // Required for EventBusIpcBridge
    public static readonly string[] All = new[] {
        APPLICATION_STARTED, APPLICATION_SHUTDOWN,
        MOD_LOADED, MOD_UNLOADED,
        TASK_ADDED, TASK_STARTED,
        DROP_ZONE_CLICK,
        // ... etc
    };
}
```

**Problems:**
- Core module shouldn't know about Mods, TaskQueue, DropZone modules
- Violates dependency inversion principle
- Makes CoreEvents file grow unbounded as new modules are added

## Solutions Implemented

### 1. Remove Double-Wrapping

**Change:** Modified `EventBusIpcBridge.ForwardEventToFrontend()` to send data directly without wrapping.

**Files Changed:**
- [D3dxSkinManager\Infrastructure\EventBusIpcBridge.cs](../../D3dxSkinManager/Infrastructure/EventBusIpcBridge.cs:51-62)
- [D3dxSkinManager.Client\src\shared\services\bridgeService.ts](../../D3dxSkinManager.Client/src/shared/services/bridgeService.ts:73-81)

**New backend code:**
```csharp
private async Task ForwardEventToFrontend(EventMessage message)
{
    try
    {
        _logger.Verbose($"Forwarding event to frontend: {message.EventType}", "EventBridge");

        // Send notification directly - no double wrapping
        _ipcHandler.SendNotification(
            type: message.EventType,
            data: message.Data
        );

        await Task.CompletedTask;
    }
    catch (Exception ex)
    {
        _logger.Error($"Error forwarding event to frontend: {ex.Message}", "EventBridge", ex);
    }
}
```

**New frontend code:**
```typescript
} else if (parsed.category === "notification") {
  // Push notification/event - emit to eventBus
  // Frontend subscribers use the 'type' to identify which event they want
  eventBus.emit({
    type: parsed.type as EventType,
    eventName: parsed.eventName,
    data: parsed.data,  // Direct data, no unwrapping needed
  });
}
```

**Result:** Data flows directly from backend to frontend without intermediate wrapping.

### 2. Wildcard Event Forwarding

**Change:** Modified `EventBusIpcBridge.Initialize()` to use wildcard "*" pattern to forward ALL events automatically.

**File Changed:**
- [D3dxSkinManager\Infrastructure\EventBusIpcBridge.cs](../../D3dxSkinManager/Infrastructure/EventBusIpcBridge.cs:31-45)
- [D3dxSkinManager\Modules\Core\Event\EventBus.cs](../../D3dxSkinManager/Modules/Core/Event/EventBus.cs:62-81)

**New EventBusIpcBridge code:**
```csharp
public void Initialize()
{
    _logger.Info("Initializing EventBus IPC Bridge - forwarding all events", "EventBridge");

    // Subscribe to ALL events with wildcard pattern
    // EventBus will match any event type starting with this pattern
    var registrationId = _eventBus.RegisterHandler("*", async (message) =>
    {
        await ForwardEventToFrontend(message);
    });

    _registrationIds.Add(registrationId);

    _logger.Info("EventBus IPC Bridge initialized - forwarding all events to frontend", "EventBridge");
}
```

**New EventBus.EmitAsync code:**
```csharp
public virtual async Task EmitAsync(EventMessage message)
{
    List<Func<EventMessage, Task>> handlersToInvoke;

    lock (_lock)
    {
        // Get handlers that match this event type
        // Support wildcard "*" to match all events
        handlersToInvoke = _handlers
            .Where(kvp => kvp.Key.StartsWith($"{message.EventType}_") || kvp.Key.StartsWith("*_"))
            .Select(kvp => kvp.Value)
            .ToList();
    }

    _logger.Verbose($"[EventBus] Emitting {message.EventType} to {handlersToInvoke.Count} handler(s)", "EventBus");

    var tasks = handlersToInvoke.Select(handler => SafeInvokeHandler(handler, message));
    await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

**Result:**
- No need to manually register each event type
- New module events automatically forwarded to frontend
- Removed dependency on `CoreEvents.All` array

### 3. Clean Up CoreEvents

**Change:** Moved module-specific events to their respective module files, keeping only core lifecycle events in `CoreEvents.cs`.

**Files Changed:**
- [D3dxSkinManager\Modules\Core\Event\CoreEvents.cs](../../D3dxSkinManager/Modules/Core/Event/CoreEvents.cs)
- [D3dxSkinManager.ExamplePlugin\ModLoggerPlugin.cs](../../D3dxSkinManager.ExamplePlugin/ModLoggerPlugin.cs)

**New CoreEvents.cs:**
```csharp
namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Core system event type constants.
/// Only contains events that are truly core to the application lifecycle.
/// Module-specific events should be defined in their respective modules.
/// </summary>
public static class CoreEvents
{
    // Application lifecycle
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";

    // Core system events
    public const string CUSTOM_EVENT = "CUSTOM_EVENT";
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";
}
```

**Module events now live in their own files:**
- `Modules/Mods/ModEvents.cs` - MOD_LOADED, MOD_UNLOADED, MOD_IMPORTED, MOD_DELETED, etc.
- `Modules/TaskQueue/TaskQueueEvents.cs` - TASK_ADDED, TASK_STARTED, TASK_PROGRESS, etc.
- `Infrastructure/DropZoneEvents.cs` - DROP_ZONE_CLICK, DROP_ZONE_DRAG_ENTER, etc.

**Result:**
- Clear separation of concerns
- CoreEvents only contains core lifecycle events
- Each module owns its own events
- No dependency from Core to other modules

### 4. Update ExamplePlugin

**Change:** Updated example plugin to use `ModEvents` instead of `CoreEvents` for mod-specific events.

**File Changed:**
- [D3dxSkinManager.ExamplePlugin\ModLoggerPlugin.cs](../../D3dxSkinManager.ExamplePlugin/ModLoggerPlugin.cs:1-9,43-47)

**Old code:**
```csharp
using D3dxSkinManager.Modules.Core.Event;

// ...

_context.RegisterEventHandler(CoreEvents.MOD_LOADED, OnModLoaded);
_context.RegisterEventHandler(CoreEvents.MOD_UNLOADED, OnModUnloaded);
_context.RegisterEventHandler(CoreEvents.MOD_IMPORTED, OnModImported);
_context.RegisterEventHandler(CoreEvents.MOD_DELETED, OnModDeleted);
```

**New code:**
```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;  // Added

// ...

_context.RegisterEventHandler(CoreEvents.APPLICATION_STARTED, OnApplicationStarted);
_context.RegisterEventHandler(ModEvents.MOD_LOADED, OnModLoaded);
_context.RegisterEventHandler(ModEvents.MOD_UNLOADED, OnModUnloaded);
_context.RegisterEventHandler(ModEvents.MOD_IMPORTED, OnModImported);
_context.RegisterEventHandler(ModEvents.MOD_DELETED, OnModDeleted);
```

## Benefits

### 1. Simplicity
- Removed unnecessary data wrapping/unwrapping
- Single wildcard handler instead of loop with multiple registrations
- Clearer data flow from backend to frontend

### 2. Maintainability
- No need to manually register new event types in `CoreEvents.All`
- Each module owns its events
- Changes to one module don't affect `CoreEvents`

### 3. Separation of Concerns
- CoreEvents only contains core lifecycle events
- Module events live in module namespaces
- No dependency from Core to other modules

### 4. Developer Experience
- Add new event → Just emit it → Automatically forwarded
- No mysterious "event not reaching frontend" bugs
- Clearer debugging (direct data structure)

## Migration Notes

### For New Events

**Old workflow:**
1. Add constant to module events file
2. Add to `CoreEvents.All` array (easy to forget!)
3. Add to frontend `EventType` enum
4. Emit event
5. Subscribe in frontend

**New workflow:**
1. Add constant to module events file
2. Add to frontend `EventType` enum
3. Emit event (automatically forwarded)
4. Subscribe in frontend

### For Existing Code

**Plugins using old CoreEvents:**
- Change `CoreEvents.MOD_*` → `ModEvents.MOD_*`
- Add `using D3dxSkinManager.Modules.Mod;`

**Frontend code:**
- No changes needed (data structure is simpler now)
- Remove any manual unwrapping code if present

## Testing

### Build Verification
```bash
dotnet build D3dxSkinManager.sln -c Release
```
**Result:** ✅ Build succeeded with 0 errors

### Runtime Verification
Test that events flow correctly:
1. Start application
2. Import a mod (triggers MOD_IMPORTED)
3. Add a task (triggers TASK_ADDED, TASK_STARTED, TASK_PROGRESS, TASK_COMPLETED)
4. Verify frontend UI updates in real-time

## Documentation Updates

Updated the following documentation files:

1. **[docs/AI_GUIDE.md](../AI_GUIDE.md)** (v2.2 → v2.3)
   - Updated event system section
   - Removed reference to `CoreEvents.All` registration requirement
   - Added wildcard forwarding explanation
   - Updated data structure examples

2. **[docs/implementation/EVENT_SYSTEM_ARCHITECTURAL_CLEANUP.md](./EVENT_SYSTEM_ARCHITECTURAL_CLEANUP.md)** (this file)
   - Complete record of changes made
   - Migration guide
   - Before/after code examples

## Related Documentation

- [TASK_QUEUE_SYSTEM.md](../features/TASK_QUEUE_SYSTEM.md) - TaskQueue feature documentation
- [TASK_QUEUE_IMPLEMENTATION.md](./TASK_QUEUE_IMPLEMENTATION.md) - TaskQueue implementation summary
- [AI_GUIDE.md](../AI_GUIDE.md) - AI assistant guide with event system patterns

## Summary

Successfully simplified the event system architecture by:
1. ✅ Removing double-wrapping of event data
2. ✅ Implementing wildcard event forwarding
3. ✅ Cleaning up CoreEvents to only contain core lifecycle events
4. ✅ Updating example plugin to use correct event constants
5. ✅ Updating documentation to reflect new architecture

The event system is now simpler, more maintainable, and provides better separation of concerns between core and module-specific functionality.
