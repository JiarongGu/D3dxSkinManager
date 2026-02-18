using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Photino.NET;
using D3dxSkinManager.Configuration;
using D3dxSkinManager.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Models;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager;

/// <summary>
/// Main entry point for D3dxSkinManager application
/// </summary>
class Program
{
    private static IServiceProvider? _serviceProvider;
    private static IAppFacade? _appFacade;
    private static PluginRegistry? _pluginRegistry;
    private static PluginEventBus? _pluginEventBus;
    private static DevelopmentServerManager? _devServer;
    private static ImageServerService? _imageServer;

    // JSON serializer options for camelCase (matches JavaScript conventions)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("=== D3dxSkinManager Starting ===");

        // Initialize services with DI container
        InitializeServices();

        // Load and initialize plugins
        InitializePluginsAsync().Wait();

        // Verify dependencies
        VerifyDependencies();

        // Start development server if in development mode
        StartDevelopmentServerIfNeeded().Wait();

        // Start image HTTP server
        StartImageServer().Wait();

        // Emit ApplicationStarted event
        EmitApplicationStartedEvent().Wait();

        // Create and configure Photino window
        var window = CreateWindow();

        Console.WriteLine($"Application initialized successfully");
        Console.WriteLine($"Press Ctrl+C to exit");
        Console.WriteLine("================================");

        window.WaitForClose();

