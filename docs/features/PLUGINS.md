# Plugin System Documentation

The D3dxSkinManager plugin system allows extending functionality without modifying core code. Plugins can work on both backend (.NET) and frontend (React) sides.

## Table of Contents

1. [Overview](#overview)
2. [Backend Plugins (.NET)](#backend-plugins-net)
3. [Frontend Plugins (React)](#frontend-plugins-react)
4. [Plugin Communication](#plugin-communication)
5. [Example Plugins](#example-plugins)
6. [Development Guide](#development-guide)
7. [Best Practices](#best-practices)

---

## Overview

The plugin system provides:

- **Event-driven architecture** - Plugins respond to system events (mod loaded, imported, deleted, etc.)
- **Full service access** - Plugins can access all core services via dependency injection
- **Custom IPC messages** - Plugins can handle custom messages from frontend
- **Frontend extensions** - React plugins can add custom UI components and tabs
- **Isolated data storage** - Each plugin gets its own data directory

### Plugin Types

| Type | Description | Example Use Cases |
|------|-------------|-------------------|
| **Backend Plugin** | .NET class implementing `IPlugin` | Custom mod processors, logging, background tasks |
| **Message Handler Plugin** | Backend plugin implementing `IMessageHandlerPlugin` | Custom API endpoints, data providers |
| **Service Plugin** | Backend plugin implementing `IServicePlugin` | Register custom services in DI container |
| **UI Plugin** | React plugin implementing `UIPlugin` | Custom tabs, mod actions, dialogs |
| **Action Plugin** | React plugin implementing `ActionPlugin` | Custom context menu items for mods |

---

## Backend Plugins (.NET)

### Basic Plugin Structure

```csharp
using D3dxSkinManager.Plugins;

public class MyPlugin : IPlugin
{
    public string Id => "com.example.myplugin";
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Description => "Example plugin";
    public string Author => "Your Name";

    public async Task InitializeAsync(IPluginContext context)
    {
        // Access services
        var modFacade = context.ModFacade;
        var repository = context.ModRepository;

        // Register event handlers
        context.RegisterEventHandler(PluginEventType.ModLoaded, OnModLoaded);

        // Get plugin data directory
        var dataPath = context.GetPluginDataPath(Id);

        context.Log(LogLevel.Info, $"[{Name}] Initialized");
    }

    public async Task ShutdownAsync()
    {
        // Cleanup
    }

    private async Task OnModLoaded(PluginEventArgs args)
    {
        var sha = args.Data?.Sha;
        // Handle event
    }
}
```

### Available Events

Backend plugins can listen to these events:

- `ApplicationStarted` - App started
- `ApplicationShutdown` - App shutting down
- `ModLoaded` - Mod was loaded into game
- `ModUnloaded` - Mod was unloaded
- `ModImported` - New mod imported
- `ModDeleted` - Mod deleted
- `ModsRefreshed` - Mod list refreshed
- `CustomEvent` - Custom event from other plugins

### Plugin Context API

The `IPluginContext` provides access to:

```csharp
// Core services
context.ModFacade           // Mod operations
context.ModRepository       // Data access
context.FileService         // File operations
context.ClassificationService  // Mod classification
context.ImageService        // Image processing

// Plugin features
context.GetPluginDataPath(pluginId)  // Get plugin data directory
context.GetService<T>()              // Get any DI service
context.RegisterEventHandler(...)    // Subscribe to events
context.UnregisterEventHandler(...)  // Unsubscribe
context.EmitEventAsync(...)          // Emit custom events
context.Log(level, message, ex)      // Log messages
```

### Message Handler Plugin

Handle custom IPC messages from frontend:

```csharp
public class MyPlugin : IMessageHandlerPlugin
{
    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "MY_CUSTOM_ACTION", "GET_CUSTOM_DATA" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        return request.Type switch
        {
            "MY_CUSTOM_ACTION" => await HandleCustomAction(request),
            "GET_CUSTOM_DATA" => await GetCustomData(request),
            _ => MessageResponse.CreateError(request.Id, "Unknown message type")
        };
    }

    private async Task<MessageResponse> HandleCustomAction(MessageRequest request)
    {
        // Access request.Payload for data
        var result = await DoSomething();
        return MessageResponse.CreateSuccess(request.Id, result);
    }
}
```

### Service Plugin

Register custom services in DI container:

```csharp
public class MyPlugin : IServicePlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMyCustomService, MyCustomService>();
        services.AddTransient<IMyHelper, MyHelper>();
    }

    public async Task InitializeAsync(IPluginContext context)
    {
        // Services are now available
        var myService = context.GetService<IMyCustomService>();
    }
}
```

### Project Setup

1. Create a new .NET class library project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\D3dxSkinManager\D3dxSkinManager.csproj" />
  </ItemGroup>
</Project>
```

2. Build the plugin:

```bash
dotnet build
```

3. Copy the compiled DLL to `{AppData}/plugins/` directory

---

## Frontend Plugins (React)

### Basic UI Plugin

```typescript
import type { UIPlugin, PluginContext } from '../plugins/PluginTypes';

export class MyUIPlugin implements UIPlugin {
  id = 'com.example.myuiplugin';
  name = 'My UI Plugin';
  version = '1.0.0';
  description = 'Example UI plugin';
  author = 'Your Name';
  tabLabel = 'My Tab';
  tabIcon = 'AppstoreOutlined';

  async initialize(context: PluginContext): Promise<void> {
    // Register event handlers
    context.registerEventHandler('MOD_LOADED', this.onModLoaded);
  }

  async cleanup(): Promise<void> {
    // Cleanup
  }

  private onModLoaded = (args: PluginEventArgs) => {
    console.log('Mod loaded:', args.data);
  };

  renderTab = () => {
    return (
      <div style={{ padding: '24px' }}>
        <h2>My Custom Tab</h2>
        <p>Custom UI content here</p>
      </div>
    );
  };
}
```

### Action Plugin

Add custom actions to mod context menus:

```typescript
import type { ActionPlugin, ModInfo, ModAction } from '../plugins/PluginTypes';

export class MyActionPlugin implements ActionPlugin {
  id = 'com.example.myactionplugin';
  name = 'My Action Plugin';
  version = '1.0.0';
  description = 'Adds custom mod actions';
  author = 'Your Name';

  async initialize(context: PluginContext): Promise<void> {
    console.log('[MyActionPlugin] Initialized');
  }

  async cleanup(): Promise<void> {}

  getModActions(mod: ModInfo): ModAction[] {
    return [
      {
        key: 'custom-action-1',
        label: 'Custom Action',
        icon: 'StarOutlined',
        onClick: async (mod) => {
          console.log('Custom action for mod:', mod.name);
          // Perform action
        }
      }
    ];
  }
}
```

### Plugin Registration

In your app initialization:

```typescript
import { usePluginSystem } from './plugins/usePluginSystem';
import { MyUIPlugin } from './plugins/examples/MyUIPlugin';

function App() {
  const pluginSystem = usePluginSystem();

  useEffect(() => {
    if (pluginSystem.initialized) {
      // Register plugins
      const context = {
        modService: modService,
        registerEventHandler: pluginSystem.registry.registerEventHandler,
        unregisterEventHandler: pluginSystem.registry.unregisterEventHandler,
        emitEvent: pluginSystem.emitCustomEvent
      };

      pluginSystem.registry.register(new MyUIPlugin(), context);
    }
  }, [pluginSystem.initialized]);

  return <div>App content</div>;
}
```

---

## Plugin Communication

### Backend to Frontend

Backend plugins can send events that frontend plugins receive:

**Backend:**
```csharp
// Emit custom event
await context.EmitEventAsync("MY_CUSTOM_EVENT", new { data = "value" });
```

**Frontend:**
```typescript
context.registerEventHandler('CUSTOM_EVENT', (args) => {
  if (args.eventName === 'MY_CUSTOM_EVENT') {
    console.log('Received:', args.data);
  }
});
```

### Frontend to Backend

Frontend can call custom backend message handlers:

**Frontend:**
```typescript
import { photinoService } from '../services/photino';

const result = await photinoService.sendMessage('MY_CUSTOM_ACTION', {
  param1: 'value1',
  param2: 'value2'
});
```

**Backend:**
```csharp
public class MyPlugin : IMessageHandlerPlugin
{
    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "MY_CUSTOM_ACTION" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Access request.Payload
        var payload = request.Payload as JsonElement?;
        var param1 = payload?.GetProperty("param1").GetString();

        return MessageResponse.CreateSuccess(request.Id, new { result = "success" });
    }
}
```

---

## Example Plugins

### Backend: ModLoggerPlugin

Located at: `D3dxSkinManager.ExamplePlugin/ModLoggerPlugin.cs`

**Features:**
- Logs all mod operations to console and file
- Handles custom messages: `GET_MOD_LOG`, `CLEAR_MOD_LOG`
- Demonstrates event handling and file I/O

**Usage:**
1. Build the plugin project
2. Copy DLL to `{AppData}/plugins/`
3. Restart application
4. Check `{AppData}/plugins/com.d3dxskinmanager.modlogger/mod_operations.log`

### Frontend: ModLogViewerPlugin

Located at: `D3dxSkinManager.Client/src/plugins/examples/ExamplePlugin.tsx`

**Features:**
- Adds "Mod Logs" tab to UI
- Displays logs from ModLoggerPlugin
- Can refresh and clear logs
- Demonstrates backend-frontend communication

**Usage:**
1. Register plugin in App.tsx
2. Navigate to "Mod Logs" tab
3. View and manage mod operation logs

---

## Development Guide

### Step 1: Create Backend Plugin

1. Create new .NET class library project
2. Reference main D3dxSkinManager project
3. Implement `IPlugin` or `IMessageHandlerPlugin`
4. Build and test

### Step 2: Create Frontend Plugin (Optional)

1. Create TypeScript file implementing `UIPlugin` or `ActionPlugin`
2. Implement required methods
3. Register plugin in App.tsx

### Step 3: Install Plugin

**Backend:**
```bash
# Build plugin
dotnet build MyPlugin.csproj

# Copy to plugins directory
copy bin\Debug\net8.0\MyPlugin.dll "{AppData}\plugins\"
```

**Frontend:**
```typescript
// Import and register in App.tsx
import { MyPlugin } from './plugins/MyPlugin';

pluginRegistry.register(new MyPlugin(), context);
```

### Step 4: Test

1. Restart application
2. Check console for plugin initialization messages
3. Test plugin functionality
4. Check logs for errors

---

## Best Practices

### Do's ✅

- **Use dependency injection** - Access services via context, not static instances
- **Handle errors gracefully** - Catch and log exceptions
- **Clean up resources** - Implement proper cleanup in `ShutdownAsync()`
- **Use plugin data directory** - Store files in `context.GetPluginDataPath(Id)`
- **Log important events** - Use `context.Log()` for debugging
- **Version your plugins** - Use semantic versioning
- **Document your plugin** - Include README with usage instructions
- **Test thoroughly** - Test with different mod types and edge cases

### Don'ts ❌

- **Don't block main thread** - Use async/await for long operations
- **Don't modify core files** - Use plugin APIs only
- **Don't assume plugin order** - Plugins may load in any order
- **Don't store sensitive data** - No passwords or API keys in code
- **Don't use global state** - Store state in plugin instance
- **Don't ignore errors** - Always handle exceptions
- **Don't hardcode paths** - Use context methods for paths

### Performance Tips

- Use events sparingly - Don't register too many handlers
- Lazy load resources - Load only when needed
- Cache expensive operations - Store results if used frequently
- Dispose resources properly - Implement IDisposable if needed
- Minimize IPC calls - Batch operations when possible

---

## Plugin Directory Structure

```
{AppData}/
└── plugins/
    ├── MyPlugin.dll                    # Backend plugin DLL
    ├── com.example.myplugin/          # Plugin data directory
    │   ├── config.json
    │   └── data.db
    └── AnotherPlugin.dll
```

---

## Troubleshooting

### Plugin Not Loading

1. Check plugin DLL is in correct directory
2. Verify DLL references correct D3dxSkinManager version
3. Check console for error messages
4. Ensure plugin implements IPlugin interface correctly

### Event Not Firing

1. Verify event is registered in `InitializeAsync()`
2. Check event type matches exactly
3. Ensure event bus is initialized
4. Add logging to confirm registration

### IPC Message Not Working

1. Verify message type is in `GetHandledMessageTypes()`
2. Check frontend sends correct message format
3. Add logging in both frontend and backend
4. Verify message routing in Program.cs

### Build Errors

1. Ensure .NET 10 SDK is installed
2. Check project references are correct
3. Restore NuGet packages: `dotnet restore`
4. Clean and rebuild: `dotnet clean && dotnet build`

---

## API Reference

See inline documentation in:
- [IPlugin.cs](../D3dxSkinManager/Plugins/IPlugin.cs)
- [IPluginContext.cs](../D3dxSkinManager/Plugins/IPluginContext.cs)
- [IMessageHandlerPlugin.cs](../D3dxSkinManager/Plugins/IMessageHandlerPlugin.cs)
- [PluginTypes.ts](../D3dxSkinManager.Client/src/plugins/PluginTypes.ts)

---

## Support

For questions or issues:
1. Check this documentation
2. Review example plugins
3. Check console logs for errors
4. Create an issue on GitHub

---

**Last Updated:** 2026-02-17
**Plugin System Version:** 1.0.0
