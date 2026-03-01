# Event Hub Architecture

## Overview

The Event Hub (EventBus) provides a decoupled, pub/sub messaging system for communication between modules, services, and plugins. It enables event-driven architecture where components can emit and subscribe to events without direct dependencies.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     Event Publishers                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ ModFacade    │  │ProfileService│  │ Workflow     │         │
│  │ Emits:       │  │ Emits:       │  │ Emits:       │         │
│  │ MOD:LOADED   │  │PROFILE:CREATED│  │WORKFLOW:     │         │
│  │ MOD:IMPORTED │  │PROFILE:SWITCHED│ │COMPLETED     │         │
│  └──────┬───────┘  └──────┬────────┘  └──────┬───────┘         │
└─────────┼──────────────────┼────────────────────┼────────────────┘
          │                  │                    │
          └──────────────────┼────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    EventBus (Singleton)                         │
│                                                                 │
│  Event Registry:                                                │
│  ┌───────────────────────────────────────────────────────┐     │
│  │ Pattern-based subscriptions                           │     │
│  │ { "MOD:LOADED" → [handler1, handler2, ...] }         │     │
│  │ { "MOD:*"      → [wildcardHandler1, ...] }           │     │
│  │ { "*:*"        → [globalHandler1, ...] }             │     │
│  └───────────────────────────────────────────────────────┘     │
│                                                                 │
│  Message Flow:                                                  │
│  1. EmitAsync(module, type, payload)                           │
│  2. Match handlers by pattern                                   │
│  3. Execute handlers in parallel                                │
│  4. Forward to WebView via EventBusIpcBridge                   │
└────────────────────────────┬────────────────────────────────────┘
                             │
          ┌──────────────────┼──────────────────┐
          ▼                  ▼                  ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ Handler 1       │  │ Handler 2       │  │ IpcBridge       │
│ (Plugin)        │  │ (Service)       │  │ (Frontend)      │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

## Core Interface

### IEventBus

```csharp
public interface IEventBus
{
    // Specific event in specific profile
    string RegisterHandler(string module, string type, string profileId,
                           Func<EventMessage, Task> handler);

    // Specific event in all profiles
    string RegisterHandler(string module, string type,
                           Func<EventMessage, Task> handler);

    // All events from module in specific profile
    string RegisterHandlerForModule(string module, string profileId,
                                    Func<EventMessage, Task> handler);

    // All events from module (all types, all profiles)
    string RegisterHandlerForModule(string module,
                                    Func<EventMessage, Task> handler);

    // All events (wildcard)
    string RegisterHandlerForAll(Func<EventMessage, Task> handler);

    void UnregisterHandler(string registrationId);

    Task EmitAsync(EventMessage message);
    Task EmitAsync(string module, string type, object? payload = null,
                   string? profileId = null);
}
```

### EventMessage

```csharp
public class EventMessage
{
    public string Id { get; set; }           // Unique event ID
    public string Module { get; set; }       // Event module (MOD, PROFILE, etc.)
    public string Type { get; set; }         // Event type (LOADED, DELETED, etc.)
    public string? ProfileId { get; set; }   // Optional profile scope
    public object? Payload { get; set; }     // Event data
    public DateTime Timestamp { get; set; }  // UTC timestamp
}
```

#### Profile Scoping

Events can be **global** or **profile-scoped**:

- **Global Events**: `ProfileId` is null/empty - affects entire application
  - Examples: `SYSTEM:APPLICATION_STARTED`, `SYSTEM:APPLICATION_SHUTDOWN`
  - These events are not tied to any specific profile

- **Profile-Scoped Events**: `ProfileId` is set - affects specific profile only
  - Examples: `MOD:LOADED`, `MOD:IMPORTED` (within a profile context)
  - Frontend handlers can filter events by active profile
  - Plugins can monitor events across all profiles or filter by profile

**Event Identifier Format:**
- Global: `MODULE:TYPE` (e.g., `SYSTEM:APPLICATION_STARTED`)
- Profile-scoped: `MODULE:TYPE:PROFILE_ID` (e.g., `MOD:LOADED:profile-123`)

## Registration Patterns

### Specific Event + Specific Profile
```csharp
// Subscribe to MOD:LOADED events in specific profile only
eventBus.RegisterHandler("MOD", "LOADED", "profile-123", handler);
```

