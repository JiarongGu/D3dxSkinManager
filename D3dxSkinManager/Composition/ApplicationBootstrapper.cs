using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Composition;

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
        // Create AppEnvironment for bootstrap phase
        var appEnv = AppEnvironment.Create(AppDomain.CurrentDomain.BaseDirectory);
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

        // Create and run application host
        var host = new ApplicationHost(appEnv, _logger);
        host.CreateMainForm();
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