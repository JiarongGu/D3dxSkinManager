using Microsoft.Web.WebView2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Category;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.System;
using D3dxSkinManager.Modules.System.Services;
using D3dxSkinManager.Modules.Tool;
using D3dxSkinManager.Modules.Launch;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Workflow;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Plugin;
using D3dxSkinManager.Modules.Setting;
using D3dxSkinManager.Modules.Setting.Services;
using D3dxSkinManager.Infrastructure.WebView;
using D3dxSkinManager.Infrastructure.Resources;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Main application host that manages the form and WebView2
/// </summary>
public class ApplicationHost
{
    // Session management
    private WebViewSessionManager _sessionManager = null!;
    private const string MAIN_SESSION_ID = "main";

    // Main window components
    private Form _mainForm = null!;
    private WebView2 _webView = null!;
    private SplashScreenPanel? _splashScreenPanel;

    // Shared services
    private ServiceProvider _serviceProvider = null!;
    private ProfileServiceRouter _profileRouter = null!;
    private IPerformanceMonitor _performanceMonitor = null!;
    private IWindowStateService _windowStateService = null!;
    private ILogHelper _logger;
    private readonly IAppEnvironment _environment;

    public ApplicationHost(IAppEnvironment environment, ILogHelper logHelper)
    {
        _logger = logHelper;
        _environment = environment;
    }

    public Form MainForm => _mainForm;

    /// <summary>
    /// Initialize services before creating the form (for early window state loading)
    /// </summary>
    public void InitializeServices()
    {
        _logger.Info("Initializing services early for window state...", "Host");
        var services = new ServiceCollection();

        // Register IAppEnvironment as interface for DI
        services.AddSingleton(_environment);

        // Create EmbeddedResourceProvider immediately to start background preloading
        // This happens BEFORE ConfigureServices, so preloading starts as early as possible
        var embeddedResourceProvider = new Resources.EmbeddedResourceProvider();
        services.AddSingleton<IEmbeddedResourceProvider>(embeddedResourceProvider);

        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Update logger and get window state service
        _logger = _serviceProvider.GetRequiredService<ILogHelper>();
        _windowStateService = _serviceProvider.GetRequiredService<IWindowStateService>();

        _logger.Info("Services initialized", "Host");
    }

    /// <summary>
    /// Show splash screen panel overlay
    /// </summary>
    private void ShowSplashScreen()
    {
        // Default to dark theme (most users prefer dark)
        // Frontend will update theme once settings are loaded if needed
        _splashScreenPanel = new SplashScreenPanel(true);
        _splashScreenPanel.UpdateStatus("Initializing application...");

        _logger.Info("Splash screen panel created (dark theme default)", "Host");
    }

    /// <summary>
    /// Hide and dispose splash screen panel
    /// </summary>
    public void HideSplashScreen()
    {
        if (_splashScreenPanel != null && _mainForm != null)
        {
            _logger.Info("Hiding splash screen panel", "Host");

            if (_mainForm.InvokeRequired)
            {
                _mainForm.Invoke(new Action(() =>
                {
                    _mainForm.Controls.Remove(_splashScreenPanel);
                    _splashScreenPanel.Dispose();
                    _splashScreenPanel = null;
                }));
            }
            else
            {
                _mainForm.Controls.Remove(_splashScreenPanel);
                _splashScreenPanel.Dispose();
                _splashScreenPanel = null;
            }
        }
    }

