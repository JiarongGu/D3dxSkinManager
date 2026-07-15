using Microsoft.Web.WebView2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Category;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.System;
using D3dxSkinManager.Modules.Tool;
using D3dxSkinManager.Modules.Launch;
using D3dxSkinManager.Modules.Remote;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Workflow;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Cleanup;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Plugin;
using D3dxSkinManager.Modules.Setting;
using D3dxSkinManager.Modules.Setting.Services;
using D3dxSkinManager.Modules.Core.WebView;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Main application host that manages the form and WebView2
/// </summary>
public class ApplicationHost
{
    // Session management
    private IWebViewSessionManager _sessionManager = null!;
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

    // Throttle form resize events to improve performance
    private WinFormsDebounce? _resizeDebounce;

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
        // This happens BEFORE ConfigureServices, so preloading starts as early as possible.
        // Pass the install ROOT (IAppEnvironment.BaseDirectory), not AppDomain.BaseDirectory. See launcher-topology.md.
        var embeddedResourceProvider = new EmbeddedResourceProvider(_environment.BaseDirectory);
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
        // Theme will be loaded properly when WebView initializes
        _splashScreenPanel = new SplashScreenPanel(true);
        _splashScreenPanel.UpdateStatus("Initializing application...");

        _logger.Info("Splash screen panel created (dark theme default)", "Host");
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
        var optimizedForm = new OptimizedForm();
        _mainForm = optimizedForm;
        _mainForm.SuspendLayout();

        // A 2nd launch of this install broadcasts the single-instance activation message; catch it here
        // and bring this window to the front (see SingleInstanceGuard).
        optimizedForm.WndProcHook = msg =>
        {
            if (SingleInstanceGuard.ActivateMessageId != 0 && (uint)msg == SingleInstanceGuard.ActivateMessageId)
            {
                ActivateMainWindow();
                return true;
            }
            return false;
        };

        _mainForm.Text = "D3dxSkinManager";

        // Set window icon from embedded resource
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var iconStream = assembly.GetManifestResourceStream("D3dxSkinManager.favicon.ico");
            if (iconStream != null)
            {
                _mainForm.Icon = new Icon(iconStream);
                _logger.Info("Window icon loaded from embedded resource", "Host");
            }
            else
            {
                _logger.Warn("Embedded icon resource not found", "Host");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load window icon: {ex.Message}", "Host");
        }

        // Apply loaded window state immediately. width/height are already PHYSICAL px for the current
        // monitor DPI (WindowStateService.ToPhysicalState). The 800x600 minimum is LOGICAL, so scale it by
        // the same DPI (WinForms window px are device px + are not auto-scaled from a logical baseline).
        var dpiScale = DpiHelper.GetDpiScaleFactor();
        _mainForm.Width = width;
        _mainForm.Height = height;
        _mainForm.MinimumSize = new Size((int)Math.Round(800 * dpiScale), (int)Math.Round(600 * dpiScale));
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

            // Bind the router into the Core accessor so global services (e.g. ProfileBundleService)
            // can reach any profile's scoped services for cross-profile export/import. Done here because
            // the router is created after the root container is built and so cannot be DI-registered.
            _serviceProvider.GetRequiredService<ProfileServiceProviderAccessor>().Bind(_profileRouter);

            // Configure global MessageDispatcher pipeline (singleton, shared across all sessions)
            _logger.Info("Configuring global message dispatcher...", "Host");
            ConfigureMessagePipeline();

