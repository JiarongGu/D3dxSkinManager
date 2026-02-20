using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Photino.NET;
using SharpSevenZip;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace D3dxSkinManager;

/// <summary>
/// Main entry point for D3dxSkinManager application
/// </summary>
class Program
{
    private static ServiceRouter? _serviceRouter;
    private static DevelopmentServerManager? _devServer;
    private static ICustomSchemeHandler? _schemeHandler;
    private static PhotinoWindow? _mainWindow;
    private static IWindowStateService? _windowStateService;
    private static IOperationNotificationService? _operationNotificationService;

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

        // Initialize 7z library for archive extraction
        Initialize7zLibrary();

        // Start development server if in development mode
        StartDevelopmentServerIfNeeded().Wait();

        // Create and configure Photino window
        _mainWindow = CreateWindow();

        Console.WriteLine($"Application initialized successfully");
        Console.WriteLine($"Press Ctrl+C to exit");
        Console.WriteLine("================================");

        _mainWindow.WaitForClose();

        // Shutdown (includes saving window state)
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

    private static void Shutdown()
    {
        Console.WriteLine("[Shutdown] Application shutting down...");

        // Save window state before closing (fallback if WindowClosingHandler didn't fire)
        if (_mainWindow != null && _windowStateService != null)
        {
            _windowStateService.SaveWindowState(_mainWindow);
        }

        // Stop development server
        if (_devServer != null)
        {
            _devServer.Dispose();
            _devServer = null;
        }

        // Dispose ServiceRouter (cleans up all service providers)
        if (_serviceRouter != null)
        {
            _serviceRouter.Dispose();
            _serviceRouter = null;
        }

        Console.WriteLine("[Shutdown] Complete");
    }

    /// <summary>
    /// Initializes the service router for stateless request handling
    /// </summary>
    private static void InitializeServices()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Console.WriteLine($"[Init] Data path: {dataPath}");

        // Ensure data directory exists
        Directory.CreateDirectory(dataPath);

        // Create ServiceRouter for stateless request routing
        _serviceRouter = new ServiceRouter(dataPath);

        // Get services from DI container
        var globalServices = _serviceRouter.GetGlobalServices();
        _schemeHandler = globalServices.GetRequiredService<ICustomSchemeHandler>();
        _windowStateService = globalServices.GetRequiredService<Modules.Settings.Services.IWindowStateService>();
        _operationNotificationService = globalServices.GetRequiredService<Modules.Core.Services.IOperationNotificationService>();

        // Subscribe to operation notifications and forward to frontend
        _operationNotificationService.OperationNotificationReceived += OnOperationNotificationReceived;

        Console.WriteLine("[Init] ServiceRouter initialized for stateless API request handling");
        Console.WriteLine("[Init] CustomSchemeHandler initialized for app:// URLs");
        Console.WriteLine("[Init] WindowStateService initialized for window persistence");
        Console.WriteLine("[Init] Global services initialized (ProfileService, Settings)");
        Console.WriteLine("[Init] Profile-scoped services ready for request routing");
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

        // Load saved window state using WindowStateService
        var (width, height, x, y, maximized) = _windowStateService?.LoadWindowState()
            ?? (1280, 800, null, null, false);

        var window = new PhotinoWindow()
            .SetTitle("D3dxSkinManager")
            .SetUseOsDefaultSize(false)
            .SetSize(width, height)
            .SetResizable(true)
            .RegisterWebMessageReceivedHandler(OnWebMessageReceived)
            .RegisterCustomSchemeHandler("app", OnAppRequestReceived)
            .Load(startUrl);

        // Register window closing handler to save state before window closes
        window.WindowClosingHandler = (sender, args) =>
        {
            Console.WriteLine("[Window] WindowClosingHandler triggered");
            _windowStateService?.SaveWindowState(window);
            return false; // Allow window to close
        };

        // Apply position if available and valid
        if (x.HasValue && y.HasValue && _windowStateService != null)
        {
            // Validate position is within screen bounds before applying
            if (_windowStateService.IsPositionValid(x.Value, y.Value, width, height, window))
            {
                window.SetLeft(x.Value);
                window.SetTop(y.Value);
                Console.WriteLine($"[Init] Window position restored: X={x.Value}, Y={y.Value}");
            }
            else
            {
                window.Center();
                Console.WriteLine($"[Init] Saved position invalid (screen changed?), centering window");
            }
        }
        else
        {
            window.Center();
        }

