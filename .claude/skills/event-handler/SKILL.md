---
name: event-handler
description: Generate C# event consolidation handler that subscribes to multiple events and emits a single consolidated event for frontend
---

# Event Handler Generator

Generate an event consolidation handler that subscribes to multiple backend events and emits a single consolidated event for the frontend.

## Purpose

Event handlers solve the event storm problem by consolidating multiple related events into one:

**Before (Event Storm)**:
```typescript
// Frontend subscribes to 8+ events
eventBus.subscribe(Module.MOD, ModEventType.LOADED, refresh);
eventBus.subscribe(Module.MOD, ModEventType.UNLOADED, refresh);
eventBus.subscribe(Module.MOD, ModEventType.DELETED, refresh);
eventBus.subscribe(Module.MOD, ModEventType.IMPORTED, refresh);
// ... 8+ subscriptions, all do the same thing
```

**After (Consolidated)**:
```typescript
// Frontend subscribes to 1 event
eventBus.subscribe(Module.MOD, ModEventType.MOD_LIST_UPDATED, refresh);
```

## Arguments

**Format**: `/event-handler <HandlerName> <Module> <SourceEvents> <TargetEvent>`

**Example**:
```
/event-handler ModListEventHandler Mod LOADED,UNLOADED,DELETED,IMPORTED MOD_LIST_UPDATED
```

**Parameters**:
- `HandlerName` - Name ending with "EventHandler" (e.g., ModListEventHandler)
- `Module` - Module name (e.g., Mod, Profile, Workflow)
- `SourceEvents` - Comma-separated source event names to subscribe to
- `TargetEvent` - Single consolidated event name to emit

## What This Skill Generates

1. **Interface file** (`I{HandlerName}.cs`)
   - Empty marker interface
   - Used for DI registration

2. **Implementation file** (`{HandlerName}.cs`)
   - Constructor subscribes to all source events
   - Private method handles events and emits consolidated event
   - Proper disposal if needed

3. **Handler registration** (updates `{Module}ServiceExtensions.cs`)
   - Singleton registration ensures handler is instantiated at startup

## Pattern to Follow

```csharp
// 1. Interface (I{HandlerName}.cs)
namespace D3dxSkinManager.Modules.{Module}.EventHandlers;

/// <summary>
/// Event handler that consolidates {module} state change events
/// </summary>
public interface I{HandlerName}
{
    // Empty interface - just for DI
}

// 2. Implementation ({HandlerName}.cs)
namespace D3dxSkinManager.Modules.{Module}.EventHandlers;

/// <summary>
/// Consolidates multiple {module} events into a single {TargetEvent} event
/// </summary>
public class {HandlerName} : I{HandlerName}
{
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    public {HandlerName}(
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        // Subscribe to all source events
        _eventBus.Subscribe(ModuleNames.{MODULE}, {Module}Events.SOURCE_EVENT_1,
            async (data) => await EmitConsolidatedEvent("SOURCE_EVENT_1", data));
        _eventBus.Subscribe(ModuleNames.{MODULE}, {Module}Events.SOURCE_EVENT_2,
            async (data) => await EmitConsolidatedEvent("SOURCE_EVENT_2", data));
        // ... one subscription per source event
    }

    private async Task EmitConsolidatedEvent(string sourceEvent, object data)
    {
        _logger.Verbose($"Consolidating {sourceEvent} into {TargetEvent}");

        // Emit consolidated event
        await _eventBus.EmitAsync(
            ModuleNames.{MODULE},
            {Module}Events.{TARGET_EVENT},
            data  // Pass through original data or transform as needed
        );
    }
}
```

## Steps to Execute

1. **Find or create EventHandlers directory**:
   - Path: `Modules/{Module}/EventHandlers/`
   - Create directory if it doesn't exist

2. **Create interface file**:
   - Path: `Modules/{Module}/EventHandlers/I{HandlerName}.cs`
   - Empty interface (just for DI registration)
   - Add XML documentation explaining purpose

3. **Create implementation file**:
   - Path: `Modules/{Module}/EventHandlers/{HandlerName}.cs`
   - Inject IProfileEventBus and ILogHelper
   - In constructor, subscribe to all source events
   - Each subscription calls EmitConsolidatedEvent
   - EmitConsolidatedEvent emits target event

4. **Add event constant (if new)**:
   - Check `Modules/{Module}/Constants/{Module}Events.cs`
   - If target event doesn't exist, add constant:
     ```csharp
     public const string {TARGET_EVENT} = "{TARGET_EVENT}";
     ```

5. **Register handler**:
   - Find `Modules/{Module}/{Module}ServiceExtensions.cs`
   - Add line: `services.AddSingleton<I{HandlerName}, {HandlerName}>();`
   - Place in "Event Handlers" section (after services, before facades)