            // Self-cleanup of transient leftovers (stale downloads, orphaned update staging, stale
            // process entries from a previous session). Non-fatal — never blocks startup.
            var cleanupSw = global::System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _serviceProvider.GetRequiredService<IStartupCleanupService>().RunAsync();
            }
            catch (Exception ex)
            {
                _logger.Warn($"Startup cleanup failed (non-critical): {ex.Message}", "Host");
            }
            cleanupSw.Stop();
            Console.WriteLine($"[Startup] StartupCleanup took {cleanupSw.ElapsedMilliseconds}ms");

            // Eager loading (DB + category-tree cache warm, ~375ms) and WebView session creation
            // (EnsureCoreWebView2Async — the ~600-800ms controller spawn) are INDEPENDENT. Run them
            // concurrently: both are async and yield the UI thread on their I/O waits, so the cache
            // warm overlaps the controller creation instead of summing serially. Measured: this is the
            // real every-launch win (env CreateAsync was already cheap; see WebView2EnvironmentPrewarmer).
            var overlapSw = global::System.Diagnostics.Stopwatch.StartNew();

            // Start eager loading WITHOUT awaiting — it warms caches React reads after it has mounted,
            // which is well after navigation, so it does not need to complete before the session starts.
            // Task.Run offloads its continuations to the thread pool so it does NOT compete with the
            // WebView controller creation (EnsureCoreWebView2Async) for the UI thread. Progress callbacks
            // still marshal back to the UI thread via the Progress<T> captured inside.
            var eagerTask = Task.Run(() => PerformEagerLoadingAsync());

            // Get Session Manager from DI
            _logger.Info("Getting session manager from DI...", "Host");
            _sessionManager = _serviceProvider.GetRequiredService<IWebViewSessionManager>();

            // Create main WebView session (runs concurrently with eager loading above)
            await CreateMainSessionAsync();

            // Make sure cache warming finished before we report init complete.
            await eagerTask;

            overlapSw.Stop();
            Console.WriteLine($"[Startup] EagerLoading+Session (overlapped) took {overlapSw.ElapsedMilliseconds}ms");

            _performanceMonitor.StopOperation("WebView2.Initialize");

            // Subscribe to window state reset events
            var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
            eventBus.Subscribe(ModuleNames.SETTING, SettingEvents.WINDOW_STATE_RESET, async (eventMessage) =>
            {
                _logger.Info("Received window state reset event", "Host");
                await HandleWindowStateResetAsync(eventMessage);
            });

            // Subscribe to profile switch events to close all secondary windows (e.g., screen capture control panels)
            eventBus.Subscribe(ModuleNames.PROFILE, ProfileEvents.SWITCHED, async (eventMessage) =>
            {
                _logger.Info("Received profile switched event, closing all secondary windows", "Host");
                _profileRouter.CloseAllSecondaryWindows();
                await Task.CompletedTask;
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

            // Create session with splash screen - it will automatically register session-specific routes
            var newSession = new WebViewSession(
                MAIN_SESSION_ID,
                _webView,
                _logger,
                _serviceProvider,
                schemeHandler,
                _mainForm,
                _splashScreenPanel  // Pass splash screen to session
            );
            return newSession;
        });

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
    /// Debounced to reduce excessive layout operations during rapid resize
    /// </summary>
    private void OnFormResize(object? sender, EventArgs e)
    {
        // Initialize debounce on first use
        _resizeDebounce ??= new WinFormsDebounce(50); // 50ms debounce

        // Debounce the layout operations to avoid excessive reflows during drag-resize
        _resizeDebounce.Execute(() =>
        {
            // Suspend WebView2 layout during resize for smoother performance
            if (_webView?.IsHandleCreated == true && _mainForm.WindowState != FormWindowState.Minimized)
            {
                _webView.SuspendLayout();
                _webView.ResumeLayout(false);
            }
        });
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
                    // Block until saved — window geometry MUST be persisted before the app exits. Safe on
                    // the UI thread: SaveWindowStateAsync reads the form's UI-affine properties FIRST (before
                    // any await) and every await inside uses ConfigureAwait(false), so no continuation needs
                    // the UI thread → .Wait() can't deadlock. (Do NOT wrap in Task.Run — that would run the
                    // form-property reads on a pool thread. See WindowStateService.SaveWindowStateAsync.)
                    _windowStateService.SaveWindowStateAsync(_mainForm).Wait();
                    _logger.Info("Window state saved", "Host");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to save window state: {ex.Message}", "Host", ex);
                }
            }

            // Dispose ProfileServiceRouter (which disposes all profile-scoped services including SecondaryWindowService)
            if (_profileRouter != null)
            {
                try
                {
                    _logger.Info("Disposing ProfileServiceRouter (closes all secondary windows)...", "Host");
                    _profileRouter.Dispose();
                    _logger.Info("ProfileServiceRouter disposed", "Host");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to dispose ProfileServiceRouter: {ex.Message}", "Host", ex);
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

            // Dispose resize debounce
            _resizeDebounce?.Dispose();

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
        services.AddSettingServices();
        services.AddSystemServices();
        services.AddProfileServices();
        // The plugin REGISTRY is global (shared into every profile container) so global services
        // can consume plugin capabilities; the loader/context stay profile-scoped.
        services.AddPluginRegistry();

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
            .MapFacade<IRemoteFacade>("REMOTE", services => services.AddRemoteServices())
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

        // Note: Session-specific routes (APP/WEBVIEW_READY, DROP_ZONE)
        // are registered by each WebViewSession in its constructor automatically

        // Register TEST module routes for debugging
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

        _logger.Info("Global message dispatcher configured", "Host");
    }


    /// <summary>
    /// Perform eager loading operations during startup
    /// </summary>
    private async Task PerformEagerLoadingAsync()
    {
        _logger.Info("Starting eager loading...", "Host");

        try
        {
            var eagerLoadingService = _serviceProvider.GetRequiredService<IEagerLoadingService>();

            // Create progress handler for splash screen updates
            var progress = new Progress<EagerLoadingProgress>(p =>
            {
                if (_splashScreenPanel != null)
                {
                    _splashScreenPanel.UpdateStatus(p.Operation);
                }
                _logger.Verbose($"Eager loading: {p.Operation} ({p.Percent}%)", "Host");
            });

            // Perform eager loading
            await eagerLoadingService.EagerLoadAsync(progress);

            _logger.Info("Eager loading completed", "Host");
        }
        catch (Exception ex)
        {
            // Non-critical - log and continue
            _logger.Warn($"Eager loading failed (non-critical): {ex.Message}", "Host");
        }
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

    /// <summary>
    /// Bring the main window to the foreground (restoring it if minimized). Invoked when a 2nd instance
    /// of this install is launched and broadcasts the SingleInstanceGuard activation message.
    /// </summary>
    private void ActivateMainWindow()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            return;
        }

        void Bring()
        {
            if (_mainForm.IsDisposed)
            {
                return;
            }
            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }
            _mainForm.Show();
            _mainForm.Activate();
            _mainForm.BringToFront();
            SetForegroundWindow(_mainForm.Handle);
        }

        // The broadcast arrives on the UI thread, but guard anyway.
        if (_mainForm.InvokeRequired)
        {
            _mainForm.BeginInvoke((Action)Bring);
        }
        else
        {
            Bring();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
