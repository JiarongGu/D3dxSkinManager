using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.WebView;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Bootstraps the application with proper initialization order
/// </summary>
public static class ApplicationBootstrapper
{
    private static ILogHelper? _logger;

    /// <summary>
    /// Bootstrap and run the application
    /// </summary>
    public static void Run()
    {
        // The install directory (the running exe's folder). Everything install-relative derives from it.
        var installDir = AppDomain.CurrentDomain.BaseDirectory;

        // Single-instance gate — FIRST, before any heavy init or the WebView2 prewarm (which takes the
        // user-data-folder lock). The app is not multi-instance-safe (per-profile SQLite, the mod-cache
        // planner only serializes within a process). A 2nd launch tells the running instance to come to
        // the front, then exits. Keyed per install so distinct installs still coexist.
        if (!SingleInstanceGuard.TryAcquire(installDir))
        {
            SingleInstanceGuard.BroadcastActivate();
            return;
        }

        // Create AppEnvironment for bootstrap phase
        var appEnv = AppEnvironment.Create(installDir);
        _logger = LogHelper.Create(appEnv);

        // Create a logger with configured log level (using named parameters to avoid ambiguity)
        _logger.Info("=== D3dxSkinManager Starting ===", "Bootstrap");
        _logger.Info($"Environment: {(appEnv.IsDevelopment ? "Development" : "Production")}", "Bootstrap");
        _logger.Info($"Log Level: {appEnv.MinimumLogLevel} (Debug logs {(appEnv.MinimumLogLevel > LogLevel.Debug ? "disabled" : "enabled")})", "Bootstrap");
        _logger.Info($"Thread apartment state: {Thread.CurrentThread.GetApartmentState()}", "Bootstrap");

        // Test that debug logs are filtered
        _logger.Debug("This debug message should not appear if log level is Info or higher", "Bootstrap");

        // Initialize WinForms
        InitializeWinForms();

        // Prewarm the WebView2 browser environment NOW (fire-and-forget). The browser-process spawn is
        // the dominant chunk of WebView2 init (~1-2s); starting it here lets it run while we build DI,
        // load window state, create the form, and do eager loading — so by the time the WebView session
        // needs it, it's usually ready. Does not need the message loop or any control.
        _logger.Info("Prewarming WebView2 environment...", "Bootstrap");
        WebView2EnvironmentPrewarmer.Begin(installDir);

        // Create application host
        var host = new ApplicationHost(appEnv, _logger);

        // Initialize services early (needed for window state loading)
        var diSw = global::System.Diagnostics.Stopwatch.StartNew();
        host.InitializeServices();
        diSw.Stop();
        Console.WriteLine($"[Startup] InitializeServices (DI build) took {diSw.ElapsedMilliseconds}ms");

        // Create main form (window state already loaded, no visual jump)
        var formSw = global::System.Diagnostics.Stopwatch.StartNew();
        host.CreateMainForm();
        formSw.Stop();
        Console.WriteLine($"[Startup] CreateMainForm took {formSw.ElapsedMilliseconds}ms");

        // Run the application
        host.Run();
    }

    /// <summary>
    /// Initialize WinForms with proper settings
    /// </summary>
    private static void InitializeWinForms()
    {
        _logger?.Info("Initializing WinForms...", "Bootstrap");

        // Enable visual styles for modern appearance
        Application.EnableVisualStyles();

        // Use GDI+ for text rendering (better performance)
        Application.SetCompatibleTextRenderingDefault(false);

        // Enable high DPI support for crisp rendering on high-resolution displays
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Optimize rendering for performance
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // Enable application-wide double buffering for smoother rendering
        typeof(Application).GetType()
            .GetProperty("UseWaitCursor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
            .SetValue(null, false);

        _logger?.Info("WinForms initialized with performance optimizations", "Bootstrap");
    }
}