6. **Verify imports**:
   - Add necessary using statements:
     ```csharp
     using D3dxSkinManager.Modules.Core.Events;
     using D3dxSkinManager.Modules.Core.Helpers;
     using D3dxSkinManager.Modules.{Module}.Constants;
     ```

## Event Consolidation Patterns

### Pattern 1: State Change Consolidation
All state changes trigger list refresh:
```csharp
// Source: LOADED, UNLOADED, DELETED, IMPORTED, METADATA_UPDATED
// Target: MOD_LIST_UPDATED
```

### Pattern 2: Workflow Step Consolidation
All workflow steps update progress:
```csharp
// Source: STEP_STARTED, STEP_COMPLETED, STEP_FAILED
// Target: WORKFLOW_PROGRESS_UPDATED
```

### Pattern 3: Cache Invalidation Consolidation
All cache changes trigger refresh:
```csharp
// Source: CACHE_ADDED, CACHE_REMOVED, CACHE_RENAMED
// Target: CACHE_CHANGED
```

## Data Transformation

Sometimes you need to transform event data:

```csharp
private async Task EmitConsolidatedEvent(string sourceEvent, object data)
{
    // Option 1: Pass through original data
    await _eventBus.EmitAsync(
        ModuleNames.MOD,
        ModEvents.MOD_LIST_UPDATED,
        data
    );

    // Option 2: Transform data
    var transformedData = new
    {
        Source = sourceEvent,
        Timestamp = DateTime.UtcNow,
        OriginalData = data
    };
    await _eventBus.EmitAsync(
        ModuleNames.MOD,
        ModEvents.MOD_LIST_UPDATED,
        transformedData
    );

    // Option 3: No data (just trigger refresh)
    await _eventBus.EmitAsync(
        ModuleNames.MOD,
        ModEvents.MOD_LIST_UPDATED,
        new { }
    );
}
```

## Frontend Consumption

After creating event handler, frontend can subscribe to consolidated event:

```typescript
// Before: 8 subscriptions
eventBus.subscribe(Module.MOD, ModEventType.LOADED, handleRefresh);
eventBus.subscribe(Module.MOD, ModEventType.UNLOADED, handleRefresh);
// ... 6 more

// After: 1 subscription with debouncing
const handleModListUpdate = useCallback(
  debounce(() => {
    if (!selectedProfileId) return;
    void modOps.refreshMods(selectedProfileId);
  }, 20),  // 20ms debounce prevents event storm
  [selectedProfileId]
);

useEffect(() => {
  const unsubscribe = eventBus.subscribe(
    Module.MOD,
    ModEventType.MOD_LIST_UPDATED,
    handleModListUpdate
  );
  return () => {
    handleModListUpdate.cancel();
    unsubscribe();
  };
}, [selectedProfileId, handleModListUpdate]);
```

## Example Output

For: `/event-handler ModListEventHandler Mod LOADED,UNLOADED,DELETED,IMPORTED MOD_LIST_UPDATED`

Creates:
- `Modules/Mod/EventHandlers/IModListEventHandler.cs`
- `Modules/Mod/EventHandlers/ModListEventHandler.cs`
- Updates `Modules/Mod/ModServiceExtensions.cs`
- May update `Modules/Mod/Constants/ModEvents.cs` (if target event is new)

## Important Rules

- ✅ Event handlers are instantiated at startup (singleton registration)
- ✅ Subscriptions happen in constructor (automatic setup)
- ✅ Use Verbose logging for consolidation (not Info - too noisy)
- ✅ Pass through original data or transform as needed
- ✅ Inject IProfileEventBus (required for subscriptions)
- ✅ Inject ILogHelper (optional but recommended)
- ✅ Name ends with "EventHandler"
- ✅ Register as singleton in ServiceExtensions
- ❌ Don't emit events from facades (only services/handlers emit)
- ❌ Don't add business logic in handlers (just consolidate events)
- ❌ Don't forget to register handler (won't work if not registered)

## When to Create Event Handlers

Create event handler when:
- ✅ Multiple events trigger same frontend action
- ✅ Frontend needs to debounce related events
- ✅ Event storm causes performance issues
- ✅ 3+ events need consolidation

Don't create event handler when:
- ❌ Only 1-2 events (frontend can subscribe directly)
- ❌ Events need different frontend actions
- ❌ Events are infrequent (no performance issue)

## Reference Examples

Look at these existing handlers:
- `Modules/Mod/EventHandlers/ModListEventHandler.cs` - 8 events → 1 event
- `Modules/Workflow/EventHandlers/WorkflowProgressHandler.cs` - Step consolidation

## Evolution Note

**How to update this skill**:
1. Add new consolidation patterns to "Event Consolidation Patterns" section
2. Add data transformation examples as patterns emerge
3. Update frontend consumption examples with new hooks/patterns
4. Add disposal patterns if handlers need cleanup
5. Update when event system architecture changes
