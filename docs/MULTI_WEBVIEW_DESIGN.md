# Multi-WebView Architecture Design

## Overview

Refactor ApplicationHost to support multiple WebView windows using a session-based architecture. Each window (webview) becomes an isolated session with its own IPC handlers, event bridges, and drop zones, while sharing the same backend services.

## Current Architecture (Single WebView)

```
ApplicationHost
├── Form (main window)
├── WebView2 (single)
├── WebViewInitializer
├── IpcCommunicationHandler
├── EventBusIpcBridge
├── MessageDispatcher
├── DropZoneManager
└── ServiceProvider (shared)
```

## New Architecture (Multi-WebView)

```
ApplicationHost
├── WebViewSessionManager
│   ├── Session["main"]
│   │   ├── Form
│   │   ├── WebView2
│   │   ├── WebViewSession
│   │   │   ├── Initializer
│   │   │   ├── Ipc
│   │   │   ├── EventBridge
│   │   │   ├── Dispatcher
│   │   │   └── DropZone
│   │
│   ├── Session["secondary"] (future)
│   │   └── ... (same structure)
│
└── ServiceProvider (shared across all sessions)
```

## Key Components

### 1. WebViewSession (DONE ✅)
**Location:** `Infrastructure/WebView/WebViewSession.cs`

Encapsulates everything needed for a single WebView instance:
- `SessionId` - Unique identifier
- `WebView` - The WebView2 control
- `Ipc` - IPC handler for this session
- `DropZone` - Drop zone manager for this session
- `EventBridge` - Event forwarding for this session
- `Dispatcher` - Message dispatcher for this session
- `Initializer` - WebView initializer for this session

**Lifecycle:**
1. Created by factory function
2. `StartAsync()` - Initialize and navigate
3. `Dispose()` - Clean up resources

### 2. WebViewSessionManager (DONE ✅)
**Location:** `Infrastructure/WebView/WebViewSessionManager.cs`

Manages all WebView sessions:
- `Create(sessionId, factory)` - Create new session
- `TryGet(sessionId, out session)` - Get existing session
- `Remove(sessionId)` - Dispose and remove session
- `BroadcastNotification(module, type, payload)` - Send to all sessions
- `Sessions` - Collection of all active sessions

### 3. ApplicationHost Updates (TODO)

#### Current Issues:
- Tightly coupled to single WebView/Form
- IPC handler, event bridge, etc. are singleton fields
- No concept of sessions

#### Required Changes:

**A. Replace Singleton Fields:**
```csharp
// ❌ REMOVE (single-session fields)
private WebView2 _webView = null!;
private WebViewInitializer _webViewInitializer = null!;
private IpcCommunicationHandler _ipcHandler = null!;
private EventBusIpcBridge _eventBridge = null!;
private MessageDispatcher _messageDispatcher = null!;
private DropZoneManager _dropZoneManager = null!;

// ✅ ADD (session manager)
private WebViewSessionManager _sessionManager = null!;
private const string MAIN_SESSION_ID = "main";
```

**B. Update CreateMainForm:**
- Still create the main form
- Create WebView2 control
- But DON'T initialize WebView-specific components yet
  (move to session creation)

**C. Update OnFormLoad:**
- Create WebViewSessionManager
- Create main session using session manager
- Call `StartAsync()` on main session
- Keep reference to main session for convenience

**D. Add Session Lifecycle:**
```csharp
private async Task CreateMainSessionAsync()
{
    var session = _sessionManager.Create(MAIN_SESSION_ID, () =>
    {
        // Get current form's WebView2
        var webView = _webView; // from CreateMainForm
        var schemeHandler = _serviceProvider.GetRequiredService<ICustomSchemeHandler>();

        return new WebViewSession(
            MAIN_SESSION_ID,
            webView,
            _logger,
            _serviceProvider,
            schemeHandler,
            _mainForm,
            _profileRouter,
            ConfigureMessagePipeline  // Reuse same pipeline config
        );
    });

    await session.StartAsync();
    return session;
}
```

**E. Update Event Handling:**
```csharp
// Instead of sending to single IPC handler:
// _ipcHandler.SendNotification(module, type, payload);

// Broadcast to all sessions:
_sessionManager.BroadcastNotification(module, type, payload);
```

**F. Future: Multi-Window Support:**
```csharp
public async Task<WebViewSession> CreateSecondaryWindowAsync(string windowId)
{
    // Create new form
    var secondaryForm = new OptimizedForm
    {
        Text = $"D3dxSkinManager - {windowId}",
        Width = 1200,
        Height = 800
    };

    // Create WebView2
    var webView = new WebView2 { Dock = DockStyle.Fill };
    secondaryForm.Controls.Add(webView);

    // Create session
    var session = _sessionManager.Create(windowId, () =>
        new WebViewSession(
            windowId,
            webView,
            _logger,
            _serviceProvider,
            _serviceProvider.GetRequiredService<ICustomSchemeHandler>(),
            secondaryForm,
            _profileRouter,
            ConfigureMessagePipeline
        ));

    await session.StartAsync();
    secondaryForm.Show();

    return session;
}
```

## Implementation Steps

