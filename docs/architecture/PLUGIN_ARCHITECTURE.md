# Plugin Architecture

## Overview

The Plugin system provides a flexible, event-driven architecture for extending application functionality without modifying core code. Plugins can subscribe to events, send messages to modules, and handle custom IPC messages from the frontend.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Frontend                               │
│  ┌─────────────┐                                                │
│  │ Plugin Button│ → PLUGIN/INVOKE                               │
│  └─────────────┘   { pluginId, messageType, payload }          │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MessageDispatcher (Singleton)                  │
│  - Routes all IPC messages through middleware pipeline          │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      PluginFacade                               │
│  Module: PLUGIN                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Routes:                                                  │   │
│  │ - GET_ALL    → List all plugins                         │   │
│  │ - INVOKE     → Route to specific plugin                 │   │
│  │   ├─ Validate pluginId                                  │   │
│  │   ├─ Validate messageType                               │   │
│  │   └─ Call plugin.HandleMessageAsync()                   │   │
│  └─────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      IPlugin Implementation                     │
│                                                                 │
│  IPluginContext (Hub):                                          │
│  ┌───────────────────────────────────────────────────────┐     │
│  │ EventBus          → Subscribe/Emit events             │     │
│  │ MessageDispatcher → Send messages to modules          │     │
│  │ Utilities         → Logging, data path                │     │
│  └───────────────────────────────────────────────────────┘     │
│                                                                 │
│  Plugin Capabilities:                                           │
│  ├─ Subscribe to system events (MOD_LOADED, etc.)              │
│  ├─ Send messages to any module (MOD, PROFILE, etc.)           │
│  ├─ Handle custom IPC messages (OPEN_UI, CLOSE_UI, etc.)       │
│  └─ Open custom WinForm/WebView UI windows                     │
└─────────────────────────────────────────────────────────────────┘
```

## Plugin Type System

**Current Design: Single Plugin Type with Capabilities**

All plugins implement the same `IPlugin` interface. There is **ONE** plugin type, not multiple types (no ServicePlugin vs MessageHandlerPlugin distinction). Plugins declare their capabilities through:

1. **Message Handling Capability**: If `GetHandledMessageTypes()` returns message types, the plugin has "MessageHandler" capability
2. **Future Capabilities** (reserved):
   - "EventInterceptor" - Intercept and modify events before they reach handlers
   - "IpcInterceptor" - Intercept and modify IPC messages before routing

The backend analyzes each plugin and exposes a `Capabilities` array in the `PluginInfo` DTO sent to frontend:

```csharp
// PluginInfo returned by PLUGIN/GET_ALL
public class PluginInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public bool IsEnabled { get; set; }
    public List<string> Capabilities { get; set; }  // e.g., ["MessageHandler"]
}
```

**Frontend Display**: The frontend PluginsView shows capabilities as tags, making it clear what each plugin can do without needing type categorization.

## Core Interfaces

### IPlugin

The base interface for all plugins. Unified interface with all capabilities built-in.

```csharp
public interface IPlugin : IAsyncDisposable
{
    // Metadata
    string Id { get; }          // Unique: "com.example.myplugin"
    string Name { get; }        // Display name
    string Version { get; }     // Semantic version
    string Description { get; } // Plugin description
    string Author { get; }      // Author/organization

    // Lifecycle (C# naming conventions)
    Task InitAsync(IPluginContext context);
    // ValueTask DisposeAsync() from IAsyncDisposable

    // Message Handling (all plugins can handle messages)
    IEnumerable<string> GetHandledMessageTypes();
    Task<IpcResponse> HandleMessageAsync(IpcRequest request);
}
```

### IPluginContext

Provides plugins with access to core services through a simplified hub.

```csharp
public interface IPluginContext
{
    // Communication Hub
    IMessageDispatcher MessageDispatcher { get; } // Send messages to modules
    IEventBus EventBus { get; }                   // Subscribe/emit events

    // Utilities
    string GetPluginDataPath(string pluginId);

    // Helper methods for event subscription (delegates to EventBus)
    string RegisterEventHandler(string modulePattern, string typePattern,
                               Func<EventMessage, Task> handler);
    void UnregisterEventHandler(string registrationId);

    /// <summary>NOTE: Plugin events are NOT currently used. Reserved for future cross-plugin communication.</summary>
    Task EmitEventAsync(string eventType, object? payload = null);