    /// <summary>
    /// Create and configure the main application form
    /// </summary>
    public void CreateMainForm()
    {
        _logger.Info("Creating main form...", "Host");

        // Create splash screen panel (will be added after form is ready)
        ShowSplashScreen();

        // Load window state BEFORE creating the form to prevent visual jump
        var (width, height, x, y, maximized) = _windowStateService.LoadWindowStateAsync().GetAwaiter().GetResult();

        // Suspend layout during form creation for better performance
        _mainForm = new OptimizedForm();
        _mainForm.SuspendLayout();

        _mainForm.Text = "D3dxSkinManager";

        // Apply loaded window state immediately
        _mainForm.Width = width;
        _mainForm.Height = height;
        _mainForm.StartPosition = FormStartPosition.Manual;
        _mainForm.BackColor = Color.FromArgb(26, 26, 26); // Match WebView2 background

        // Apply position if saved and valid
        if (x.HasValue && y.HasValue)
        {
            if (_windowStateService.IsPositionValid(x.Value, y.Value, width, height, _mainForm))
            {
                _mainForm.Left = x.Value;
                _mainForm.Top = y.Value;
                _logger.Info($"Applied saved window position: ({x.Value}, {y.Value})", "Host");
            }
            else
            {
                CenterFormOnScreen();
                _logger.Info("Saved position invalid, centered on screen", "Host");
            }
        }
        else
        {
            CenterFormOnScreen();
            _logger.Info("No saved position, centered on screen", "Host");
        }

        // Apply maximized state
        if (maximized)
        {
            _mainForm.WindowState = FormWindowState.Maximized;
            _logger.Info("Applied maximized state", "Host");
        }

        // Create WebView2 control
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 26, 26) // Prevent white flash
        };

        _mainForm.Controls.Add(_webView);

        // Add splash screen panel on top of WebView
        if (_splashScreenPanel != null)
        {
            _mainForm.Controls.Add(_splashScreenPanel);
            _splashScreenPanel.BringToFront();
            _logger.Info("Splash screen panel added to form", "Host");
        }

        // Wire up form events
        _mainForm.Load += OnFormLoad;
        _mainForm.FormClosed += OnFormClosed;
        _mainForm.Resize += OnFormResize;

        // Note: File drops are handled by DropZoneManager with transparent overlays
        // positioned by the frontend to match web elements exactly

        // Resume layout after all controls are added
        _mainForm.ResumeLayout(false);
        _mainForm.PerformLayout();

        // Set the main form reference in FormInteractionService for dialog blocking
        var formInteractionService = _serviceProvider.GetRequiredService<IFormInteractionService>();
        formInteractionService.SetMainForm(_mainForm);

        _logger.Info("Main form created with double buffering enabled", "Host");
    }

    /// <summary>
    /// Initialize all components when form loads
    /// </summary>
    private async void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            _logger.Info("Form loaded, initializing components...", "Host");

            // Services already initialized in InitializeServices(), just get remaining dependencies
            _performanceMonitor = _serviceProvider.GetRequiredService<IPerformanceMonitor>();

            // Track WebView2 initialization performance
            _performanceMonitor.StartOperation("WebView2.Initialize");

            // Initialize Profile Service Router (shared across all sessions)
            _logger.Info("Initializing profile service router...", "Host");
            _profileRouter = new ProfileServiceRouter(_serviceProvider, _logger);
            ConfigureProfileRouter();

            // Configure global MessageDispatcher pipeline (singleton, shared across all sessions)
            _logger.Info("Configuring global message dispatcher...", "Host");
            ConfigureMessagePipeline();

            // Initialize Session Manager
            _logger.Info("Initializing session manager...", "Host");
            _sessionManager = new WebViewSessionManager(_logger);

            // Create main WebView session
            await CreateMainSessionAsync();

            _performanceMonitor.StopOperation("WebView2.Initialize");

            // Subscribe to window state reset events
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            eventBus.RegisterHandler(ModuleNames.SETTING, SettingEvents.WINDOW_STATE_RESET, async (eventMessage) =>
            {
                _logger.Info("Received window state reset event", "Host");
                await HandleWindowStateResetAsync(eventMessage);
            });

            _logger.Info("All components initialized", "Host");
        }
        catch (Exception ex)
        {
            _logger.Error($"Initialization error: {ex.Message}", "Host", ex);

            MessageBox.Show(
                $"Failed to initialize application:\n\n{ex.Message}",
                "Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Create and start the main WebView session
    /// </summary>
    private async Task CreateMainSessionAsync()
    {
        _logger.Info("Creating main WebView session...", "Host");

        var session = _sessionManager.Create(MAIN_SESSION_ID, () =>
        {
            var schemeHandler = _serviceProvider.GetRequiredService<ICustomSchemeHandler>();

            // Create session - it will automatically wire up to the global singleton MessageDispatcher
            var newSession = new WebViewSession(
                MAIN_SESSION_ID,
                _webView,
                _logger,
                _serviceProvider,
                schemeHandler,
                _mainForm
            );
            return newSession;
        });

        // Register session-specific routes (WEBVIEW_READY, SUBSCRIBE, DROP_ZONE, etc.)
        RegisterSessionRoutes(session);

        await session.StartAsync();

        _logger.Info("Main WebView session created and started", "Host");
    }

    /// <summary>
    /// Center the form on the primary screen
    /// </summary>
    private void CenterFormOnScreen()
    {
        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            var workingArea = screen.WorkingArea;
            _mainForm.Left = workingArea.Left + (workingArea.Width - _mainForm.Width) / 2;
            _mainForm.Top = workingArea.Top + (workingArea.Height - _mainForm.Height) / 2;
        }
    }

    /// <summary>
    /// Handle window state reset event from SettingsFacade
    /// </summary>
    private Task HandleWindowStateResetAsync(EventMessage eventMessage)
    {
        try
        {
            var data = eventMessage.Payload as dynamic;
            if (data == null)
            {
                _logger.Warn("Window state reset event received with no data", "Host");
                return Task.CompletedTask;
            }

            int width = data.Width;
            int height = data.Height;

            _logger.Info($"Applying window state reset: {width}x{height}", "Host");

            // Apply changes on UI thread
            if (_mainForm.InvokeRequired)
            {
                _mainForm.Invoke(() => ApplyWindowStateReset(width, height));
            }
            else
            {
                ApplyWindowStateReset(width, height);
            }

            _logger.Info("Window state reset applied successfully", "Host");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to apply window state reset: {ex.Message}", "Host", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Apply window state reset to the form (must be called on UI thread)
    /// </summary>
    private void ApplyWindowStateReset(int width, int height)
    {
        // Reset to normal state first if maximized
        if (_mainForm.WindowState == FormWindowState.Maximized)
        {
            _mainForm.WindowState = FormWindowState.Normal;
        }

        // Apply size
        _mainForm.Width = width;
        _mainForm.Height = height;

        // Center the window on primary screen
        CenterFormOnScreen();

        _logger.Info($"Window reset to {width}x{height} and centered", "Host");
    }

    /// <summary>
    /// Handle form resize to optimize WebView2 rendering
    /// </summary>
    private void OnFormResize(object? sender, EventArgs e)
    {
        // Suspend WebView2 layout during resize for smoother performance
        if (_webView?.IsHandleCreated == true && _mainForm.WindowState != FormWindowState.Minimized)
        {
            _webView.SuspendLayout();
            _webView.ResumeLayout(false);
        }
    }

    /// <summary>
    /// Clean up when form closes
    /// </summary>
    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _logger.Info("Form closed, cleaning up...", "Host");

        try
        {
            // Save window state
            if (_windowStateService != null)
            {
                try
                {
                    _windowStateService.SaveWindowStateAsync(_mainForm).Wait();
                    _logger.Info("Window state saved", "Host");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to save window state: {ex.Message}", "Host", ex);
                }
            }

            // Dispose all WebView sessions
            if (_sessionManager != null)
            {
                _sessionManager.Remove(MAIN_SESSION_ID);
                _logger.Info("All sessions disposed", "Host");
            }

            // Dispose service provider
            if (_serviceProvider != null)
            {
                _serviceProvider.Dispose();
                _logger.Info("Service provider disposed", "Host");
            }

            _logger.Info("Cleanup completed", "Host");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error during cleanup: {ex.Message}", "Host", ex);
        }
    }


    /// <summary>
    /// Configure application services
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        _logger?.Info("Configuring services...", "Host");

        // Note: EmbeddedResourceProvider is registered in InitializeServices() before ConfigureServices
        // to enable early background preloading of embedded resources

        // Register global facades (no profile dependency)
        services.AddCoreServices();
        services.AddSettingsServices();
        services.AddSystemServices();
        services.AddProfileServices();

        _logger?.Info("Services configured", "Host");
    }

    /// <summary>
    /// Configure the profile service router
    /// </summary>
    private void ConfigureProfileRouter()
    {
        _logger.Info("Configuring profile router...", "Host");

        // Register profile-scoped facades
        _profileRouter
            .MapFacade<IModFacade>("MOD", services => services.AddModsServices())
            .MapFacade<ICategoryFacade>(ModuleNames.CATEGORY, services => services.AddCategoryServices())
            .MapFacade<IToolFacade>(ModuleNames.TOOL, services => services.AddToolsServices())
            .MapFacade<ILaunchFacade>("LAUNCH", services => services.AddLaunchServices())
            .MapFacade<IMigrationFacade>("MIGRATION", services => services.AddMigrationServices())
            .MapFacade<IPluginFacade>(ModuleNames.PLUGIN, services => services.AddPluginsServices())
            .MapFacade<IWorkflowFacade>(ModuleNames.WORKFLOW, services => services.AddWorkflowServices());

        _logger.Info("Profile router configured", "Host");
    }

    /// <summary>
    /// Configure the global singleton message dispatcher pipeline
    /// </summary>
    private void ConfigureMessagePipeline()
    {
        var dispatcher = _serviceProvider.GetRequiredService<MessageDispatcher>();
        _logger.Info("Configuring global message dispatcher pipeline...", "Host");

        // Add error handling middleware (first in pipeline)
        dispatcher.UseErrorHandler();

        // Add logging middleware
        dispatcher.UseLogging();

        // Add profile routing middleware (before global facades)
        dispatcher.UseProfileRouter(_profileRouter);

        // Register global facade handlers
        dispatcher.UseSettingsFacade(_serviceProvider);
        dispatcher.UseSystemFacade(_serviceProvider);
        dispatcher.UseProfileFacade(_serviceProvider);
        dispatcher.UsePluginFacade(_serviceProvider);

        // Session-specific routes (WEBVIEW_READY, DROP_ZONE)
        // will be registered by ApplicationHost.RegisterSessionRoutes() after session creation
        // This keeps the global dispatcher clean while allowing per-session operations

        _logger.Info("Global message dispatcher configured", "Host");
    }

    /// <summary>
    /// Register session-specific routes for the main session
    /// These routes need access to the specific WebViewSession's IPC and DropZone
    /// </summary>
    private void RegisterSessionRoutes(WebViewSession session)
    {
        var dispatcher = _serviceProvider.GetRequiredService<MessageDispatcher>();
        _logger.Info($"[{session.SessionId}] Registering session-specific routes...", "Host");

        // Register session-specific APP routes
        dispatcher.MapModule("APP", routes =>
        {
            routes.Route("WEBVIEW_READY", message =>
            {
                var webViewId = message.Payload?.GetProperty("webViewId").GetString() ?? "unknown";
                _logger.Info($"WebView ready notification received with ID: {webViewId}", "Host");

                // Clear all drop zones on webview startup/hot-reload
                _logger.Info("Clearing all drop zones due to webview startup", "Host");
                session.DropZone?.ClearAll();

                return new { success = true, webViewId };
            });

            routes.Route("INITIALIZED", message =>
            {
                _logger.Info("Frontend application initialized - hiding splash screen", "Host");

                // Hide the splash screen now that the app is ready
                HideSplashScreen();

                return new { success = true };
            });
        });

        // Register DROP_ZONE module for managing WinForms drop overlays
        dispatcher.MapModule("DROP_ZONE", routes =>
        {
            routes.Route("REGISTER", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;

                session.DropZone.RegisterZone(zoneId, x, y, width, height);
                return new { success = true, zoneId };
            });

            routes.Route("UPDATE", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;

                session.DropZone.UpdateZoneBounds(zoneId, x, y, width, height);
                return new { success = true };
            });

            routes.Route("SHOW", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                session.DropZone.ShowZone(zoneId);
                return new { success = true };
            });

            routes.Route("HIDE", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                session.DropZone.HideZone(zoneId);
                return new { success = true };
            });

            routes.Route("UNREGISTER", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                session.DropZone.UnregisterZone(zoneId);
                return new { success = true };
            });
        });

        // Register TEST module routes
        dispatcher.MapModule("TEST", routes =>
        {
            routes.Route("ECHO", message => message.Payload);

            routes.RouteAsync("DELAY", async message =>
            {
                await Task.Delay(1000);
                return new { message = "Delay completed", delayMs = 1000 };
            });

            routes.Route("ERROR", message => throw new Exception("Test error requested"));
        });

        _logger.Info("Message pipeline configured", "Host");
    }

    /// <summary>
    /// Run the application
    /// </summary>
    public void Run()
    {
        _logger?.Info("Starting application...", "Host");
        Application.Run(_mainForm);
        _logger?.Info("Application ended", "Host");
    }
}