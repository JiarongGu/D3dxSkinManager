# Profile-Aware Event System

**Last Updated:** 2026-03-12

## Overview

The EventBus now supports profile-scoped events, allowing services to emit and listen to events for specific profiles. This eliminates the need for profile-scoped services to manually attach `profileId` to every event.

## Architecture

### Core Components

**1. EventMessage with ProfileId**
```csharp
public class EventMessage
{
    public string Module { get; set; }
    public string Type { get; set; }
    public string? ProfileId { get; set; }  // ✨ NEW: Optional profile filter
    public object? Payload { get; set; }
}
```

**2. Global EventBus (IEventBus)**
- Singleton service shared across all profiles
- Supports profileId filtering through multiple registration overloads:
  - `RegisterHandler(module, type, handler)` - all profiles
  - `RegisterHandler(module, type, profileId, handler)` - specific profile
  - `RegisterHandlerForModule(module, handler)` - all events from module, all profiles
  - `RegisterHandlerForModule(module, profileId, handler)` - all events from module, specific profile
  - `RegisterHandlerForAll(handler)` - all events, all profiles
- Automatically filters events by profileId

**3. ProfileEventBus (IProfileEventBus)**
- Profile-scoped service injected into profile contexts
- Wrapper around global IEventBus that auto-injects ProfileId from IProfileContext
- Automatically adds ProfileId to all emitted events
- Automatically filters subscriptions to only receive events for this profile
- Profile-scoped services use this instead of global IEventBus

## Usage Patterns

### Global Services (No ProfileId)

Global services continue to use `IEventBus` for application-level events:

```csharp
public class ApplicationService
{
    private readonly IEventBus _eventBus;

    // Emit global event (no profileId)
    public async Task StartAsync()
    {
        await _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.APPLICATION_STARTED);
    }

    // Listen to all events regardless of profile
    public void Init()
    {
        _eventBus.RegisterHandlerForAll(async (message) =>
        {
            _logger.Info($"Event: {message.Module}.{message.Type}");
        });
    }
}
```

### Profile-Scoped Services (Auto ProfileId)

Profile-scoped services use `IProfileEventBus` - profileId is automatically injected:

```csharp
public class ModService
{
    private readonly IProfileEventBus _eventBus;  // ✨ Profile-scoped
    private readonly IProfileContext _profileContext;

    // ✅ CORRECT: Use IProfileEventBus - ProfileId auto-injected
    public async Task LoadModAsync(string id)
    {
        // ProfileId automatically added from ProfileContext
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LOADED, new { id });
    }

    // ❌ WRONG: Don't manually add profileId anymore
    // await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LOADED,
    //     new { id }, _profileContext.ProfileId);
}
```

### Listening to Profile-Specific Events

Use different registration overloads for different filtering needs:

```csharp
// Listen to events for a specific profile (3-parameter overload)
_eventBus.RegisterHandler(
    ModuleNames.MOD,
    ModEvents.MOD_LOADED,
    "profile-abc-123",  // ✨ Only events from this profile
    async (message) =>
    {
        _logger.Info($"Mod loaded in profile {message.ProfileId}");
    });

// Listen to events from all profiles (2-parameter overload)
_eventBus.RegisterHandler(
    ModuleNames.MOD,
    ModEvents.MOD_LOADED,
    async (message) =>
    {
        _logger.Info($"Mod loaded: {message.Payload}");
    });

// Listen to all MOD events across all profiles
_eventBus.RegisterHandlerForModule(
    ModuleNames.MOD,
    async (message) =>
    {
        _logger.Info($"MOD event: {message.Type}");
    });

// Listen to all MOD events in specific profile only
_eventBus.RegisterHandlerForModule(
    ModuleNames.MOD,
    "profile-abc-123",
    async (message) =>
    {
        _logger.Info($"MOD event in profile: {message.Type}");
    });
```

## Event Matching Rules

The EventBus uses the following matching logic based on the registration method used:

| Registration Method | Handler ProfileId Pattern | Event ProfileId | Matches? |
|-------------------|---------------------------|-----------------|----------|
| `RegisterHandler(m, t, handler)` | `null` (all profiles) | Any | ✅ Yes |
| `RegisterHandler(m, t, profileId, h)` | `"profile-123"` | `"profile-123"` | ✅ Yes (exact) |
| `RegisterHandler(m, t, profileId, h)` | `"profile-123"` | `"profile-456"` | ❌ No |
| `RegisterHandler(m, t, profileId, h)` | `"profile-123"` | `null` (global) | ❌ No |
| `RegisterHandlerForModule(m, h)` | `null` (all profiles) | Any | ✅ Yes |
| `RegisterHandlerForModule(m, profileId, h)` | `"profile-123"` | `"profile-123"` | ✅ Yes |
| `RegisterHandlerForAll(handler)` | `null` (all profiles) | Any | ✅ Yes |

**Implementation Note:** The matching logic in EventBus.cs lines 110-114:
```csharp
var profileMatch = string.IsNullOrEmpty(profileIdPattern) || profileIdPattern == "*" ||
                   string.IsNullOrEmpty(message.ProfileId) ||
                   profileIdPattern == message.ProfileId;
```

This means:
- Handlers registered without a profileId parameter receive ALL events (profile-scoped and global)
- Handlers registered with a specific profileId only receive events for that profile
- Global events (`ProfileId = null`) are matched by handlers that don't specify a profile filter

## Service Registration

### Global Services (ApplicationHost)

```csharp
// Register once at application startup
services.AddSingleton<IEventBus, EventBus>();
```

### Profile-Scoped Services (ProfileContext)