### Phase 1: Refactor ApplicationHost for Single Session ✅
1. Add WebViewSessionManager field
2. Move WebView component initialization to session factory
3. Replace direct IPC calls with session manager broadcasts
4. Test single session works identically

### Phase 2: Session Lifecycle Management
1. Handle form close → session disposal
2. Handle session errors
3. Add session recovery logic

### Phase 3: Enable Multi-Window (Future)
1. Add `CreateSecondaryWindowAsync` method
2. Add window management UI
3. Add session-specific routing (target specific windows)

## Benefits

### Immediate (Phase 1):
- **Cleaner separation**: Each session is self-contained
- **Easier testing**: Session can be mocked/tested independently
- **Better resource management**: Dispose disposes everything

### Future (Phase 2-3):
- **Multi-window support**: Easy to add new windows
- **Window-specific features**: Each window can have different purposes
- **Parallel workflows**: Multiple tasks in different windows

## Migration Checklist

- [x] Update ApplicationHost fields (remove singletons, add session manager)
- [x] Move WebView initialization to session factory
- [x] Update OnFormLoad to use session creation
- [x] Replace direct IPC calls with session.Ipc references
- [x] Update FormClosed handler to dispose session
- [x] Fix ConfigureMessagePipeline to accept dispatcher and session parameters
- [x] Fix WebViewSession.Dispose (remove non-existent Dispose calls)
- [x] Fix ModFileService.cs missing IProgressReporter namespace
- [x] Build successful
- [ ] Test single window still works correctly (runtime testing)
- [ ] Document new architecture in AI_GUIDE.md

## Constants to Add

```csharp
// ApplicationHost.cs
public static class SessionIds
{
    public const string MAIN = "main";
    public const string SECONDARY_PREFIX = "window_";
}
```

## Disposal Pattern

```csharp
// ApplicationHost
private void OnFormClosed(object? sender, FormClosedEventArgs e)
{
    try
    {
        // Dispose all sessions
        _sessionManager?.Remove(MAIN_SESSION_ID);

        // Save window state
        _windowStateService?.SaveWindowStateAsync(...);

        // Dispose service provider
        _serviceProvider?.Dispose();
    }
    catch (Exception ex)
    {
        _logger?.Error($"Error during shutdown: {ex.Message}", "Host", ex);
    }
}
```

## Error Handling

Each session should handle its own errors gracefully without crashing the application:

```csharp
// In WebViewSession.StartAsync()
try
{
    await Initializer.InitializeAsync();
    Ipc.Initialize();
    Dispatcher.Initialize();
    EventBridge.Initialize();
    Initializer.NavigateToApp();
}
catch (Exception ex)
{
    _logger.Error($"[{SessionId}] Failed to start session: {ex.Message}", "Host", ex);
    throw; // Let caller handle
}
```

## Testing Strategy

1. **Unit Tests**: WebViewSession creation and disposal
2. **Integration Tests**: Session manager add/remove/broadcast
3. **Manual Tests**:
   - Open app → verify single window works
   - Close app → verify clean shutdown
   - (Future) Open secondary window → verify independent operation

## Shared vs Per-Session Architecture

### Shared Across All Sessions (via ServiceProvider)

All services registered as singletons in DI are shared across all WebView sessions:

**Core Services (all singleton):**
- `IEventBus` - Global event bus for backend events
- `ILogHelper` - Centralized logging
- `IFileHelper`, `IHashHelper`, `IArchiveHelper` - File operations
- `IGlobalPathService` - Application-level paths
- `ICustomSchemeHandler` - app:// URL handling
- `IMemoryCache` - Application-wide caching
- `IPerformanceMonitor` - Performance tracking

**Module Services (all singleton):**
- All repositories (ModRepository, ProfileRepository, etc.)
- All business services (ModService, ProfileService, etc.)
- TaskQueue system (TaskQueueManager, TaskExecutor, etc.)

**Infrastructure:**
- `ProfileServiceRouter` - Shared profile routing logic
- `ServiceProvider` - The DI container itself

### Per-Session (Isolated per WebView)

Each WebView session has its own instances:

- `IpcCommunicationHandler` - IPC messages for this specific window
- `EventBusIpcBridge` - Subscribes to global EventBus, forwards to this window only
- `MessageDispatcher` - Message routing for requests from this window
- `DropZoneManager` - Drop zones for this window's UI
- `WebViewInitializer` - WebView2 initialization for this window

### Event Flow Pattern

**Backend → Frontend:**
1. Service emits event via shared `IEventBus.EmitAsync()`
2. Each session's `EventBusIpcBridge` receives event (wildcard subscription)
3. Each bridge forwards to its own `IpcCommunicationHandler`
4. Each window receives the event independently

**Frontend → Backend:**
1. Window sends IPC request via its `IpcCommunicationHandler`
2. Session's `MessageDispatcher` routes to appropriate handler
3. Handler uses shared services (repositories, etc.)
4. Response sent back to requesting window only

This ensures:
- ✅ All windows see backend events (via EventBus broadcast)
- ✅ All windows share the same data (via singleton services)
- ✅ Each window has independent UI communication (via per-session IPC)