### Specific Event + All Profiles
```csharp
// Subscribe to MOD:LOADED events across all profiles
eventBus.RegisterHandler("MOD", "LOADED", handler);
```

### Module Events + Specific Profile
```csharp
// Subscribe to all MOD events in specific profile
eventBus.RegisterHandlerForModule("MOD", "profile-123", handler);

// Matches: MOD:LOADED, MOD:UNLOADED, MOD:IMPORTED, etc. (only in profile-123)
```

### Module Events + All Profiles
```csharp
// Subscribe to all MOD events across all profiles
eventBus.RegisterHandlerForModule("MOD", handler);

// Matches: MOD:LOADED, MOD:UNLOADED, MOD:IMPORTED, etc. (any profile)
```

### All Events (Wildcard)
```csharp
// Subscribe to ALL events from all modules and profiles
eventBus.RegisterHandlerForAll(handler);

// Matches: Everything
```

## Common Event Patterns

All events follow the pattern: `MODULE:EVENT_TYPE`

### System Events (SYSTEM)

```csharp
SYSTEM:APPLICATION_STARTED   // App startup complete
SYSTEM:APPLICATION_SHUTDOWN  // App shutting down
SYSTEM:LOG_LEVEL_CHANGED     // Log level changed
```

### Mod Events (MOD)

```csharp
MOD:LOADED               // Mod loaded into profile
MOD:UNLOADED             // Mod unloaded from profile
MOD:IMPORTED             // New mod imported
MOD:DELETED              // Mod deleted
MOD:REFRESHED            // Mod list refreshed
MOD:CATEGORIES_UPDATED   // Categories updated
MOD:METADATA_UPDATED     // Mod metadata updated
MOD:CATEGORY_UPDATED     // Mod category changed
MOD:PREVIEW_IMPORTED     // Preview image imported
MOD:THUMBNAIL_UPDATED    // Thumbnail updated
MOD:PREVIEW_DELETED      // Preview deleted
```

### Profile Events (PROFILE)

```csharp
PROFILE:CREATED          // New profile created
PROFILE:UPDATED          // Profile updated
PROFILE:DELETED          // Profile deleted
PROFILE:DUPLICATED       // Profile duplicated
PROFILE:SWITCHED         // Active profile changed
PROFILE:CONFIG_UPDATED   // Profile config updated
```

### Workflow Events (WORKFLOW)

```csharp
WORKFLOW:CREATED                 // Workflow created
WORKFLOW:STATUS_CHANGED          // Workflow status changed
WORKFLOW:COMPLETED               // Workflow completed successfully
WORKFLOW:FAILED                  // Workflow failed
WORKFLOW:CANCELLED               // Workflow cancelled
```

### Setting Events (SETTING)

```csharp
SETTING:WINDOW_STATE_RESET       // Window state reset
SETTING:GLOBAL_SETTINGS_CHANGED  // Global settings changed
```

### Migration Events (MIGRATION)

```csharp
MIGRATION:COMPLETED  // Migration completed
```

### Tool Events (TOOL)

```csharp
TOOL:CACHE_CLEANED       // Cache cleaned
TOOL:CACHE_ITEM_DELETED  // Cache item deleted
```

### Plugin Events (PLUGIN)

**Note:** The current plugin system does not emit events. Plugins are **consumers only** - they:
- Subscribe to system events (MOD, PROFILE, SYSTEM, etc.) via `IPluginContext.RegisterEventHandler()`
- Handle IPC messages from the frontend via `HandleMessageAsync()`

Plugin-to-plugin communication via events is not currently supported but could be added in the future if needed for cross-plugin scenarios.

## Usage Examples

### 1. Subscribing to Events