```csharp
// Automatically registered for each profile context
public static IServiceCollection AddContextServices(
    this IServiceCollection services,
    string profileId)
{
    // Register ProfileContext
    services.AddSingleton<IProfileContext>(new ProfileContext(profileId));

    // Register ProfileEventBus (auto-injects profileId)
    services.AddSingleton<IProfileEventBus, ProfileEventBus>();

    return services;
}
```

## Event Flow Examples

### Example 1: Profile-Scoped Event

```csharp
// 1. Profile-scoped service emits event
public class ModService
{
    private readonly IProfileEventBus _eventBus;  // Profile: "abc-123"

    public async Task LoadModAsync(string id)
    {
        // ProfileId "abc-123" automatically added
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LOADED, new { id });
    }
}

// 2. EventBus receives: { Module: "MOD", Type: "MOD_LOADED", ProfileId: "abc-123" }

// 3. Handlers are evaluated:
// ✅ Handler A: ("MOD", "MOD_LOADED", "*") → MATCHES (wildcard)
// ✅ Handler B: ("MOD", "MOD_LOADED", "abc-123") → MATCHES (exact)
// ❌ Handler C: ("MOD", "MOD_LOADED", "xyz-456") → NO MATCH (different profile)
```

### Example 2: Global Event

```csharp
// 1. Global service emits event
public class SystemService
{
    private readonly IEventBus _eventBus;

    public async Task StartAsync()
    {
        // No ProfileId
        await _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.APPLICATION_STARTED);
    }
}

// 2. EventBus receives: { Module: "SYSTEM", Type: "APPLICATION_STARTED", ProfileId: null }

// 3. Handlers are evaluated:
// ✅ Handler A: ("SYSTEM", "APPLICATION_STARTED") → MATCHES (global handler, no profile filter)
// ✅ Handler B: ("SYSTEM", "APPLICATION_STARTED", "abc-123") → NO MATCH (profile-filtered handler, event is global)
// ✅ Handler C: RegisterHandlerForAll() → MATCHES (wildcard handler)
```

## Migration Guide

### For Profile-Scoped Services

**Before:**
```csharp
public class ModService
{
    private readonly IEventBus _eventBus;
    private readonly IProfileContext _profileContext;

    public async Task LoadModAsync(string id)
    {
        // ❌ OLD: Manually pass profileId
        await _eventBus.EmitAsync(
            ModuleNames.MOD,
            ModEvents.MOD_LOADED,
            new { id },
            _profileContext.ProfileId);  // Manual profileId
    }
}
```

**After:**
```csharp
public class ModService
{
    private readonly IProfileEventBus _eventBus;  // ✨ Changed to IProfileEventBus

    public async Task LoadModAsync(string id)
    {
        // ✅ NEW: ProfileId auto-injected
        await _eventBus.EmitAsync(
            ModuleNames.MOD,
            ModEvents.MOD_LOADED,
            new { id });  // No manual profileId needed!
    }
}
```

### For Global Services

No changes needed! Global services continue using `IEventBus` as before:

```csharp
public class SystemService
{
    private readonly IEventBus _eventBus;  // Still use IEventBus

    public async Task StartAsync()
    {
        await _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.APPLICATION_STARTED);
    }
}
```

## Benefits

1. **Less Boilerplate**: Profile-scoped services don't need to manually add profileId
2. **Type Safety**: IProfileEventBus interface ensures correct usage
3. **Separation of Concerns**: Profile context handled automatically
4. **Backward Compatible**: Global services unchanged
5. **Flexible Filtering**: Handlers can listen to specific profiles or all profiles
6. **Performance**: Event cache handles profileId patterns efficiently

## Technical Details

### EventBus Caching

The EventBus caches event matching results using a cache key that includes profileId:

```csharp
// Global event cache key
var eventId = $"{message.Module}.{message.Type}";  // e.g., "SYSTEM.APPLICATION_STARTED"

// Profile-scoped event cache key
var eventId = $"{message.Module}.{message.Type}.{message.ProfileId}";  // e.g., "MOD.MOD_LOADED.abc-123"
```

This ensures efficient lookups without re-evaluating patterns for every event.

### ProfileEventBus Implementation

```csharp
public class ProfileEventBus : IProfileEventBus
{
    private readonly IEventBus _globalEventBus;
    private readonly IProfileContext _profileContext;

    public async Task EmitAsync(string module, string type, object? payload = null)
    {
        // Automatically inject profileId from context
        await _globalEventBus.EmitAsync(module, type, payload, _profileContext.ProfileId);
    }
}
```

The `ProfileEventBus` is a thin wrapper that:
1. Receives events from profile-scoped services
2. Injects ProfileId from ProfileContext
3. Forwards to global EventBus

This maintains the single EventBus instance (singleton) while providing profile-specific API.

## Future Enhancements

**Optional: Profile-Filtered IPC Forwarding**

Currently, `EventBusIpcBridge` forwards ALL events to ALL webviews. A future enhancement could create profile-specific bridges that only forward events for the active profile in each webview window:

```csharp
// Future: Profile-specific event bridge
public class ProfileEventBusIpcBridge : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IpcCommunicationHandler _ipc;
    private readonly string _profileId;

    public void Init()
    {
        // Only forward events for this profile
        _eventBus.RegisterHandlerForAll(async (message) =>
        {
            // Filter to only forward events for this profile
            if (message.ProfileId == _profileId)
            {
                await ForwardEventToFrontend(message);
            }
        });
    }
}
```

This would allow multi-window scenarios where each window shows a different profile and only receives events for that profile.