        // Apply maximized state if needed
        if (maximized)
        {
            window.SetMaximized(true);
            Console.WriteLine($"[Init] Window restored as maximized");
        }

        Console.WriteLine($"[Init] Window size: {width}x{height}");

        return window;
    }

    /// <summary>
    /// Handles operation notification events from OperationNotificationService
    /// Forwards notifications to frontend via IPC push message
    /// </summary>
    private static void OnOperationNotificationReceived(object? sender, Modules.Core.Models.OperationNotification notification)
    {
        if (_mainWindow == null)
        {
            return;
        }

        try
        {
            // Create a push notification message (no ID needed - this is a one-way push)
            var pushMessage = new
            {
                type = "OPERATION_NOTIFICATION",
                notification = new
                {
                    type = notification.Type.ToString(),
                    operation = new
                    {
                        operationId = notification.Operation.OperationId,
                        operationName = notification.Operation.OperationName,
                        status = notification.Operation.Status.ToString(),
                        percentComplete = notification.Operation.PercentComplete,
                        currentStep = notification.Operation.CurrentStep,
                        startedAt = notification.Operation.StartedAt,
                        completedAt = notification.Operation.CompletedAt,
                        errorMessage = notification.Operation.ErrorMessage,
                        metadata = notification.Operation.Metadata
                    },
                    timestamp = notification.Timestamp
                }
            };

            var json = JsonSerializer.Serialize(pushMessage, JsonOptions);
            _mainWindow.SendWebMessage(json);

            Console.WriteLine($"[IPC] Operation notification pushed: {notification.Type} - {notification.Operation.OperationName} ({notification.Operation.PercentComplete}%)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Error pushing operation notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles custom app:// scheme requests by delegating to CustomSchemeHandler service
    /// </summary>
    private static Stream OnAppRequestReceived(object sender, string scheme, string url, out string contentType)
    {
        if (_schemeHandler == null)
        {
            contentType = "text/plain";
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes("CustomSchemeHandler not initialized"));
        }

        return _schemeHandler.HandleRequest(url, out contentType);
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

        MessageRequest? request = null;
        try
        {
            // Parse incoming message (case-insensitive for incoming)
            request = JsonSerializer.Deserialize<MessageRequest>(message, JsonOptions);
            if (request is null)
            {
                throw new InvalidOperationException("Failed to deserialize message");
            }

            Console.WriteLine($"[IPC] Request: {request.Type} (ID: {request.Id}, Module: {request.Module}, ProfileId: {request.ProfileId})");

            // ServiceRouter handles everything:
            // 1. Routes to appropriate service provider (global vs profile-scoped)
            // 2. Checks for plugin handlers
            // 3. Routes to appropriate module facade
            if (_serviceRouter == null)
            {
                throw new InvalidOperationException("ServiceRouter not initialized");
            }

            var response = await _serviceRouter.HandleMessageAsync(request);

            // Send response back to frontend (using camelCase)
            var json = JsonSerializer.Serialize(response, JsonOptions);
            window.SendWebMessage(json);

            Console.WriteLine($"[IPC] Response: {(response.Success ? "Success" : "Error")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Error processing message: {ex.Message}");

            // Send error response with proper message ID (using camelCase)
            var requestId = request?.Id ?? "unknown";
            var errorResponse = MessageResponse.CreateError(requestId, ex.Message);
            var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
            window.SendWebMessage(json);
        }
    }

    /// <summary>
    /// Initialize 7z library for archive extraction
    /// Sets the path to the native 7z.dll based on platform (x64/x86)
    /// </summary>
    private static void Initialize7zLibrary()
    {
        try
        {
            var platformFolder = Environment.Is64BitProcess ? "x64" : "x86";
            var libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, platformFolder, "7z.dll");

            if (File.Exists(libraryPath))
            {
                SharpSevenZipBase.SetLibraryPath(libraryPath);
                Console.WriteLine($"[Init] 7z library initialized: {libraryPath}");
            }
            else
            {
                Console.WriteLine($"[Init] WARNING: 7z.dll not found at: {libraryPath}");
                Console.WriteLine($"[Init] Archive extraction may fail for some formats (7z, etc.)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Init] ERROR: Failed to initialize 7z library: {ex.Message}");
        }
    }
}
