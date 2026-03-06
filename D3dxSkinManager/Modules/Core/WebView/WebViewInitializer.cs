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

    public WebViewInitializer(WebView2 webView, ICustomSchemeHandler schemeHandler, IEmbeddedResourceProvider resourceProvider)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _schemeHandler = schemeHandler ?? throw new ArgumentNullException(nameof(schemeHandler));
        _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Initialize WebView2 with proper environment settings
    /// </summary>
    public async Task InitAsync()
    {
        Console.WriteLine("[WebView2] Starting initialization...");

        // Create user data folder for WebView2
        var userDataFolder = Path.Combine(_baseDirectory, "data", "webview2");
        Directory.CreateDirectory(userDataFolder);

        // Create environment options for better performance
        var options = new CoreWebView2EnvironmentOptions();
        options.AdditionalBrowserArguments = "--enable-features=msWebView2EnableDraggableRegions " +
                                             "--disable-features=msSmartScreenProtection " +
                                             "--enable-gpu-rasterization " +
                                             "--enable-zero-copy " +
                                             "--enable-accelerated-2d-canvas " +
                                             "--enable-hardware-overlays " +
                                             "--force-color-profile=srgb " +
                                             "--disable-background-timer-throttling " +
                                             "--disable-renderer-backgrounding " +
                                             "--disable-features=TranslateUI " +
                                             "--disable-ipc-flooding-protection " +
                                             "--disable-gpu-driver-bug-workarounds " +
                                             "--disable-component-update " +
                                             "--disable-default-apps " +
                                             "--disable-domain-reliability " +
                                             "--disable-sync " +
                                             "--no-first-run " +
                                             "--no-default-browser-check " +
                                             "--disable-background-networking " +
                                             "--disable-breakpad " +
                                             // ENABLE V8 CODE CACHING - this is critical for fast subsequent loads!
                                             // Code cache is stored in user data folder and persists between runs
                                             "--enable-features=IsolatedCodeCache " +
                                             // V8 JavaScript optimization flags:
                                             // --no-lazy: Compile all functions immediately (not lazy)
                                             // --always-opt: Always optimize (don't wait for hot code)
                                             // --serialize-eager: Eagerly serialize code cache
                                             // --max-old-space-size: Limit V8 memory
                                             "--js-flags=--no-lazy --always-opt --serialize-eager --max-old-space-size=512 " +
                                             "--enable-lazy-image-loading " +
                                             "--enable-features=ScriptStreaming";

        // Create WebView2 environment with performance options
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: options);

        // Initialize WebView2
        await _webView.EnsureCoreWebView2Async(environment);

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

        // Block browser keyboard shortcuts in production
        if (!isDevelopment)
        {
            ConfigureKeyboardShortcutBlocking();
        }

        Console.WriteLine($"[WebView2] Settings configured (Mode: {(isDevelopment ? "Development" : "Production")})");
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

        // Register filter for virtual host (embedded web resources)
        _webView.CoreWebView2.AddWebResourceRequestedFilter("https://app.local/*", CoreWebView2WebResourceContext.All);

        // Handle web resource requests
        _webView.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            try
            {
                var uri = args.Request.Uri;
                Console.WriteLine($"[WebView2] 🔍 Resource requested: {uri}");

                // Handle app:// scheme (dynamic file resources like thumbnails)
                if (uri.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
                {
                    // Get file stream from custom scheme handler
                    var stream = _schemeHandler.HandleRequest(uri, out var contentType);

                    // Create response
                    var response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        stream,
                        200,
                        "OK",
                        $"Content-Type: {contentType}");

                    args.Response = response;
                }
                // Handle virtual host (embedded web resources)
                else if (uri.StartsWith("https://app.local/", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract virtual path from URI
                    // https://app.local/index.html -> wwwroot/index.html
                    // https://app.local/assets/index.js -> wwwroot/assets/index.js
                    var path = uri.Substring("https://app.local/".Length);

                    // Strip query parameters if present (e.g., capture.html?profileId=xxx -> capture.html)
                    var queryIndex = path.IndexOf('?');
                    if (queryIndex >= 0)
                    {
                        path = path.Substring(0, queryIndex);
                    }

                    var virtualPath = "wwwroot/" + path;

                    var stream = _resourceProvider.GetResourceStream(virtualPath);

                    if (stream != null)
                    {
                        var contentType = GetContentType(virtualPath);

                        // Add aggressive caching headers to speed up resource loading
                        var headers = $"Content-Type: {contentType}\n" +
                                     "Cache-Control: public, max-age=31536000, immutable\n" +
                                     "Access-Control-Allow-Origin: *";

                        var response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            stream,
                            200,
                            "OK",
                            headers);

                        args.Response = response;
                    }
                    else
                    {
                        // Resource not found
                        Console.WriteLine($"[WebView2] ✗ NOT FOUND: {virtualPath}");
                        var errorStream = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes($"Embedded resource not found: {virtualPath}"));
                        var errorResponse = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            errorStream,
                            404,
                            "Not Found",
                            "Content-Type: text/plain");

                        args.Response = errorResponse;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebView2] Error handling custom scheme: {ex.Message}");

                // Return 404 error
                var errorStream = new MemoryStream(global::System.Text.Encoding.UTF8.GetBytes("Resource not found"));
                var errorResponse = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    errorStream,
                    404,
                    "Not Found",
                    "Content-Type: text/plain");

                args.Response = errorResponse;
            }
        };

        Console.WriteLine("[WebView2] Custom scheme handlers registered (app://, https://app.local/)");
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
        var devUrl = "http://localhost:3000";
        Console.WriteLine($"[WebView2] Development mode - navigating to {devUrl}");
        _webView.CoreWebView2.Navigate(devUrl);
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