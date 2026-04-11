# Screen Capture Tool

**Module:** Tool (profile-scoped)
**Last Updated:** 2026-03-06

## Overview

The Screen Capture Tool provides a desktop screen capture feature with customizable capture areas. It consists of:
- **Control Panel**: A secondary WebView2 window for managing capture operations
- **Border Overlay**: A transparent WinForms overlay showing the capture area boundary
- **Profile Storage**: Capture profiles stored per-profile in `profile.db`
- **Theme/Language Sync**: Automatic synchronization of theme and language settings across all windows

## Architecture

### Components

1. **ScreenCaptureService** (Profile-scoped)
   - Manages capture operations and overlay display
   - Creates WinForms overlays on separate STA threads
   - Location: `D3dxSkinManager/Modules/Tool/ScreenCapture/Services/ScreenCaptureService.cs`

2. **SecondaryWindowService** (Profile-scoped)
   - Manages secondary WebView2 windows (generic for any window type)
   - Uses `ConcurrentDictionary<string, WindowEntry>` for thread-safe window tracking by name
   - Provides window-specific operations: `HasWindow(name)`, `CloseWindow(name)`
   - Stores window positions/sizes in profile configuration via `ProfileService`
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
    const string captureWindowName = "capture";

    // Check if capture window already exists (service is scoped to current profile)
    if (_windowService.HasWindow(captureWindowName))
    {
        _windowService.CloseWindow(captureWindowName);
        return;
    }

    // Create new window on STA thread
    // ...
}
```

### 2. Generic Window System

SecondaryWindowService provides a generic system for creating and managing multiple window types:

**Window Types:**
- `"capture"` - Screen capture control panel
- `"debug"` - Debug console (future)
- `"tools"` - Tool windows (future)

**Storage:**
- Window configurations stored in `ProfileConfiguration.Windows` dictionary
- Each window saves: `{x, y, width, height}`
- Managed via `ProfileService.UpdateWindowConfigurationAsync(profileId, windowName, x, y, width, height)`

**Architecture:**
- Service is profile-scoped (gets profileId from `IProfileContext`)
- Windows tracked by name in `ConcurrentDictionary<string, WindowEntry>`
- Thread-safe operations for concurrent access
- Auto-saves position/size on window close

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
    // PerMonitorV2 enables proper DPI awareness for 4K monitors
    // Windows automatically scales window dimensions based on monitor DPI
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    var form = _windowService.CreateCaptureWindowAsync(profileId).GetAwaiter().GetResult();
    Application.Run(form); // Blocks until window closes
});

thread.SetApartmentState(ApartmentState.STA);
thread.IsBackground = false; // Keep app alive
thread.Start();
```

### DPI Scaling Support

**Window Dimensions:**
- Base window size: 300x210 pixels (logical)
- Saved in config as logical pixels (DPI-independent)
- Converted to physical pixels based on current monitor DPI:
  - 100% DPI → 300x210 physical pixels
  - 150% DPI → 450x315 physical pixels
  - 200% DPI → 600x420 physical pixels

**Configuration Storage:**
- All window positions/sizes stored as logical pixels in config.json
- Automatically converted on load/save based on current DPI
- Works seamlessly when moving between monitors with different DPI

**Internal UI Elements:**
- Border width, resize handles, and hit areas are manually DPI-scaled
- Uses `DpiHelper.ScalePixels()` for consistent interaction across all DPIs
- Base values (3px border, 10px hit area) scale proportionally

**Benefits:**
- DPI-independent configuration
- Consistent visual size across different monitor types
- Sharp rendering on high-DPI displays
- Proper per-monitor DPI support (moving between monitors)

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

## Theme and Language Synchronization

### Overview

The screen capture control panel automatically synchronizes theme and language settings with the main application window in real-time. This ensures a consistent user experience across all windows.

### Architecture

**Backend (GlobalSettingService.cs):**
- Emits `GLOBAL_SETTINGS_CHANGED` event when settings are updated
- Event includes full settings object: `{theme, language, annotationLevel, logLevel, lastUpdated}`
- Events are emitted from:
  - `UpdateSettingsAsync()` - bulk settings update
  - `UpdateSettingAsync()` - single field update (theme, language, etc.)
  - `ResetSettingsAsync()` - reset to defaults

**Frontend (ThemeContext.tsx):**
- Subscribes to `GLOBAL_SETTINGS_CHANGED` events
- Updates theme state and applies new theme via ConfigProvider
- Syncs settings store to keep all consumers in sync

**Frontend (I18nInitializer.tsx):**
- Subscribes to `GLOBAL_SETTINGS_CHANGED` events
- Calls `i18n.changeLanguage()` to update UI language immediately
- All React components re-render with new translations

### Implementation

```typescript
// ThemeContext.tsx - Listen for theme changes
useEffect(() => {
  const unsubscribe = eventBus.subscribe(
    Module.SETTING,
    SettingsEventType.GLOBAL_SETTINGS_CHANGED,
    (event) => {
      if (event.payload?.theme) {
        const newTheme = event.payload.theme as ThemeMode;
        setThemeState(newTheme);
        // Update settings store for consistency
        const { setGlobalSettings } = useSettingsStore.getState();
        // ...
      }
    }
  );
  return unsubscribe;
}, []);
```