```csharp
public class MyService
{
    private readonly IEventBus _eventBus;
    private readonly List<string> _registrations = new();

    public MyService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Initialize()
    {
        // Subscribe to specific event
        var id1 = _eventBus.RegisterHandler("MOD", "LOADED", OnModLoaded);
        _registrations.Add(id1);

        // Subscribe to all MOD events
        var id2 = _eventBus.RegisterHandlerForModule("MOD", OnAnyModEvent);
        _registrations.Add(id2);

        // Subscribe to all events
        var id3 = _eventBus.RegisterHandlerForAll(OnAnyEvent);
        _registrations.Add(id3);
    }

    private async Task OnModLoaded(EventMessage e)
    {
        var sha = e.Payload?.GetProperty("Sha").GetString();
        Console.WriteLine($"Mod loaded: {sha}");
    }

    private async Task OnAnyModEvent(EventMessage e)
    {
        Console.WriteLine($"MOD event: {e.Type}");
    }

    private async Task OnAnyEvent(EventMessage e)
    {
        Console.WriteLine($"Event: {e.Module}:{e.Type}");
    }

    public void Cleanup()
    {
        foreach (var id in _registrations)
        {
            _eventBus.UnregisterHandler(id);
        }
    }
}
```

### 2. Emitting Events

```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.System;

public class ModFacade
{
    private readonly IEventBus _eventBus;

    public async Task LoadModAsync(string sha, string profileId)
    {
        // Load the mod...

        // Emit profile-scoped event
        await _eventBus.EmitAsync(
            module: ModuleNames.MOD,
            type: ModEvents.LOADED,
            payload: new { Sha = sha, ProfileId = profileId },
            profileId: profileId  // Profile scope
        );
    }

    public async Task DeleteModAsync(string sha, string profileId)
    {
        // Delete the mod...

        // Emit profile-scoped event
        await _eventBus.EmitAsync(
            module: ModuleNames.MOD,
            type: ModEvents.DELETED,
            payload: new { Sha = sha },
            profileId: profileId  // Profile scope
        );
    }

    public async Task StartApplicationAsync()
    {
        // Startup logic...

        // Emit global event (no profileId)
        await _eventBus.EmitAsync(
            module: ModuleNames.SYSTEM,
            type: SystemEvents.APPLICATION_STARTED,
            payload: new { Version = "1.0.0" }
            // profileId omitted = global event
        );
    }
}
```

### 3. Plugin Event Handling

```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.System;

public class MonitorPlugin : IPlugin
{
    private IPluginContext? _context;
    private readonly List<string> _registrations = new();

    public async Task InitAsync(IPluginContext context)
    {
        _context = context;

        // Monitor all MOD events (across all profiles)
        var id1 = context.EventBus.RegisterHandlerForModule(ModuleNames.MOD, async (e) =>
        {
            // e.ProfileId indicates which profile this event belongs to
            var profileInfo = string.IsNullOrEmpty(e.ProfileId)
                ? "global"
                : $"profile {e.ProfileId}";
            await LogEventAsync($"MOD event: {e.Type} in {profileInfo}");
        });
        _registrations.Add(id1);

        // Monitor only events for specific profile
        var targetProfileId = "profile-123";
        var id2 = context.EventBus.RegisterHandler(
            ModuleNames.MOD, ModEvents.LOADED, targetProfileId,
            async (e) => await LogEventAsync($"Mod loaded in target profile")
        );
        _registrations.Add(id2);

        // Monitor global events (no profile filtering needed)
        var id3 = context.EventBus.RegisterHandler(
            ModuleNames.SYSTEM,
            SystemEvents.APPLICATION_STARTED,
            async (e) => await LogEventAsync("App started")
        );
        _registrations.Add(id3);

        var id4 = context.EventBus.RegisterHandler(
            ModuleNames.SYSTEM,
            SystemEvents.APPLICATION_SHUTDOWN,
            async (e) => await LogEventAsync("App shutting down")
        );
        _registrations.Add(id4);
    }

    private async Task LogEventAsync(string message)
    {
        // Log to file or console...
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _registrations)
        {
            _context?.EventBus.UnregisterHandler(id);
        }
    }
}
```

## EventBus + IpcBridge Integration

The `EventBusIpcBridge` forwards backend events to the frontend WebView:

```
Backend Event
    ↓
EventBus.EmitAsync()
    ↓
EventBusIpcBridge (subscriber)
    ↓
IpcHandler.SendNotification()
    ↓
WebView (Frontend)
```

### Frontend Event Subscription

```typescript
// Subscribe to backend events
ipc.subscribe("MOD", "LOADED", (event) => {
    console.log("Mod loaded:", event.payload);
});

// Subscribe to all MOD events
ipc.subscribe("MOD", "*", (event) => {
    console.log("MOD event:", event.type, event.payload);
});
```

