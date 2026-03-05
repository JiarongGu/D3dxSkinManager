# Screen Capture Tool

**Module:** Tool (profile-scoped)
**Last Updated:** 2026-03-05

## Overview

The Screen Capture Tool provides a desktop screen capture feature with customizable capture areas. It consists of:
- **Control Panel**: A secondary WebView2 window for managing capture operations
- **Border Overlay**: A transparent WinForms overlay showing the capture area boundary
- **Profile Storage**: Capture profiles stored per-profile in `profile.db`

## Architecture

### Components

1. **ScreenCaptureService** (Profile-scoped)
   - Manages capture operations and overlay display
   - Creates WinForms overlays on separate STA threads
   - Location: `D3dxSkinManager/Modules/Tool/ScreenCapture/Services/ScreenCaptureService.cs`

2. **SecondaryWindowService** (Profile-scoped)
   - Manages secondary WebView2 windows (control panels)
   - Tracks open windows per profile
   - Location: `D3dxSkinManager/Infrastructure/WebView/SecondaryWindowService.cs`

3. **ScreenCaptureProfileRepository** (Profile-scoped)
   - Stores capture profiles in profile-specific database
   - Location: `D3dxSkinManager/Modules/Tool/ScreenCapture/Repositories/ScreenCaptureProfileRepository.cs`

4. **ToolFacade** (Profile-scoped)
   - IPC message routing for screen capture commands
   - Location: `D3dxSkinManager/Modules/Tool/ToolFacade.cs`

### Frontend

- **ToolsView**: Main tools page with Screen Capture card
- **toolService**: IPC service for screen capture operations
- **Module**: `TOOL` (profile-scoped messages)

## Key Features

### 1. Toggle Control Panel

**Behavior:** Single click opens the control panel, second click closes it.

**IPC Message:** `SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL`

**Frontend Usage:**
```typescript
import { api } from '@/shared/services/ipc';

// Toggle control panel for current profile
await api.tool.toggleControlPanel(profileId);
```

**Backend Implementation:**
```csharp
public void ToggleCaptureControlPanel(string profileId)
{
    // Check if window already exists for this profile
    if (_windowService.HasWindowForProfile(profileId))
    {
        _windowService.CloseWindowForProfile(profileId);
        return;
    }

    // Create new window on STA thread
    // ...
}
```

### 2. Profile-Scoped Windows

Each profile can have its own control panel window. Windows are tracked by `profileId` in `SecondaryWindowService._openWindows` list.

### 3. Auto-Close on Profile Switch

When switching profiles, all secondary windows (including screen capture control panels) are automatically closed.

**Implementation:**
- `ProfileServiceRouter.CloseAllSecondaryWindows()` iterates through all profile-scoped services
- Called from `ApplicationHost` on `PROFILE:SWITCHED` event

```csharp
// ApplicationHost.cs
eventBus.Subscribe(ModuleNames.PROFILE, ProfileEvents.SWITCHED, async (eventMessage) =>
{
    _logger.Info("Received profile switched event, closing all secondary windows", "Host");
    _profileRouter.CloseAllSecondaryWindows();
    await Task.CompletedTask;
});
```

### 4. Border Overlay

A transparent WinForms overlay displays the capture area boundary:
- Resizable and draggable
- Sends `SCREEN_CAPTURE_BOUNDS_CHANGED` events (throttled to 100ms)
- Auto-closes when control panel window closes

## Data Model

### ScreenCaptureProfile

Stored in `profile.db` → `ScreenCaptureProfiles` table

```csharp
public class ScreenCaptureProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
```

**Database Location:** Per-profile database (not global)
- Uses `IProfilePathService` to get profile database path
- Follows same pattern as `ModRepository`

## IPC Messages

All messages use `TOOL` module with `SCREEN_CAPTURE_` prefix:

| Message Type | Description | Payload |
|--------------|-------------|---------|
| `SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL` | Toggle control panel window | None |
| `SCREEN_CAPTURE_SHOW_BORDER` | Show border overlay | `{x, y, width, height}` |
| `SCREEN_CAPTURE_HIDE_BORDER` | Hide border overlay | None |
| `SCREEN_CAPTURE_BOUNDS_CHANGED` | Overlay bounds changed (event) | `{x, y, width, height}` |
| `SCREEN_CAPTURE_GET_PROFILES` | Get all profiles | None |
| `SCREEN_CAPTURE_SAVE_PROFILE` | Save profile | `SaveScreenCaptureProfileRequest` |
| `SCREEN_CAPTURE_DELETE_PROFILE` | Delete profile | `{id}` |

## Threading Model

### WinForms Threading Requirements

Both control panel and overlay windows require STA threads:

```csharp
var thread = new Thread(() =>
{
    Application.SetHighDpiMode(HighDpiMode.SystemAware);
    var form = _windowService.CreateCaptureWindowAsync(profileId).GetAwaiter().GetResult();
    Application.Run(form); // Blocks until window closes
});

thread.SetApartmentState(ApartmentState.STA);
thread.IsBackground = false; // Keep app alive
thread.Start();
```

### WebView2 Threading

All `CoreWebView2` access must happen on the UI thread:

```csharp
// ✅ Correct - check on UI thread
if (_webView.InvokeRequired)
{
    _webView.BeginInvoke(() =>
    {
        if (_webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.PostWebMessageAsString(json);
        }
    });
}

// ❌ Wrong - accessing from background thread
if (_webView.CoreWebView2 == null) // Throws InvalidOperationException!
    return;
```

## Performance Optimizations

### Event Throttling

Bounds changed events are throttled to 100ms using the reusable `Throttle` utility:

```csharp
private readonly Throttle _boundsChangeThrottle = new Throttle(100);

form.BoundsChanged += (x, y, w, h) =>
{
    _boundsChangeThrottle.Execute(() =>
    {
        eventBus.EmitAsync("TOOL", "SCREEN_CAPTURE_BOUNDS_CHANGED",
            new { x, y, width, height }).Wait();
    });
};
```

**Utility Location:** `D3dxSkinManager/Modules/Core/Utilities/Throttle.cs`

## Service Registration

Screen capture services are registered as profile-scoped singletons:

```csharp
// ToolServiceExtensions.cs
public static IServiceCollection AddToolsServices(this IServiceCollection services)
{
    services.TryAddSingleton<IScreenCaptureProfileRepository, ScreenCaptureProfileRepository>();
    services.TryAddSingleton<IScreenCaptureService, ScreenCaptureService>();
    services.TryAddSingleton<ISecondaryWindowService, SecondaryWindowService>();
    services.TryAddSingleton<IToolFacade, ToolFacade>();

    return services;
}
```

Registered in `ProfileServiceRouter` during profile service creation.

## Window Lifecycle

### Opening Control Panel

1. User clicks Screen Capture tool in ToolsView
2. Frontend calls `api.tool.toggleControlPanel(profileId)`
3. Backend checks if window exists for profile
4. If not exists, creates new STA thread and WebView2 window
5. Window added to `_openWindows` list

### Closing Control Panel

1. User closes window or clicks tool again
2. Window's `FormClosing` event fires
3. Auto-closes associated border overlay
4. Removes from `_openWindows` list
5. Cleans up WebView session

### Profile Switch Cleanup

1. User switches profile
2. `PROFILE:SWITCHED` event emitted
3. `ProfileServiceRouter.CloseAllSecondaryWindows()` called
4. Iterates all profile service providers
5. Gets `ISecondaryWindowService` from each
6. Calls `CloseAllWindows()` on each service

## Common Patterns

### Creating Secondary Windows

```csharp
public async Task<Form?> CreateCaptureWindowAsync(string profileId)
{
    var form = new Form
    {
        Text = "Screen Capture",
        Size = new Size(400, 180),
        FormBorderStyle = FormBorderStyle.FixedToolWindow,
        TopMost = false
    };

    var webView = new WebView2 { Dock = DockStyle.Fill };
    form.Controls.Add(webView);

    var session = _sessionManager.Create(sessionId, () => new WebViewSession(...));

    _openWindows.Add((form, session, profileId));

    return form;
}
```

### Checking Window Existence

```csharp
public bool HasWindowForProfile(string profileId)
{
    return _openWindows.Any(w => w.ProfileId == profileId);
}
```

## Troubleshooting

### Control Panel Not Opening

**Symptoms:** No window appears when clicking Screen Capture tool

**Check:**
1. Console for errors in `ScreenCaptureService`
2. Verify profile ID is passed correctly
3. Check if previous window failed to close properly

**Fix:** Call `toggleControlPanel` again to close stuck window

### Thread Errors

**Symptoms:** `InvalidOperationException: CoreWebView2 can only be accessed from the UI thread`

**Cause:** Accessing WebView2 properties from background thread

**Fix:** Always check `InvokeRequired` and use `BeginInvoke` for WebView access

### Windows Not Closing on Profile Switch

**Symptoms:** Control panel remains open after switching profiles

**Check:**
1. `PROFILE:SWITCHED` event subscription in `ApplicationHost`
2. `ProfileServiceRouter.CloseAllSecondaryWindows()` implementation
3. Service registration in `AddToolsServices`

## Related Documentation

- [Profile Service Architecture](../architecture/PROFILE_SERVICE_ARCHITECTURE.md)
- [Module Architecture](../architecture/MODULE_ARCHITECTURE.md)
- [Workflows](../ai-assistant/WORKFLOWS.md)
