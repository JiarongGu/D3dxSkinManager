using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Handles WebView2 initialization and configuration
/// </summary>
public class WebViewInitializer
{
    private readonly WebView2 _webView;
    private readonly string _baseDirectory;
    private readonly ICustomSchemeHandler _schemeHandler;
    private readonly IEmbeddedResourceProvider _resourceProvider;
    // Secondary windows run on their own STA thread and must create their OWN CoreWebView2Environment
    // there (the prewarmed one is affine to the main UI thread — using it off-thread throws).
    private readonly bool _ownEnvironment;

    public WebViewInitializer(WebView2 webView, ICustomSchemeHandler schemeHandler, IEmbeddedResourceProvider resourceProvider, string baseDirectory, bool ownEnvironment = false)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _schemeHandler = schemeHandler ?? throw new ArgumentNullException(nameof(schemeHandler));
        _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        // The install ROOT (IAppEnvironment.BaseDirectory), NOT AppDomain.BaseDirectory: the runtime runs
        // from {install}/libs in production, so AppDomain would put the WebView2 user-data folder at
        // {install}/libs/data/webview2 for secondary windows (CreateForThreadAsync isn't cached like the
        // prewarmed main-window env). See launcher-topology.md.
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _ownEnvironment = ownEnvironment;
    }

    /// <summary>
    /// Initialize WebView2 with proper environment settings
    /// </summary>
    public async Task InitAsync()
    {
        Console.WriteLine("[WebView2] Starting initialization...");

        // Use the prewarmed environment (started at process launch by WebView2EnvironmentPrewarmer.Begin)
        // so the ~1-2s browser-process spawn overlaps the rest of startup instead of running serially here.
        // The browser arguments (incl. the dev CDP env-var append) are built inside the prewarmer.
        // The residual wait here = how much of the spawn did NOT overlap (≈0 when prewarm finished in time).
        var envWait = global::System.Diagnostics.Stopwatch.StartNew();
        // Secondary windows (own STA thread) create their own env on THIS thread; the main window uses
        // the prewarmed shared env. Mixing threads throws "CoreWebView2Environment members can only be
        // accessed from the UI thread" — which broke every secondary window (capture + analyzer).
        var environment = _ownEnvironment
            ? await WebView2EnvironmentPrewarmer.CreateForThreadAsync(_baseDirectory)
            : await WebView2EnvironmentPrewarmer.GetAsync(_baseDirectory);
        envWait.Stop();
        Console.WriteLine($"[WebView2] Environment ready (InitAsync waited {envWait.ElapsedMilliseconds}ms, own={_ownEnvironment})");

        // Initialize WebView2
        var ensureSw = global::System.Diagnostics.Stopwatch.StartNew();
        await _webView.EnsureCoreWebView2Async(environment);
        ensureSw.Stop();
        Console.WriteLine($"[WebView2] EnsureCoreWebView2Async took {ensureSw.ElapsedMilliseconds}ms");

        // Add navigation event handlers for debugging with precise timing
        var navigationStartTime = DateTime.MinValue;

        _webView.CoreWebView2.NavigationStarting += (s, e) =>
        {
            navigationStartTime = DateTime.Now;
            Console.WriteLine($"[WebView2] ⏱️  [{navigationStartTime:HH:mm:ss.fff}] Navigation starting: {e.Uri}");
        };

        _webView.CoreWebView2.NavigationCompleted += (s, e) =>
        {
            var elapsed = navigationStartTime != DateTime.MinValue ? (DateTime.Now - navigationStartTime).TotalMilliseconds : 0;
            Console.WriteLine($"[WebView2] ⏱️  [{DateTime.Now:HH:mm:ss.fff}] {(e.IsSuccess ? "✅" : "❌")} Navigation completed in {elapsed:F0}ms: Success={e.IsSuccess}, Status={e.WebErrorStatus}");
        };

        _webView.CoreWebView2.DOMContentLoaded += (s, e) =>
        {
            var elapsed = navigationStartTime != DateTime.MinValue ? (DateTime.Now - navigationStartTime).TotalMilliseconds : 0;
            Console.WriteLine($"[WebView2] ⏱️  [{DateTime.Now:HH:mm:ss.fff}] 📄 DOM Content Loaded in {elapsed:F0}ms from navigation start");
        };

        // Configure settings
        ConfigureWebViewSettings();

        // Register custom scheme handler
        RegisterCustomSchemeHandler();

        // Inject application metadata (version, etc.)
        InjectAppMetadata();

        Console.WriteLine("[WebView2] Initialization completed");
    }

    /// <summary>
    /// Navigate to the appropriate URL based on environment
    /// </summary>
    public void NavigateToApp()
    {
        var isDevelopment = IsDevelopmentMode();

        if (isDevelopment)
        {
            NavigateToDevelopment();
        }
        else
        {
            NavigateToProduction();
        }
    }

    private void ConfigureWebViewSettings()
    {
        var settings = _webView.CoreWebView2.Settings;
        var isDevelopment = IsDevelopmentMode();

        // Enable dev tools only in development mode
        settings.AreDevToolsEnabled = isDevelopment;

        // Enable default context menus only in development mode
        settings.AreDefaultContextMenusEnabled = isDevelopment;

        // Disable password autosave
        settings.IsPasswordAutosaveEnabled = false;

        // Performance settings - disable everything unnecessary for faster startup
        settings.IsWebMessageEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;

        // Allow external drop to enable internal drag-drop between React components
        // External file drops are still captured by DropZoneOverlay at Form level
        _webView.AllowExternalDrop = true;

        // Set default background color to prevent white flash
        _webView.DefaultBackgroundColor = Color.FromArgb(26, 26, 26); // #1a1a1a

        // Prevent default browser drop behavior (opening files in new tab)
        PreventDefaultDropBehavior();

        // Block browser keyboard shortcuts in production
        if (!isDevelopment)
        {
            ConfigureKeyboardShortcutBlocking();
        }

        Console.WriteLine($"[WebView2] Settings configured (Mode: {(isDevelopment ? "Development" : "Production")})");
    }

    /// <summary>
    /// Prevent default browser drop behavior (opening files in new tab) for EXTERNAL FILES ONLY
    /// Allows React internal drag-and-drop to work normally
    /// External file drops are handled by DropZoneOverlay at Form level
    /// </summary>
    private void PreventDefaultDropBehavior()
    {
        var preventDropScript = @"
(function() {
    // Prevent default browser behavior ONLY for external file drops
    // Allow React internal drag-and-drop (HTML elements) to work normally

    document.addEventListener('dragover', function(e) {
        // Check if this is an external file drag (not internal HTML element drag)
        var types = e.dataTransfer.types;
        var isFileDrag = types && types.indexOf('Files') !== -1;

        if (isFileDrag) {
            // External file: allow drag over but prevent default browser behavior
            e.preventDefault();
        }
        // For internal HTML drags: do nothing, let React handle it
    }, true);

    document.addEventListener('drop', function(e) {
        // Check if this is an external file drop
        var types = e.dataTransfer.types;
        var isFileDrop = types && types.indexOf('Files') !== -1;

        if (isFileDrop) {
            // External file drop: prevent browser from opening the file
            e.preventDefault();
            console.log('[DropZone] Browser file drop prevented - handled by WinForms overlay');
        }
        // For internal HTML drops: do nothing, let React handle it
    }, true);

    console.log('[DropZone] Default browser file drop behavior disabled (React drag-and-drop still works)');
})();
";

        // Add script to execute on every page navigation
        _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(preventDropScript);

        Console.WriteLine("[WebView2] Default file drop prevention enabled - React drag-and-drop preserved (JavaScript injection)");
    }

    /// <summary>
    /// Configure keyboard shortcut blocking for production mode
    /// Blocks common browser shortcuts that should not be available in the app
    /// Uses JavaScript injection since WinForms WebView2 doesn't expose AcceleratorKeyPressed
    /// </summary>
    private void ConfigureKeyboardShortcutBlocking()
    {
        // Inject JavaScript to block keyboard shortcuts at the document level
        var blockingScript = @"
(function() {
    // Block browser shortcuts in production
    document.addEventListener('keydown', function(e) {
        const ctrl = e.ctrlKey || e.metaKey;
        const shift = e.shiftKey;
        const key = e.key;

        // Block common browser shortcuts
        if (ctrl) {
            switch(key.toLowerCase()) {
                case 'f': // Find
                case 'g': // Find next
                case 'h': // History
                case 'j': // Downloads
                case 'p': // Print
                case 's': // Save page
                case 'u': // View source
                case '0': // Reset zoom
                case '+': // Zoom in
                case '=': // Zoom in
                case '-': // Zoom out
                case '_': // Zoom out
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
            }

            // Block Ctrl+Shift+I (DevTools)
            if (shift && key.toLowerCase() === 'i') {
                e.preventDefault();
                e.stopPropagation();
                return false;
            }
        }

        // Block F12 (DevTools)
        if (key === 'F12') {
            e.preventDefault();
            e.stopPropagation();
            return false;
        }

        // Allow: Ctrl+C, Ctrl+V, Ctrl+X, Ctrl+A, Ctrl+Z, Ctrl+Y (editing shortcuts)
        // Allow: Ctrl+R (Refresh)
        // Allow: Ctrl+W (Close)
    }, true); // Use capture phase to intercept before React
})();
";

        // Add script to execute on every page navigation
        _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(blockingScript);

        Console.WriteLine("[WebView2] Keyboard shortcut blocking enabled for production (JavaScript injection)");
    }

    private void RegisterCustomSchemeHandler()
    {
        // Register filter for app:// scheme (dynamic file resources)
        _webView.CoreWebView2.AddWebResourceRequestedFilter("app://*", CoreWebView2WebResourceContext.All);

        // proxy:// = remote resources fetched+cached by the backend on demand (proxy://image/?u=<url>).
        // Distinct scheme so the URL states its contract: app:// = local file, proxy:// = remote-via-cache.
        _webView.CoreWebView2.AddWebResourceRequestedFilter("proxy://*", CoreWebView2WebResourceContext.All);

        // Register filter for virtual host (embedded web resources)
        _webView.CoreWebView2.AddWebResourceRequestedFilter("https://app.local/*", CoreWebView2WebResourceContext.All);

        // Two schemes, two serving strategies — the split matters:
        //
        // app.local/* = the embedded prod bundle (index.html with JS/CSS inlined by vite-plugin-singlefile,
        //   + a few assets). These are IN-MEMORY embedded resources, and index.html is the MAIN DOCUMENT
        //   WebView2 is navigating to at startup. Serve them SYNCHRONOUSLY inline: an in-memory read is
        //   instant (no disk, no freeze risk) and the navigation needs the document promptly. Deferring the
        //   main document stalls the initial navigation → "stuck on start" (only reproduces in production;
        //   dev loads the bundle from Vite over http, never through this handler).
        //
        // app://* = on-disk dynamic files (mod previews + category/profile thumbnails). A library with
        //   hundreds of category cards fires that many DISK reads in a startup/scroll burst; doing those
        //   inline blocks the UI thread → FREEZE. So these go OFF the UI thread via a deferral: GetDeferral
        //   returns the UI thread immediately, the read happens async on the pool, and the response is built
        //   back on the UI thread (CoreWebView2 is UI-affine) via non-blocking BeginInvoke.
        //   (Do NOT decode images here — that earlier per-request ImageSharp decode was the real slowness,
        //   not the deferral. Removed.)
        _webView.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            var uri = args.Request.Uri;
            bool isApp = uri.StartsWith("app://", StringComparison.OrdinalIgnoreCase);
            bool isProxy = uri.StartsWith("proxy://", StringComparison.OrdinalIgnoreCase);
            bool isLocal = uri.StartsWith("https://app.local/", StringComparison.OrdinalIgnoreCase);
            if (!isApp && !isProxy && !isLocal) return; // not ours — let WebView2 handle it

            // --- app.local: synchronous, in-memory, prompt (main document + bundle) ---
            if (isLocal)
            {
                try
                {
                    var path = uri.Substring("https://app.local/".Length);
                    var queryIndex = path.IndexOf('?');
                    if (queryIndex >= 0) path = path.Substring(0, queryIndex);
                    var virtualPath = "wwwroot/" + path;

                    var stream = _resourceProvider.GetResourceStream(virtualPath);
                    if (stream != null)
                    {
                        var headers = $"Content-Type: {GetContentType(virtualPath)}\n" +
                                      "Cache-Control: public, max-age=31536000, immutable\n" +
                                      "Access-Control-Allow-Origin: *";
                        args.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            stream, 200, "OK", headers);
                    }
                    else
                    {
                        var data = global::System.Text.Encoding.UTF8.GetBytes($"Embedded resource not found: {virtualPath}");
                        args.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            new MemoryStream(data, writable: false), 404, "Not Found", "Content-Type: text/plain");
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        var data = global::System.Text.Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                        args.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            new MemoryStream(data, writable: false), 404, "Not Found", "Content-Type: text/plain");
                    }
                    catch { /* webview may be tearing down */ }
                }
                return;
            }

            // --- app:// + proxy://: deferred, off-UI-thread, async (disk read / remote fetch+cache) ---
            var deferral = args.GetDeferral();
            _ = Task.Run(async () =>
            {
                byte[] data;
                int status = 200;
                string reason = "OK";
                string headers;
                try
                {
                    // Cache 1 day; callers cache-bust via ?t=<mtime>.
                    var (bytes, contentType) = await _schemeHandler.HandleRequestBytesAsync(uri).ConfigureAwait(false);
                    data = bytes;
                    headers = $"Content-Type: {contentType}\nCache-Control: public, max-age=86400";
                }
                catch (Exception ex)
                {
                    data = global::System.Text.Encoding.UTF8.GetBytes($"Error: {ex.Message}");
                    status = 404; reason = "Not Found"; headers = "Content-Type: text/plain";
                }

                // CoreWebView2 is UI-thread affine — create the response there, then complete the deferral.
                void Build()
                {
                    try
                    {
                        args.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            new MemoryStream(data, writable: false), status, reason, headers);
                    }
                    catch { /* webview may be tearing down */ }
                    finally { deferral.Complete(); }
                }
                try
                {
                    if (_webView.IsHandleCreated && _webView.InvokeRequired)
                        _webView.BeginInvoke((Action)Build);
                    else
                        Build();
                }
                catch { try { deferral.Complete(); } catch { /* ignore */ } }
            });
        };

        Console.WriteLine("[WebView2] Custom scheme handlers registered (app://, proxy://, https://app.local/)");
    }

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            ".map" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private bool IsDevelopmentMode()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
               File.Exists(Path.Combine(_baseDirectory, ".dev"));
    }

    private void NavigateToDevelopment()
    {
        // Unique dev port (matches D3dxSkinManager.Client/vite.config.ts server.port). NOT 3000 — avoids
        // colliding with other local WebView2/React dev servers (e.g. a sibling app). Keep all in sync.
        var devUrl = "http://localhost:3517";
        Console.WriteLine($"[WebView2] Development mode - navigating to {devUrl}");
        _webView.CoreWebView2.Navigate(devUrl);
    }

    /// <summary>
    /// Inject application metadata (version, name) as global JavaScript variables
    /// </summary>
    private void InjectAppMetadata()
    {
        var assembly = global::System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var appName = assembly.GetName().Name;

        // Use simple version (major.minor) from AssemblyVersion, e.g., "1.1"
        // Avoid InformationalVersion as it may include git commit hash from build process
        var versionString = version != null ? $"{version.Major}.{version.Minor}" : "1.0";

        var metadataScript = $@"
(function() {{
    // Inject app metadata as global variable
    window.__APP_METADATA__ = {{
        name: '{appName}',
        version: '{versionString}'
    }};
    console.log('[App] Metadata injected:', window.__APP_METADATA__);
}})();
";

        // Add script to execute on every page navigation
        _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(metadataScript);

        Console.WriteLine($"[WebView2] App metadata injected: {appName} v{versionString}");
    }

    private void NavigateToProduction()
    {
        // Check if we're in embedded mode or file-based mode
        if (_resourceProvider.IsEmbeddedMode)
        {
            // Embedded mode: Navigate to custom scheme (single-file HTML with all JS/CSS inlined by vite-plugin-singlefile)
            // This will be served by the custom scheme handler from embedded resources
            const string productionUrl = "https://app.local/index.html";

            Console.WriteLine($"[WebView2] Production mode (EMBEDDED) - navigating to {productionUrl} (single-file build)");
            _webView.CoreWebView2.Navigate(productionUrl);
        }
        else
        {
            // File-based mode: Load from filesystem (fallback for development)
            var indexPath = Path.Combine(_baseDirectory, "wwwroot", "index.html");

            if (File.Exists(indexPath))
            {
                var fileUrl = $"file:///{indexPath.Replace('\\', '/')}";
                Console.WriteLine($"[WebView2] Production mode (FILE-BASED) - loading {fileUrl}");
                _webView.CoreWebView2.Navigate(fileUrl);
            }
            else
            {
                Console.WriteLine("[WebView2] Warning: wwwroot/index.html not found");
                _webView.CoreWebView2.NavigateToString(@"
                    <html>
                    <body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h1>React Build Not Found</h1>
                        <p>Please build the React application:</p>
                        <pre>cd D3dxSkinManager.Client && npm run build</pre>
                    </body>
                    </html>");
            }
        }
    }
}