    void Log(LogLevel level, string message, Exception? exception = null);
}
```

## Plugin Capabilities

### 1. Event Subscription

Subscribe to system events from any module using the helper methods or EventBus directly:

```csharp
public async Task InitAsync(IPluginContext context)
{
    // Option 1: Use helper methods (simple pattern matching)
    context.RegisterEventHandler("MOD", "LOADED", OnModLoaded);
    context.RegisterEventHandler("MOD", "UNLOADED", OnModUnloaded);
    context.RegisterEventHandler("MOD", "*", OnAnyModEvent);
    context.RegisterEventHandler("*", "*", OnAnyEvent);

    // Option 2: Use EventBus directly (more powerful registration options)
    context.EventBus.RegisterHandler("MOD", "LOADED", "profile-123", OnModLoadedInProfile);
    context.EventBus.RegisterHandlerForModule("MOD", OnAllModEvents);
    context.EventBus.RegisterHandlerForAll(OnAnyEvent);
}

private async Task OnModLoaded(EventMessage e)
{
    var id = e.Payload?.GetProperty("Sha").GetString();
    _logger.Info($"Mod loaded: {id}");
}
```

### 2. Sending Messages to Modules

Call any module without direct dependencies:

```csharp
public async Task ProcessModData(string profileId)
{
    // Get all mods
    var mods = await _context.MessageDispatcher.SendAsync<List<ModInfo>>(
        "MOD", "GET_ALL", profileId
    );

    // Get profile info
    var profile = await _context.MessageDispatcher.SendAsync<ProfileDto>(
        "PROFILE", "GET_BY_ID", payload: new { id = profileId }
    );

    // Save setting
    await _context.MessageDispatcher.SendAsync(
        "SETTING", "SAVE",
        payload: new { key = "plugin.data", value = "test" }
    );
}
```

### 3. Handling Frontend Messages

Handle custom IPC messages for UI operations:

```csharp
public IEnumerable<string> GetHandledMessageTypes()
    => new[] { "OPEN_UI", "CLOSE_UI", "GET_DATA", "EXPORT" };

public async Task<IpcResponse> HandleMessageAsync(IpcRequest request)
{
    return request.Type switch
    {
        "OPEN_UI" => OpenCustomUI(request),
        "CLOSE_UI" => CloseCustomUI(request),
        "GET_DATA" => GetPluginData(request),
        "EXPORT" => ExportData(request),
        _ => IpcResponse.CreateError(request.Id, "Unknown type")
    };
}

private IpcResponse OpenCustomUI(IpcRequest request)
{
    // Open WinForm or WebView window
    var form = new MyPluginForm();
    form.Show();

    return IpcResponse.CreateSuccess(request.Id,
        new { windowId = form.Handle.ToString() });
}
```

## Frontend Integration

### Listing Plugins

```typescript
// Get all loaded plugins
const response = await ipc.send("PLUGIN", "GET_ALL");
const plugins = response.data; // List<PluginInfo>

plugins.forEach(plugin => {
    logger.info(`${plugin.name} v${plugin.version}`);
    logger.info(`Capabilities: ${plugin.capabilities.join(", ")}`);
});
```

### Invoking Plugin Actions

```typescript
// Open plugin UI
const result = await ipc.send("PLUGIN", "INVOKE", {
    pluginId: "com.example.myplugin",
    messageType: "OPEN_UI",
    payload: {
        windowSize: "large",
        theme: "dark"
    }
});

if (result.success) {
    logger.info("Plugin UI opened:", result.data);
}
```

### Example: Plugin Button Component

```tsx
function PluginButton({ plugin }: { plugin: PluginInfo }) {
    const handleClick = async () => {
        await ipc.send("PLUGIN", "INVOKE", {
            pluginId: plugin.id,
            messageType: "OPEN_UI"
        });
    };

    return (
        <button onClick={handleClick}>
            Open {plugin.name}
        </button>
    );
}
```

## Plugin Lifecycle

### 1. Loading

```
Application Startup
    ↓
ProfileServerService.StartAsync()
    ↓
PluginLoader.LoadPluginsAsync()
    ├─ Scan plugins directory for .dll files
    ├─ Load assemblies
    ├─ Find IPlugin implementations
    ├─ Create instances
    └─ Register in PluginRegistry
```

### 2. Initialization

```
After Loading
    ↓
PluginLoader.InitPluginsAsync()
    ├─ Get all registered plugins
    ├─ For each plugin:
    │   ├─ Create IPluginContext
    │   ├─ Call plugin.InitAsync(context)
    │   └─ Plugin subscribes to events
    └─ Wait for all initializations