        // Shutdown
        Shutdown();
    }

    /// <summary>
    /// Starts the React development server if in development mode
    /// </summary>
    private static async Task StartDevelopmentServerIfNeeded()
    {
        // Check if we're in development mode
        var frontendPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(frontendPath, "index.html");
        var isDevelopment = !File.Exists(indexPath);

        if (!isDevelopment)
        {
            Console.WriteLine("[Init] Running in production mode - skipping dev server");
            return;
        }

        // Find the client directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var clientPath = Path.Combine(Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir, "D3dxSkinManager.Client");

        if (!Directory.Exists(clientPath))
        {
            Console.WriteLine($"[Init] Warning: Client directory not found at {clientPath}");
            Console.WriteLine("[Init] Continuing without dev server - ensure React app is running manually");
            return;
        }

        // Start the development server
        _devServer = new DevelopmentServerManager(clientPath);
        var started = await _devServer.StartAsync();

        if (!started)
        {
            Console.WriteLine("[Init] Warning: Failed to start development server");
            Console.WriteLine("[Init] Please ensure Node.js and npm are installed and try starting manually:");
            Console.WriteLine($"[Init]   cd {clientPath}");
            Console.WriteLine("[Init]   npm install");
            Console.WriteLine("[Init]   npm start");
        }
    }

    /// <summary>
    /// Starts the HTTP image server
    /// </summary>
    private static async Task StartImageServer()
    {
        if (_serviceProvider == null)
        {
            Console.WriteLine("[Init] Warning: Service provider not initialized, cannot start image server");
            return;
        }

        _imageServer = _serviceProvider.GetRequiredService<ImageServerService>();
        await _imageServer.StartAsync();
    }

    /// <summary>
    /// Performs cleanup on shutdown
    /// </summary>
    private static void Shutdown()
    {
        Console.WriteLine("[Shutdown] Application shutting down...");

        // Shutdown plugins
        ShutdownPluginsAsync().Wait();

        // Stop image server
        if (_imageServer != null)
        {
            _imageServer.Dispose();
            _imageServer = null;
        }

        // Stop development server
        if (_devServer != null)
        {
            _devServer.Dispose();
            _devServer = null;
        }

        Console.WriteLine("[Shutdown] Complete");
    }

    /// <summary>
    /// Initializes all application services using .NET DI container
    /// </summary>
    private static void InitializeServices()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Console.WriteLine($"[Init] Data path: {dataPath}");

        // Ensure data directory exists
        Directory.CreateDirectory(dataPath);

        // Build service container
        var services = new ServiceCollection();
        services.AddD3dxSkinManagerServices(dataPath);

        _serviceProvider = services.BuildServiceProvider();

        // Resolve the top-level application facade (handles all IPC routing)
        _appFacade = _serviceProvider.GetRequiredService<IAppFacade>();

        // Resolve plugin infrastructure
        _pluginRegistry = _serviceProvider.GetRequiredService<PluginRegistry>();
        _pluginEventBus = _serviceProvider.GetRequiredService<PluginEventBus>();

        Console.WriteLine("[Init] Services and facades initialized with DI container");
        Console.WriteLine("[Init] AppFacade registered for IPC message routing");
    }

    /// <summary>
    /// Loads and initializes plugins
    /// </summary>
    private static async Task InitializePluginsAsync()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialized");
        }

        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        var pluginsPath = Path.Combine(dataPath, "plugins");

        // Ensure plugins directory exists
        Directory.CreateDirectory(pluginsPath);

        var logger = _serviceProvider.GetRequiredService<ILogger>();
        var registry = _serviceProvider.GetRequiredService<PluginRegistry>();
        var context = _serviceProvider.GetRequiredService<PluginContext>();

        // For proper plugin support with service registration, we'd need to load plugins
        // before building the service provider. For now, we create a temporary collection.
        var tempServices = new ServiceCollection();
        var pluginLoader = new PluginLoader(pluginsPath, registry, tempServices, logger);

        // Load plugins from directory
        var loadedCount = await pluginLoader.LoadPluginsAsync();

        // Initialize plugins
        await pluginLoader.InitializePluginsAsync(context);

        Console.WriteLine($"[Init] Loaded and initialized {loadedCount} plugin(s)");
    }

    /// <summary>
    /// Emits the ApplicationStarted event
    /// </summary>
    private static async Task EmitApplicationStartedEvent()
    {
        if (_pluginEventBus != null)
        {
            await _pluginEventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ApplicationStarted
            });
        }
    }

    /// <summary>
    /// Shutdown all plugins
    /// </summary>
    private static async Task ShutdownPluginsAsync()
    {
        if (_pluginEventBus != null)
        {
            await _pluginEventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.ApplicationShutdown
            });
        }

        if (_pluginRegistry != null)
        {
            var plugins = _pluginRegistry.GetAllPlugins().ToList();
            foreach (var plugin in plugins)
            {
                try
                {
                    await plugin.ShutdownAsync();
                    Console.WriteLine($"[Shutdown] Plugin shut down: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Shutdown] Error shutting down plugin {plugin.Name}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Verifies required dependencies are available and performs startup validation
    /// </summary>
    private static void VerifyDependencies()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialized");
        }

        // Archive extraction now uses SharpCompress (pure .NET library)
        // No external dependencies needed - all formats supported natively
        Console.WriteLine("[Init] Archive extraction ready (SharpCompress: ZIP, RAR, 7Z, TAR, GZIP)");

        // Run startup validation
        Console.WriteLine("[Init] Running startup validation checks...");
        var validationService = _serviceProvider.GetRequiredService<IStartupValidationService>();
        var validationReport = validationService.ValidateStartupAsync().Result;

        // Display validation results
        foreach (var result in validationReport.Results)
        {
            var symbol = result.IsValid ? "✓" : "✗";
            var color = result.IsValid ? "" :
                result.Severity == ValidationSeverity.Error ? "[ERROR] " :
                result.Severity == ValidationSeverity.Warning ? "[WARNING] " : "";

            Console.WriteLine($"[Init] {symbol} {result.CheckName}: {color}{result.Message}");
        }

        // Show summary
        if (validationReport.IsValid)
        {
            Console.WriteLine($"[Init] Startup validation passed ({validationReport.WarningCount} warnings)");
        }
        else
        {
            Console.WriteLine($"[Init] Startup validation failed: {validationReport.ErrorCount} errors");
            Console.WriteLine("[Init] Application may not function correctly. Please check the errors above.");
        }
    }

    /// <summary>
    /// Creates and configures the Photino window
    /// </summary>
    private static PhotinoWindow CreateWindow()
    {
        var frontendPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(frontendPath, "index.html");

        // Determine if running in development or production mode
        var isDevelopment = !File.Exists(indexPath);
        var startUrl = isDevelopment
            ? "http://localhost:3000"
            : $"file:///{indexPath.Replace("\\", "/")}";

        Console.WriteLine($"[Init] Mode: {(isDevelopment ? "Development" : "Production")}");
        Console.WriteLine($"[Init] Loading: {startUrl}");

        return new PhotinoWindow()
            .SetTitle("D3dxSkinManager")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 800)
            .SetResizable(true)
            .Center()
            .RegisterWebMessageReceivedHandler(OnWebMessageReceived)
            .Load(startUrl);
    }

    /// <summary>
    /// Handles incoming IPC messages from the frontend
    /// </summary>
    private static async void OnWebMessageReceived(object? sender, string message)
    {
        if (sender is not PhotinoWindow window)
        {
            Console.WriteLine("[IPC] Error: Window not initialized");
            return;
        }

        try
        {
            // Parse incoming message (case-insensitive for incoming)
            var request = JsonSerializer.Deserialize<MessageRequest>(message, JsonOptions);
            if (request is null)
            {
                throw new InvalidOperationException("Failed to deserialize message");
            }

            Console.WriteLine($"[IPC] Request: {request.Type} (ID: {request.Id})");

            MessageResponse response;

            // Check if any plugin can handle this message type
            if (_pluginRegistry != null && _pluginRegistry.CanHandleMessage(request.Type))
            {
                var handler = _pluginRegistry.GetMessageHandler(request.Type);
                if (handler != null)
                {
                    Console.WriteLine($"[IPC] Routing to plugin: {handler.Name}");
                    response = await handler.HandleMessageAsync(request);
                }
                else
                {
                    // Fallback to AppFacade if plugin handler is null
                    if (_appFacade == null)
                    {
                        throw new InvalidOperationException("AppFacade not initialized");
                    }
                    response = await _appFacade.HandleMessageAsync(request);
                }
            }
            else
            {
                // Route to AppFacade (which routes to appropriate module facade)
                if (_appFacade == null)
                {
                    throw new InvalidOperationException("AppFacade not initialized");
                }
                response = await _appFacade.HandleMessageAsync(request);
            }

            // Send response back to frontend (using camelCase)
            var json = JsonSerializer.Serialize(response, JsonOptions);
            window.SendWebMessage(json);

            Console.WriteLine($"[IPC] Response: {(response.Success ? "Success" : "Error")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Error processing message: {ex.Message}");

            // Send error response (using camelCase)
            var errorResponse = MessageResponse.CreateError("unknown", ex.Message);
            var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
            window.SendWebMessage(json);
        }
    }
}
