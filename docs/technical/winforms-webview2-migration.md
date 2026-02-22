# WinForms + WebView2 Migration Plan

**Version:** 1.4
**Last Updated:** 2026-02-22
**Technology:** .NET 10 + Modern WinForms + WebView2 + React 19

## Overview

Migrate from Photino.NET to WinForms + WebView2 for better Windows integration and native control.

## Why Migrate?

### Current Limitations with Photino.NET
- Limited native Windows drag-drop control
- Wrapper abstraction over WebView2
- Difficult to implement OS-level features
- Limited window manipulation capabilities

### Benefits of WinForms + WebView2
- ✅ Full native drag-drop control (DragEnter, DragOver, DragDrop events)
- ✅ Direct access to WebView2 APIs
- ✅ Better OS integration (system tray, notifications, etc.)
- ✅ Same IPC pattern - minimal React changes
- ✅ Microsoft's official WebView2 control
- ✅ Better debugging with DevTools
- ✅ Keep all existing services and DI architecture

## Architecture Comparison

### Current: Photino.NET
```
React Frontend (localhost:3000 or wwwroot)
    ↕ (window.chrome.webview / window.external)
Photino.NET Wrapper
    ↕
WebView2 (embedded)
    ↕
ServiceRouter → Module Facades → Services
```

### New: WinForms + WebView2
```
React Frontend (localhost:3000 or wwwroot)
    ↕ (window.chrome.webview)
WebView2 Control (direct)
    ↕
WinForms Host Application
    ↕
ServiceRouter → Module Facades → Services (UNCHANGED!)
```

## IPC Communication - NO REACT CHANGES NEEDED!

### Why No React Changes?

Photino.NET already uses the standard WebView2 API internally. Your React code checks for `window.chrome.webview`, which is the native WebView2 API:

**Current photinoService.ts code:**
```typescript
function getPhotinoBridge(): PhotinoWindow | undefined {
  // This fallback is for old Photino versions
  if (window.external && typeof window.external.sendMessage === 'function') {
    return window.external;
  }
  // This is the standard WebView2 API - already using it!
  if (window.chrome?.webview) {
    return window.chrome.webview;  // ← This works with WinForms+WebView2!
  }
  return undefined;
}
```

### IPC APIs (Same in both)

| Direction | Photino (current) | WinForms+WebView2 (new) | React Code Change |
|-----------|-------------------|-------------------------|-------------------|
| React → C# | `window.chrome.webview.postMessage()` | `window.chrome.webview.postMessage()` | **NONE** |
| C# → React | `window.SendWebMessage()` | `webView.CoreWebView2.PostWebMessageAsJson()` | **NONE** |
| React handler | `window.chrome.webview.addEventListener('message')` | `window.chrome.webview.addEventListener('message')` | **NONE** |
| C# handler | `window.RegisterWebMessageReceivedHandler()` | `webView.CoreWebView2.WebMessageReceived += handler` | N/A |

## Migration Steps

### Phase 1: Update Project File

**File: D3dxSkinManager.csproj**

```xml
<!-- BEFORE -->
<PackageReference Include="Photino.NET" Version="4.0.16" />

<!-- AFTER - Using latest .NET 10 compatible versions -->
<PackageReference Include="Microsoft.Web.WebView2.WinForms" Version="1.0.2895.51" />
```

Already have `<UseWindowsForms>true</UseWindowsForms>` ✅

**.NET 10 Modern Features We'll Use:**
- **Top-level statements** - Cleaner Program.cs
- **File-scoped namespaces** - Less indentation
- **Init-only properties** - Immutable configuration
- **Record types** - For DTOs and messages
- **Nullable reference types** - Better null safety
- **Global usings** - Reduce boilerplate

### Phase 2: Create WinForms Application

**New File: MainForm.cs** (.NET 10 modern syntax)