```typescript
// I18nInitializer.tsx - Listen for language changes
useEffect(() => {
  const unsubscribe = eventBus.subscribe(
    Module.SETTING,
    SettingsEventType.GLOBAL_SETTINGS_CHANGED,
    async (event) => {
      if (event.payload?.language) {
        await i18n.changeLanguage(event.payload.language);
      }
    }
  );
  return unsubscribe;
}, []);
```

### Shared App Initialization

Both the main app and capture window use `AppWrapper` component for consistent initialization:

**Location:** `shared/components/AppWrapper.tsx`

**Provides:**
- ProfileProvider - Profile context management
- SettingsProvider - Loads global settings into store
- ThemeProvider - Theme management with event subscriptions
- I18nInitializer - Language initialization with event subscriptions
- ConfigProvider - Ant Design theme algorithm
- NotificationInitializer - Sets up notification API

**Usage:**
```typescript
// Main app (App.tsx)
<AppWrapper>
  <ModProvider>
    <SlideInScreenProvider>
      <AppInitializer>
        <AppContent />
      </AppInitializer>
    </SlideInScreenProvider>
  </ModProvider>
</AppWrapper>

// Capture window (capture.tsx)
<AppWrapper>
  <ScreenCaptureTool />
</AppWrapper>
```

### CSS Styling

**Full Height Layout:**

The capture window uses `.ant-app { height: 100vh; }` in `visual-enhancements.css` to ensure the content fills the entire window without black areas.

**Form Background:**

The WinForms `Form` has `BackColor = Color.White` set to match the default light theme and prevent black background showing through during load.

### Event Flow Example

1. **User changes theme in settings** (main app or capture window)
2. **Frontend calls** `settingsService.updateGlobalSetting('theme', 'dark')`
3. **Backend updates** settings file and cache via `GlobalSettingService`
4. **Backend emits** `GLOBAL_SETTINGS_CHANGED` event with updated settings
5. **All windows receive event** via EventBus bridge
6. **ThemeProvider updates** theme state in each window
7. **ConfigProvider applies** dark theme algorithm to all Ant Design components
8. **UI re-renders** with new theme immediately

### Benefits

- **Real-time sync**: Changes appear instantly in all open windows
- **Consistent UX**: All windows always match the current theme/language
- **No manual refresh**: Users don't need to close/reopen windows
- **Centralized logic**: Theme/language handling is shared via AppWrapper

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

### Creating Capture Window (ScreenCaptureService)

```csharp
// Capture-specific window creation in ScreenCaptureService
private async Task<Form?> CreateCaptureWindowAsync()
{
    const string windowName = "capture";
    const string title = "Screen Capture";
    const int defaultWidth = 300;
    const int defaultHeight = 210;

    // Call generic SecondaryWindowService
    var form = await _windowService.CreateSecondaryWindowAsync(
        windowName,
        title,
        defaultWidth,
        defaultHeight,
        "capture.html"
    );

    if (form != null)
    {
        // Add capture-specific behavior: close overlay when window closes
        form.FormClosing += (s, e) =>
        {
            if (IsBorderOverlayVisible)
            {
                HideBorderOverlayAsync().GetAwaiter().GetResult();
            }
        };
    }

    return form;
}
```

### Generic Window Creation (SecondaryWindowService)

```csharp
// Generic method for creating any secondary window
public async Task<Form?> CreateSecondaryWindowAsync(
    string windowName,
    string title,
    int defaultWidth,
    int defaultHeight,
    string htmlPage)
{
    // Load saved position/size from ProfileConfiguration.Windows[windowName]
    var (position, size) = await LoadWindowConfigurationAsync(windowName, defaultWidth, defaultHeight, screen);

    var form = new Form
    {
        Text = title,
        Size = size,
        Location = position,
        FormBorderStyle = FormBorderStyle.FixedToolWindow,
        TopMost = true
    };

    // ... WebView2 setup ...

    // Save window configuration on close
    form.FormClosing += (s, e) =>
    {
        if (_openWindows.TryRemove(windowName, out var entry))
        {
            _ = Task.Run(async () =>
            {
                await SaveWindowConfigurationAsync(windowName, form.Location, form.Size);
            });
        }
    };

    // Add to tracking dictionary
    _openWindows.TryAdd(windowName, new WindowEntry(form, session, windowName));

    return form;
}
```

### Checking Window Existence

```csharp
// Check if specific window exists
public bool HasWindow(string windowName)
{
    return _openWindows.ContainsKey(windowName);
}

// Close specific window
public void CloseWindow(string windowName)
{
    if (_openWindows.TryGetValue(windowName, out var entry))
    {
        var form = entry.Form;
        if (form.InvokeRequired)
            form.Invoke(() => form.Close());
        else
            form.Close();
    }
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
