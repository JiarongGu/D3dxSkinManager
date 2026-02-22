using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Composition;

/// <summary>
/// Handles WebView2 initialization and configuration
/// </summary>
public class WebViewInitializer
{
    private readonly WebView2 _webView;
    private readonly string _baseDirectory;
    private readonly ICustomSchemeHandler _schemeHandler;

    public WebViewInitializer(WebView2 webView, ICustomSchemeHandler schemeHandler)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _schemeHandler = schemeHandler ?? throw new ArgumentNullException(nameof(schemeHandler));
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Initialize WebView2 with proper environment settings
    /// </summary>
    public async Task InitializeAsync()
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
                                             "--disable-gpu-driver-bug-workarounds";

        // Create WebView2 environment with performance options
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: options);

        // Initialize WebView2
        await _webView.EnsureCoreWebView2Async(environment);

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

        // Enable dev tools in development
        settings.AreDevToolsEnabled = IsDevelopmentMode();

        // Enable default context menus
        settings.AreDefaultContextMenusEnabled = true;

        // Disable password autosave
        settings.IsPasswordAutosaveEnabled = false;

        // Performance settings
        settings.IsWebMessageEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;

        // Allow external drop
        _webView.AllowExternalDrop = true;

        // Set default background color to prevent white flash
        _webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(26, 26, 26); // #1a1a1a

        Console.WriteLine("[WebView2] Settings configured");
    }

    private void RegisterCustomSchemeHandler()
    {
        // Register filter for app:// scheme
        _webView.CoreWebView2.AddWebResourceRequestedFilter("app://*", CoreWebView2WebResourceContext.All);

        // Handle web resource requests
        _webView.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            try
            {
                var uri = args.Request.Uri;

                if (!uri.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
                    return;

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
            catch (Exception ex)
            {
                Console.WriteLine($"[WebView2] Error handling custom scheme: {ex.Message}");

                // Return 404 error
                var errorStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Resource not found"));
                var errorResponse = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    errorStream,
                    404,
                    "Not Found",
                    "Content-Type: text/plain");

                args.Response = errorResponse;
            }
        };

        Console.WriteLine("[WebView2] Custom scheme handler registered for app://");
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
        var indexPath = Path.Combine(_baseDirectory, "wwwroot", "index.html");

        if (File.Exists(indexPath))
        {
            var fileUrl = $"file:///{indexPath.Replace('\\', '/')}";
            Console.WriteLine($"[WebView2] Production mode - loading {fileUrl}");
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