## Event Flow Example

### Complete Flow: Mod Import

```
1. User imports mod via frontend
   Frontend: MOD/IMPORT IPC call
        ↓
2. ModFacade.ImportModAsync()
   - Processes mod file
   - Saves to database
        ↓
3. ModFacade emits event
   await _eventBus.EmitAsync(
       ModuleNames.MOD,
       ModEvents.IMPORTED,
       modInfo
   );
        ↓
4. EventBus matches handlers
   - Plugin handlers (monitoring, logging)
   - Service handlers (cache invalidation)
   - IpcBridge (forwards to frontend)
        ↓
5. Parallel execution
   ┌─────────────────┬─────────────────┬─────────────────┐
   │ Plugin 1        │ Cache Service   │ Frontend        │
   │ Logs import     │ Invalidates     │ Updates UI      │
   └─────────────────┴─────────────────┴─────────────────┘
```

## Best Practices

### 1. Profile Scoping

Always set `profileId` for profile-specific operations:

```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;

// Good - Profile-scoped event
await eventBus.EmitAsync(
    ModuleNames.MOD,
    ModEvents.LOADED,
    new { Sha = sha },
    profileId: profileId  // Explicitly set profile scope
);

// Bad - Missing profile context for profile-specific operation
await eventBus.EmitAsync(
    ModuleNames.MOD,
    ModEvents.LOADED,
    new { Sha = sha }
    // profileId missing - event appears global
);
```

**When to use profile scoping:**
- ✅ **Use ProfileId**: MOD operations, PROFILE operations, profile-specific settings
- ❌ **Omit ProfileId**: SYSTEM events, application-wide events, global settings

### 2. Event Naming Convention

Always use the predefined constants from `ModuleNames` and event classes:
```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Profile;

// Good - Use constants
await eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, payload);
await eventBus.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CREATED, payload);

// Bad - Magic strings
await eventBus.EmitAsync("MOD", "LOADED", payload);
await eventBus.EmitAsync("mod", "loaded", payload);
```

### 3. Payload Structure

Use anonymous objects or DTOs:
```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;

// Good - structured payload with constants
await eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new {
    Sha = modSha,
    ProfileId = profileId,
    Name = modName
}, profileId: profileId);

// Avoid - primitive payloads
await eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, modSha, profileId);
```

### 4. Error Handling

Handlers should not throw exceptions:
```csharp
eventBus.RegisterHandler("MOD", "LOADED", async (e) =>
{
    try
    {
        // Handler logic
    }
    catch (Exception ex)
    {
        _logger.Error($"Error handling event: {ex.Message}", ex);
        // Don't rethrow - other handlers should still run
    }
});
```

### 5. Cleanup Registrations

Always unregister handlers when disposing:
```csharp
public class MyService : IDisposable
{
    private readonly List<string> _registrations = new();

    public void Initialize()
    {
        var id = _eventBus.RegisterHandler("MOD", "*", OnModEvent);
        _registrations.Add(id);
    }

    public void Dispose()
    {
        foreach (var id in _registrations)
        {
            _eventBus.UnregisterHandler(id);
        }
        _registrations.Clear();
    }
}
```

### 6. Avoid Circular Events

Don't emit events from within event handlers of the same type:
```csharp
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;

// BAD - Infinite loop risk
eventBus.RegisterHandler(ModuleNames.MOD, ModEvents.LOADED, async (e) =>
{
    // Processing...
    await eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, newData); // DON'T DO THIS
});

// GOOD - Emit different event or use flag
eventBus.RegisterHandler(ModuleNames.MOD, ModEvents.LOADED, async (e) =>
{
    // Processing...
    await eventBus.EmitAsync(ModuleNames.MOD, ModEvents.REFRESHED, processedData);
});
```

## Performance Characteristics

### Handler Execution
- **Parallel**: Handlers execute concurrently
- **Fire-and-Forget**: EmitAsync doesn't wait for handlers
- **Non-Blocking**: Event emission is fast

### Pattern Matching
- **O(n)**: Linear scan of registered handlers
- **Cached**: Pattern matching is optimized
- **Thread-Safe**: Uses concurrent collections