```csharp
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace D3dxSkinManager;  // File-scoped namespace (.NET 10)

public partial class MainForm : Form
{
    private readonly WebView2 _webView;
    private readonly ServiceRouter _serviceRouter;
    private readonly DevelopmentServerManager? _devServer;
    private readonly ICustomSchemeHandler _schemeHandler;
    private readonly IWindowStateService _windowStateService;

    // JSON serializer options (reuse from Program.cs)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MainForm(
        ServiceRouter router,
        DevelopmentServerManager? devServer,
        ICustomSchemeHandler schemeHandler,
        IWindowStateService windowStateService)
    {
        _serviceRouter = router;
        _devServer = devServer;
        _schemeHandler = schemeHandler;
        _windowStateService = windowStateService;

        InitializeComponent();

        // Load saved window state
        LoadWindowState();

        // Initialize WebView2 asynchronously
        _ = InitializeWebViewAsync();
    }

    private void LoadWindowState()
    {
        var (width, height, x, y, maximized) = _windowStateService.LoadWindowState();

        Size = new Size(width, height);

        if (x.HasValue && y.HasValue &&
            _windowStateService.IsPositionValid(x.Value, y.Value, width, height, this))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(x.Value, y.Value);
        }

        if (maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        // WebView2 initialization with modern await pattern
        await _webView.EnsureCoreWebView2Async(null);

        // Set up IPC message handler
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        // Set up custom scheme handler (for app:// URLs)
        _webView.CoreWebView2.AddWebResourceRequestedFilter("app://*",
            Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

        // Dev tools (F12) in development mode
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

        // Navigate to URL
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var indexPath = Path.Combine(baseDir, "wwwroot", "index.html");
        var isDevelopment = !File.Exists(indexPath);

        var url = isDevelopment
            ? "http://localhost:3000"
            : $"file:///{indexPath.Replace("\\", "/")}";

        _webView.CoreWebView2.Navigate(url);

        Console.WriteLine($"[Init] WebView2 navigated to: {url}");
    }

    private async void OnWebMessageReceived(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.WebMessageAsJson;

        try
        {
            // Handle FILE_DROP_INTERCEPTED
            var rawMessage = JsonSerializer.Deserialize<JsonElement>(message);
            if (rawMessage.TryGetProperty("type", out var typeElement) &&
                typeElement.GetString() == "FILE_DROP_INTERCEPTED")
            {
                var filePath = rawMessage.GetProperty("filePath").GetString();
                if (!string.IsNullOrEmpty(filePath))
                {
                    SendFilesDropped([filePath]);
                }
                return;
            }

            // Handle all other messages through ServiceRouter
            var request = JsonSerializer.Deserialize<MessageRequest>(message, JsonOptions);
            if (request is null)
            {
                throw new InvalidOperationException("Failed to deserialize message");
            }

            Console.WriteLine($"[IPC] Request: {request.Type} (ID: {request.Id})");

            // Legacy compatibility handlers
            if (request.Type == "INIT_DROP_TARGET" ||
                request.Type == "START_DROP_LISTENING" ||
                request.Type == "STOP_DROP_LISTENING")
            {
                SendResponse(MessageResponse.CreateSuccess(request.Id, new { initialized = true }));
                return;
            }

            // Route through ServiceRouter (same as before)
            var response = await _serviceRouter.HandleMessageAsync(request);
            SendResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Error: {ex.Message}");
            SendResponse(MessageResponse.CreateError("unknown", ex.Message));
        }
    }

    private void OnWebResourceRequested(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);

        if (uri.Scheme == "app")
        {
            var stream = _schemeHandler.HandleRequest(e.Request.Uri, out string contentType);

            var response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                stream,
                200,
                "OK",
                $"Content-Type: {contentType}");

            e.Response = response;
        }
    }

    // Native drag-drop support - THIS IS WHY WE'RE MIGRATING!
    protected override void OnDragEnter(DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            SendFilesDropped(files);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Save window state before closing
        _windowStateService.SaveWindowState(this);
        base.OnFormClosing(e);
    }

    // Helper methods
    private void SendResponse(MessageResponse response)
    {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        _webView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void SendFilesDropped(string[] filePaths)
    {
        var message = new { type = "FILES_DROPPED", filePaths };
        var json = JsonSerializer.Serialize(message, JsonOptions);
        _webView.CoreWebView2.PostWebMessageAsJson(json);
        Console.WriteLine($"[DragDrop] Sent {filePaths.Length} file(s) to React");
    }
}
```

**New File: MainForm.Designer.cs**

