using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Photino.NET;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager;

/// <summary>
/// Main entry point for D3dxSkinManager application
/// </summary>
class Program
{
    private static ServiceRouter? _serviceRouter;
    private static DevelopmentServerManager? _devServer;

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

        // Start development server if in development mode
        StartDevelopmentServerIfNeeded().Wait();

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
    /// Performs cleanup on shutdown
    /// </summary>
    private static void Shutdown()
    {
        Console.WriteLine("[Shutdown] Application shutting down...");

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

        Console.WriteLine("[Init] ServiceRouter initialized for stateless API request handling");
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

        return new PhotinoWindow()
            .SetTitle("D3dxSkinManager")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 800)
            .SetResizable(true)
            .Center()
            .RegisterWebMessageReceivedHandler(OnWebMessageReceived)
            .RegisterCustomSchemeHandler("app", OnAppRequestReceived)
            .Load(startUrl);
    }

    private static Stream OnAppRequestReceived(object sender, string scheme, string url, out string contentType)
    {
        contentType = string.Empty;
        Console.WriteLine($"[CustomScheme] Received request: {url}");
        return Stream.Null;
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
}
