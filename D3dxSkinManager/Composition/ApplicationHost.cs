using Microsoft.Web.WebView2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Settings;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.System;
using D3dxSkinManager.Modules.System.Services;
using D3dxSkinManager.Modules.Tools;
using D3dxSkinManager.Modules.Launch;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Plugins;
using D3dxSkinManager.Modules.TaskQueue;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Composition;

/// <summary>
/// Main application host that manages the form and WebView2
/// </summary>
public class ApplicationHost
{
    private Form _mainForm = null!;
    private WebView2 _webView = null!;
    private WebViewInitializer _webViewInitializer = null!;
    private IpcCommunicationHandler _ipcHandler = null!;
    private EventBusIpcBridge _eventBridge = null!;
    private MessageDispatcher _messageDispatcher = null!;
    private ServiceProvider _serviceProvider = null!;
    private ProfileServiceRouter _profileRouter = null!;
    private IPerformanceMonitor _performanceMonitor = null!;
    private DropZoneManager _dropZoneManager = null!;
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

        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Update logger and get window state service
        _logger = _serviceProvider.GetRequiredService<ILogHelper>();
        _windowStateService = _serviceProvider.GetRequiredService<IWindowStateService>();

        _logger.Info("Services initialized", "Host");
    }

    /// <summary>
    /// Create and configure the main application form
    /// </summary>
    public void CreateMainForm()
    {
        _logger.Info("Creating main form...", "Host");

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

            // Get custom scheme handler from DI
            var schemeHandler = _serviceProvider.GetRequiredService<ICustomSchemeHandler>();

            // Initialize WebView2 with custom scheme handler
            _webViewInitializer = new WebViewInitializer(_webView, schemeHandler);
            await _webViewInitializer.InitializeAsync();

            _performanceMonitor.StopOperation("WebView2.Initialize");

            // Navigate to app
            _webViewInitializer.NavigateToApp();

            // Initialize IPC communication
            _logger.Info("Initializing IPC communication...", "Host");
            _ipcHandler = new IpcCommunicationHandler(_webView, _logger);
            _ipcHandler.Initialize();

            // Initialize Drop Zone Manager
            _logger.Info("Initializing drop zone manager...", "Host");
            _dropZoneManager = new DropZoneManager(_webView, _mainForm, _logger, _ipcHandler);

            // Initialize EventBus IPC Bridge (forwards backend events to frontend)
            _logger.Info("Initializing EventBus IPC Bridge...", "Host");
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            _eventBridge = new EventBusIpcBridge(eventBus, _ipcHandler, _logger);
            _eventBridge.Initialize();

            // Subscribe to window state reset events
            eventBus.RegisterHandler(ModuleNames.SETTING, SettingsEvents.WINDOW_STATE_RESET, async (eventMessage) =>
            {
                _logger.Info("Received window state reset event", "Host");
                await HandleWindowStateResetAsync(eventMessage);
            });

            // Initialize Profile Service Router
            _logger.Info("Initializing profile service router...", "Host");
            _profileRouter = new ProfileServiceRouter(_serviceProvider, _logger);
            ConfigureProfileRouter();

            // Initialize Message Dispatcher with middleware pipeline
            _logger.Info("Initializing message dispatcher...", "Host");
            _messageDispatcher = new MessageDispatcher(_ipcHandler, _logger);
            ConfigureMessagePipeline();
            _messageDispatcher.Initialize();

            // TODO: Initialize other components here
            // - Mod Service
            // - Profile Service
            // - etc.

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

        // Shutdown EventBus IPC Bridge
        _eventBridge?.Shutdown();

        // TODO: Clean up components
        // - Close connections
        // - Dispose resources

        _logger.Info("Cleanup completed", "Host");
    }


    /// <summary>
    /// Configure application services
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        _logger?.Info("Configuring services...", "Host");

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
            .MapFacade<IToolsFacade>(ModuleNames.TOOL, services => services.AddToolsServices())
            .MapFacade<ILaunchFacade>("LAUNCH", services => services.AddLaunchServices())
            .MapFacade<IMigrationFacade>("MIGRATION", services => services.AddMigrationServices())
            .MapFacade<IPluginsFacade>(ModuleNames.PLUGIN, services => services.AddPluginsServices())
            .MapFacade<ITaskQueueFacade>("TASK_QUEUE", services => services.AddTaskQueueServices());

        _logger.Info("Profile router configured", "Host");
    }

    /// <summary>
    /// Configure the message processing pipeline
    /// </summary>
    private void ConfigureMessagePipeline()
    {
        _logger.Info("Configuring message pipeline...", "Host");

        // Add error handling middleware (first in pipeline)
        _messageDispatcher.UseErrorHandler();

        // Add logging middleware
        _messageDispatcher.UseLogging();

        // Add profile routing middleware (before global facades)
        _messageDispatcher.UseProfileRouter(_profileRouter);

        // Register global facade handlers
        _messageDispatcher.UseSettingsFacade(_serviceProvider);
        _messageDispatcher.UseSystemFacade(_serviceProvider);
        _messageDispatcher.UseProfileFacade(_serviceProvider);

        // Register built-in module routes (APP, TEST, DROP_ZONE)
        _messageDispatcher.MapModule("APP", routes =>
        {
            routes.Route("PING", message => new { message = "pong", timestamp = DateTime.UtcNow });

            routes.Route("GET_VERSION", message => new
            {
                version = "1.0.0",
                dotnet = Environment.Version.ToString(),
                os = Environment.OSVersion.ToString()
            });

            routes.Route("GET_STATUS", message => new
            {
                status = "ready",
                uptime = DateTime.UtcNow.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
            });
        });

        // Register DROP_ZONE module for managing WinForms drop overlays
        _messageDispatcher.MapModule("DROP_ZONE", routes =>
        {
            routes.Route("REGISTER", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;

                _dropZoneManager.RegisterZone(zoneId, x, y, width, height);
                return new { success = true, zoneId };
            });

            routes.Route("UPDATE", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                var x = message.Payload?.GetProperty("x").GetInt32() ?? 0;
                var y = message.Payload?.GetProperty("y").GetInt32() ?? 0;
                var width = message.Payload?.GetProperty("width").GetInt32() ?? 0;
                var height = message.Payload?.GetProperty("height").GetInt32() ?? 0;

                _dropZoneManager.UpdateZoneBounds(zoneId, x, y, width, height);
                return new { success = true };
            });

            routes.Route("SHOW", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                _dropZoneManager.ShowZone(zoneId);
                return new { success = true };
            });

            routes.Route("HIDE", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                _dropZoneManager.HideZone(zoneId);
                return new { success = true };
            });

            routes.Route("UNREGISTER", message =>
            {
                var zoneId = message.Payload?.GetProperty("zoneId").GetString() ?? "";
                _dropZoneManager.UnregisterZone(zoneId);
                return new { success = true };
            });
        });

        // Register TEST module routes
        _messageDispatcher.MapModule("TEST", routes =>
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