using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Delegate for message processing middleware
/// </summary>
public delegate Task<IpcResponse?> MessageMiddleware(IpcRequest message, Func<Task<IpcResponse?>> next);

/// <summary>
/// Dispatcher that manages a middleware pipeline for processing IPC messages
/// </summary>
public class MessageDispatcher
{
    private readonly IpcCommunicationHandler _ipcHandler;
    private readonly ILogHelper _logger;
    private readonly List<MessageMiddleware> _middlewares;
    private Lazy<Func<IpcRequest, Task<IpcResponse?>>> _pipeline;

    public MessageDispatcher(IpcCommunicationHandler ipcHandler, ILogHelper logger)
    {
        _ipcHandler = ipcHandler ?? throw new ArgumentNullException(nameof(ipcHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _middlewares = new List<MessageMiddleware>();
        _pipeline = new Lazy<Func<IpcRequest, Task<IpcResponse?>>>(BuildPipeline);
    }

    /// <summary>
    /// Initialize the dispatcher and subscribe to IPC messages
    /// </summary>
    public void Initialize()
    {
        _logger.Info("Initializing message dispatcher...", "MessageDispatcher");

        // Subscribe to IPC messages
        _ipcHandler.MessageReceived += OnMessageReceived;

        _logger.Info($"Message dispatcher initialized with {_middlewares.Count} middleware(s)", "MessageDispatcher");
    }

    /// <summary>
    /// Register a middleware in the pipeline
    /// </summary>
    public MessageDispatcher Use(MessageMiddleware middleware)
    {
        if (middleware == null)
            throw new ArgumentNullException(nameof(middleware));

        _middlewares.Add(middleware);
        // Reset lazy to rebuild pipeline on next access
        _pipeline = new Lazy<Func<IpcRequest, Task<IpcResponse?>>>(BuildPipeline);
        _logger.Debug($"Middleware registered (total: {_middlewares.Count})", "MessageDispatcher");

        return this; // Fluent API
    }

    /// <summary>
    /// Register a conditional middleware that only processes specific modules
    /// </summary>
    public MessageDispatcher UseModule(string moduleName, Func<IpcRequest, Task<IpcResponse>> handler)
    {
        return Use(async (message, next) =>
        {
            if (string.Equals(message.Module, moduleName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"Module '{moduleName}' handling message type: {message.Type}", "MessageDispatcher");
                var response = await handler(message);
                if (response != null)
                    return response;
            }
            return await next();
        });
    }

    /// <summary>
    /// Register a conditional middleware for a specific module and type
    /// </summary>
    public MessageDispatcher UseRoute(string moduleName, string messageType, Func<IpcRequest, Task<IpcResponse>> handler)
    {
        return Use(async (message, next) =>
        {
            if (string.Equals(message.Module, moduleName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(message.Type, messageType, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"Route '{moduleName}/{messageType}' matched", "MessageDispatcher");
                return await handler(message);
            }
            return await next();
        });
    }

    /// <summary>
    /// Register middleware that logs all messages
    /// </summary>
    public MessageDispatcher UseLogging()
    {
        return Use(async (message, next) =>
        {
            _logger.Debug($"Processing: {message.Module}/{message.Type}", "MessageDispatcher");
            var response = await next();
            if (response?.Success == true)
                _logger.Debug($"Success: {message.Module}/{message.Type}", "MessageDispatcher");
            else if (response?.Success == false)
                _logger.Warn($"Failed: {message.Module}/{message.Type} - {response.Error}", "MessageDispatcher");
            return response;
        });
    }

    /// <summary>
    /// Register error handling middleware
    /// </summary>
    public MessageDispatcher UseErrorHandler()
    {
        return Use(async (message, next) =>
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error handling {message.Module}/{message.Type}: {ex.Message}", "MessageDispatcher", ex);
                return IpcResponse.CreateError(message.Id, $"Internal error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Handle incoming IPC messages through the middleware pipeline
    /// </summary>
    private async void OnMessageReceived(object? sender, IpcMessageReceivedEventArgs e)
    {
        var message = e.Message;
        var sendResponse = e.SendResponse;

        try
        {
            // Execute the pipeline (Lazy<T> ensures it's built only once and is thread-safe)
            var response = await _pipeline.Value(message);

            // Send response
            if (response != null)
            {
                sendResponse(response);
            }
            else
            {
                // No middleware handled the message
                var errorResponse = IpcResponse.CreateError(message.Id,
                    $"No handler registered for {message.Module}/{message.Type}");
                sendResponse(errorResponse);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Unhandled error: {ex.Message}", "MessageDispatcher", ex);
            var errorResponse = IpcResponse.CreateError(message.Id, $"Dispatcher error: {ex.Message}");
            sendResponse(errorResponse);
        }
    }

    /// <summary>
    /// Build the middleware pipeline
    /// </summary>
    private Func<IpcRequest, Task<IpcResponse?>> BuildPipeline()
    {
        // Start with the final handler that returns null
        Func<IpcRequest, Task<IpcResponse?>> pipeline = async (message) =>
        {
            await Task.CompletedTask;
            return null;
        };

        // Build the pipeline in reverse order
        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = pipeline;
            pipeline = async (message) => await middleware(message, () => next(message));
        }

        return pipeline;
    }
}

/// <summary>
/// Extension methods for common middleware patterns
/// </summary>
public static class MessageDispatcherExtensions
{
    /// <summary>
    /// Register a simple handler for a specific route
    /// </summary>
    public static MessageDispatcher MapRoute(this MessageDispatcher dispatcher,
        string moduleName, string messageType, Func<IpcRequest, object?> handler)
    {
        return dispatcher.UseRoute(moduleName, messageType, message =>
        {
            var result = handler(message);
            return Task.FromResult(IpcResponse.CreateSuccess(message.Id, result));
        });
    }

    /// <summary>
    /// Register multiple routes for a module using a route table
    /// </summary>
    public static MessageDispatcher MapModule(this MessageDispatcher dispatcher,
        string moduleName, Action<ModuleRouteBuilder> configure)
    {
        var builder = new ModuleRouteBuilder(dispatcher, moduleName);
        configure(builder);
        return dispatcher;
    }
}

/// <summary>
/// Builder for configuring routes within a module
/// </summary>
public class ModuleRouteBuilder
{
    private readonly MessageDispatcher _dispatcher;
    private readonly string _moduleName;

    public ModuleRouteBuilder(MessageDispatcher dispatcher, string moduleName)
    {
        _dispatcher = dispatcher;
        _moduleName = moduleName;
    }

    /// <summary>
    /// Map a route within this module
    /// </summary>
    public ModuleRouteBuilder Route(string messageType, Func<IpcRequest, object?> handler)
    {
        _dispatcher.MapRoute(_moduleName, messageType, handler);
        return this;
    }

    /// <summary>
    /// Map an async route within this module
    /// </summary>
    public ModuleRouteBuilder RouteAsync(string messageType, Func<IpcRequest, Task<object?>> handler)
    {
        _dispatcher.UseRoute(_moduleName, messageType, async message =>
        {
            var result = await handler(message);
            return IpcResponse.CreateSuccess(message.Id, result);
        });
        return this;
    }
}