### Memory
- **Weak References**: No memory leaks from registrations
- **Cleanup**: Unregister handlers when disposed

## Implementation Details

### EventBus Class

Located at: `Modules/Core/Event/EventBus.cs`

```csharp
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, EventHandler> _handlers;
    private readonly ILogHelper _logger;

    public string RegisterHandler(
        string modulePattern,
        string typePattern,
        Func<EventMessage, Task> handler)
    {
        var registrationId = Guid.NewGuid().ToString();
        var eventHandler = new EventHandler
        {
            ModulePattern = modulePattern,
            TypePattern = typePattern,
            Handler = handler
        };
        _handlers[registrationId] = eventHandler;
        return registrationId;
    }

    public async Task EmitAsync(
        string module,
        string type,
        object? payload = null,
        string? source = null)
    {
        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid().ToString(),
            Module = module,
            Type = type,
            Payload = payload,
            Source = source,
            Timestamp = DateTime.UtcNow
        };

        // Find matching handlers
        var matchingHandlers = _handlers.Values
            .Where(h => MatchesPattern(h, module, type))
            .ToList();

        // Execute handlers in parallel (fire-and-forget)
        var tasks = matchingHandlers.Select(h =>
            ExecuteHandlerSafelyAsync(h.Handler, eventMessage));

        await Task.WhenAll(tasks);
    }

    private bool MatchesPattern(
        EventHandler handler,
        string module,
        string type)
    {
        var moduleMatch = handler.ModulePattern == "*" ||
            string.Equals(handler.ModulePattern, module,
                StringComparison.OrdinalIgnoreCase);

        var typeMatch = handler.TypePattern == "*" ||
            string.Equals(handler.TypePattern, type,
                StringComparison.OrdinalIgnoreCase);

        return moduleMatch && typeMatch;
    }
}
```

### EventBusIpcBridge Class

Located at: `Infrastructure/WebView/EventBusIpcBridge.cs`

```csharp
public class EventBusIpcBridge : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IpcHandler _ipcHandler;
    private readonly List<string> _registrationIds = new();

    public void Init()
    {
        // Subscribe to ALL backend events (all modules, types, profiles)
        var registrationId = _eventBus.RegisterHandlerForAll(
            async (message) => await ForwardEventToFrontend(message)
        );
        _registrationIds.Add(registrationId);
    }

    private async Task ForwardEventToFrontend(EventMessage message)
    {
        _ipcHandler.SendNotification(
            module: message.Module,
            type: message.Type,
            payload: message.Payload
        );
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var registrationId in _registrationIds)
        {
            _eventBus.UnregisterHandler(registrationId);
        }
        _registrationIds.Clear();
    }
}
```

## Testing

### Unit Test Example

```csharp
[TestClass]
public class EventBusTests
{
    [TestMethod]
    public async Task EmitAsync_MatchesExactPattern()
    {
        // Arrange
        var eventBus = new EventBus(mockLogger);
        var received = false;

        eventBus.RegisterHandler("MOD", "LOADED", async (e) =>
        {
            received = true;
        });

        // Act
        await eventBus.EmitAsync("MOD", "LOADED");

        // Assert
        Assert.IsTrue(received);
    }

    [TestMethod]
    public async Task EmitAsync_MatchesModuleEvents()
    {
        // Arrange
        var eventBus = new EventBus(mockLogger);
        var events = new List<string>();

        eventBus.RegisterHandlerForModule("MOD", async (e) =>
        {
            events.Add(e.Type);
        });

        // Act
        await eventBus.EmitAsync("MOD", "LOADED");
        await eventBus.EmitAsync("MOD", "UNLOADED");

        // Assert
        Assert.AreEqual(2, events.Count);
        CollectionAssert.Contains(events, "LOADED");
        CollectionAssert.Contains(events, "UNLOADED");
    }
}
```

## Related Documentation

- [PLUGIN_ARCHITECTURE.md](./PLUGIN_ARCHITECTURE.md) - Plugin system using EventBus
- [MESSAGE_DISPATCHER_ARCHITECTURE.md](./MESSAGE_DISPATCHER_ARCHITECTURE.md) - Message routing
- [MODULE_ARCHITECTURE.md](./MODULE_ARCHITECTURE.md) - Module events