```

### 3. Runtime

```
Plugin Subscriptions Active
    ├─ Receives events via EventBus
    ├─ Sends messages via MessageDispatcher
    └─ Handles IPC messages from frontend

Frontend Interaction
    ├─ User clicks button
    ├─ PLUGIN/INVOKE → PluginFacade
    ├─ Routes to plugin
    └─ Plugin opens UI/performs action
```

### 4. Shutdown

```
Application Shutdown
    ↓
PluginLoader.DisposePluginsAsync()
    ├─ Get all plugins
    ├─ For each plugin:
    │   └─ await plugin.DisposeAsync() (IAsyncDisposable)
    └─ Wait for all disposals
```

## Plugin Development Guide

### Minimal Plugin Example

```csharp
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;
using D3dxSkinManager.Modules.Core.Models;

public class MinimalPlugin : IPlugin
{
    public string Id => "com.example.minimal";
    public string Name => "Minimal Plugin";
    public string Version => "1.0.0";
    public string Description => "A minimal example plugin";
    public string Author => "Your Name";

    private IPluginContext? _context;

    public async Task InitAsync(IPluginContext context)
    {
        _context = context;
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
    }

    public IEnumerable<string> GetHandledMessageTypes()
        => Array.Empty<string>(); // No message handling

    public Task<IpcResponse> HandleMessageAsync(IpcRequest request)
        => Task.FromResult(IpcResponse.CreateError(request.Id, "Not implemented"));

    public ValueTask DisposeAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Disposed");
        return ValueTask.CompletedTask;
    }
}
```

### Full-Featured Plugin Example

See [ModLoggerPlugin.cs](../../D3dxSkinManager.ExamplePlugin/ModLoggerPlugin.cs) for a complete example demonstrating:
- Event subscription
- Plugin data storage
- IPC message handling
- Logging

## File Structure

```
D3dxSkinManager/
├── Modules/
│   └── Plugin/
│       ├── IPlugin.cs                    # Main interface
│       ├── PluginFacade.cs               # IPC routing
│       ├── PluginServiceExtensions.cs    # DI registration
│       ├── Services/
│       │   ├── PluginContext.cs          # Hub implementation
│       │   ├── PluginLoader.cs           # Load/Init plugins
│       │   └── PluginRegistry.cs         # Plugin tracking
│       └── Models/
│           └── PluginInfo.cs             # DTO for frontend
│
├── Plugins/                               # Plugin installations
│   ├── com.example.plugin1/
│   │   └── Plugin1.dll
│   └── com.example.plugin2/
│       └── Plugin2.dll
│
└── D3dxSkinManager.ExamplePlugin/        # Example plugin project
    └── ModLoggerPlugin.cs
```

## Best Practices

### 1. Plugin ID Convention
Use reverse domain notation: `com.organization.pluginname`

### 2. Error Handling
Always wrap async operations in try-catch:

```csharp
public async Task InitAsync(IPluginContext context)
{
    try
    {
        // Initialization code
    }
    catch (Exception ex)
    {
        context.Log(LogLevel.Error, $"Init failed: {ex.Message}", ex);
        throw;
    }
}
```

### 3. Resource Cleanup
Implement proper async disposal:

```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        // Unregister handlers
        if (_registrationId != null)
            _context?.UnregisterEventHandler(_registrationId);

        // Close UI windows
        _form?.Close();

        // Save state
        await SaveStateAsync();
    }
    catch (Exception ex)
    {
        _context?.Log(LogLevel.Error, $"Dispose failed: {ex.Message}", ex);
    }
}
```

### 4. Use Plugin Data Directory
Store plugin-specific data in the designated directory:

```csharp
var dataPath = _context.GetPluginDataPath(Id);
var configFile = Path.Combine(dataPath, "config.json");
```

### 5. Defensive Programming
Validate all frontend inputs:

```csharp
public Task<IpcResponse> HandleMessageAsync(IpcRequest request)
{
    if (request.Payload == null)
        return Task.FromResult(IpcResponse.CreateError(
            request.Id, "Payload required"));

    // Process request...
}
```

## Related Documentation

- [EVENT_HUB_ARCHITECTURE.md](./EVENT_HUB_ARCHITECTURE.md) - Event system
- [MODULE_ARCHITECTURE.md](./MODULE_ARCHITECTURE.md) - Module structure
- [CURRENT_ARCHITECTURE.md](./CURRENT_ARCHITECTURE.md) - Overall architecture