```csharp
namespace D3dxSkinManager;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private Microsoft.Web.WebView2.WinForms.WebView2 webView;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
        ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
        this.SuspendLayout();

        // webView
        this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
        this.webView.Location = new System.Drawing.Point(0, 0);
        this.webView.Name = "webView";
        this.webView.Size = new System.Drawing.Size(1280, 800);
        this.webView.TabIndex = 0;

        // MainForm
        this.AllowDrop = true;  // Enable drag-drop!
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1280, 800);
        this.Controls.Add(this.webView);
        this.Name = "MainForm";
        this.Text = "D3dxSkinManager";
        this.StartPosition = FormStartPosition.CenterScreen;

        ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
        this.ResumeLayout(false);
    }
}
```

### Phase 3: Update Program.cs

**File: Program.cs**

```csharp
[STAThread]
static void Main(string[] args)
{
    Console.WriteLine("=== D3dxSkinManager Starting ===");

    // Initialize services (same as before)
    InitializeServices();
    Initialize7zLibrary();

    // Start dev server (same as before)
    var devServer = await StartDevelopmentServerIfNeeded();

    // Create WinForms application
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.SetHighDpiMode(HighDpiMode.SystemAware);

    // Create and run main form
    var mainForm = new MainForm(_serviceRouter, devServer);
    Application.Run(mainForm);

    // Shutdown (same as before)
    Shutdown();
}
```

### Phase 4: Custom Scheme Handler Migration

**WebView2 approach for app:// URLs:**

```csharp
private void OnWebResourceRequested(object? sender,
    Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs e)
{
    var uri = new Uri(e.Request.Uri);

    if (uri.Scheme == "app")
    {
        var stream = _schemeHandler.HandleRequest(e.Request.Uri, out string contentType);

        var response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
            stream,
            200,
            "OK",
            $"Content-Type: {contentType}");

        e.Response = response;
    }
}
```

## Services - NO CHANGES NEEDED

All your existing services remain unchanged:
- ✅ ServiceRouter
- ✅ Module Facades (ModFacade, etc.)
- ✅ All service implementations
- ✅ Dependency injection setup
- ✅ CustomSchemeHandler (minor adapter for WebView2)
- ✅ WindowStateService (adapt to use Form properties)
- ✅ DevelopmentServerManager

## React Frontend - NO CHANGES NEEDED

Your entire React codebase works as-is:
- ✅ All components
- ✅ All hooks
- ✅ photinoService.ts (already using chrome.webview)
- ✅ All service calls
- ✅ Message types and interfaces

## Testing Strategy

### Step 1: Parallel Development
1. Keep Photino version working
2. Create WinForms version in new branch
3. Test both side-by-side

### Step 2: Feature Parity Testing
- [ ] Window state persistence
- [ ] IPC communication (all message types)
- [ ] File drag-drop
- [ ] Custom scheme handler (app://)
- [ ] Operation notifications
- [ ] All module operations
- [ ] Dev server integration
- [ ] Production build

### Step 3: Native Features Testing
- [ ] Native drag-drop with file paths
- [ ] Window manipulation
- [ ] System tray integration (future)
- [ ] OS notifications (future)

## Timeline Estimate

- **Phase 1** (Project setup): 30 minutes
- **Phase 2** (Create MainForm): 2 hours
- **Phase 3** (Update Program.cs): 1 hour
- **Phase 4** (Custom scheme handler): 1 hour
- **Testing**: 2-3 hours
- **Total**: ~1 day of focused work

## Rollback Plan

Keep Photino version in a branch:
```bash
git checkout -b backup/photino-version
git commit -am "Backup: Photino version before migration"
git checkout master
# Proceed with migration
```

## Additional Features After Migration

Once migrated, you can easily add:
1. **System tray icon** - `NotifyIcon` control
2. **Native context menus** - Right-click menus
3. **Toast notifications** - Windows 10/11 notifications
4. **File associations** - Register .mod file handler
5. **Better drag-drop** - Visual feedback, drop zones
6. **Clipboard integration** - Copy/paste file paths
7. **Window theming** - Match Windows dark/light mode

## Resources

- [WebView2 Documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [WinForms + WebView2 Guide](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms)
- [WebView2 Samples](https://github.com/MicrosoftEdge/WebView2Samples)

## Conclusion

This migration is **low risk** and **high reward**:
- ✅ Minimal code changes
- ✅ React frontend unchanged
- ✅ Services unchanged
- ✅ Better Windows integration
- ✅ More control and flexibility
- ✅ Foundation for future native features

The main work is converting Program.cs to MainForm.cs - everything else stays the same!
