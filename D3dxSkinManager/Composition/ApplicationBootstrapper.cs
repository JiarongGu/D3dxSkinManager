using System;
using System.Threading;
using System.Windows.Forms;
using D3dxSkinManager.Modules.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;

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
        // Create a temporary logger for bootstrap phase
        // (Full DI container not available yet)
        _logger = new LogHelper();

        _logger.Info("=== D3dxSkinManager Starting ===", "Bootstrap");
        _logger.Info($"Thread apartment state: {Thread.CurrentThread.GetApartmentState()}", "Bootstrap");

        // Initialize WinForms
        InitializeWinForms();

        // Create and run application host
        var host = new ApplicationHost();
        host.CreateMainForm();
        host.Run();
    }

    /// <summary>
    /// Initialize WinForms with proper settings
    /// </summary>
    private static void InitializeWinForms()
    {
        _logger?.Info("Initializing WinForms...", "Bootstrap");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        _logger?.Info("WinForms initialized", "Bootstrap");
    }
}