using D3dxSkinManager.Infrastructure.WebView;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Helpers;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Routes IPC messages to module facades via middleware pipeline.
/// Singleton shared across all WebView sessions.
/// </summary>
public interface IMessageDispatcher
{
    Task<IpcResponse> SendAsync(string module, string type, string? profileId = null, object? payload = null);
    Task<T?> SendAsync<T>(string module, string type, string? profileId = null, object? payload = null);
}

public delegate Task<IpcResponse?> MessageMiddleware(IpcRequest message, Func<Task<IpcResponse?>> next);

/// <summary>
/// Singleton dispatcher with middleware pipeline for routing IPC messages to module facades.
/// </summary>
public class MessageDispatcher : IMessageDispatcher
{
    private readonly ILogHelper _logger;
    private readonly List<MessageMiddleware> _middlewares;
    private Lazy<Func<IpcRequest, Task<IpcResponse?>>> _pipeline;

    public MessageDispatcher(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _middlewares = new List<MessageMiddleware>();
        _pipeline = new Lazy<Func<IpcRequest, Task<IpcResponse?>>>(BuildPipeline);
    }

    /// <summary>
    /// Process a message through the middleware pipeline programmatically.
    /// Used by plugins and services to send messages to modules.
    /// </summary>
    public async Task<IpcResponse?> ProcessMessageAsync(IpcRequest message)
    {
        try
        {
            // Execute the pipeline (Lazy<T> ensures it's built only once and is thread-safe)
            return await _pipeline.Value(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing message: {ex.Message}", "MessageDispatcher", ex);
            return IpcResponse.CreateError(message.Id, $"Dispatcher error: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a message to a module facade and get the response.
    /// Used by plugins and services to communicate with modules programmatically.
    /// </summary>
    public async Task<IpcResponse> SendAsync(string module, string type, string? profileId = null, object? payload = null)
    {
        var request = new IpcRequest
        {
            Id = Guid.NewGuid().ToString(),
            Module = module,
            Type = type,
            ProfileId = profileId,
            Payload = payload != null ? JsonSerializer.SerializeToElement(payload) : null,
            Timestamp = DateTime.UtcNow
        };

        _logger.Verbose($"Sending programmatic message: {module}.{type} (ProfileId: {profileId ?? "none"})", "MessageDispatcher");

        var response = await ProcessMessageAsync(request);

        // ProcessMessageAsync returns null if no handler matched
        if (response == null)
        {
            return IpcResponse.CreateError(request.Id, $"No handler registered for {module}/{type}");
        }

        return response;
    }

    /// <summary>
    /// Send a message to a module facade and get the typed response data.
    /// Throws if the response indicates an error.
    /// </summary>
    public async Task<T?> SendAsync<T>(string module, string type, string? profileId = null, object? payload = null)
    {
        var response = await SendAsync(module, type, profileId, payload);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Message dispatch failed: {response.Error}");
        }

        if (response.Data == null)
        {
            return default;
        }

        // Deserialize the response data
        try
        {
            if (response.Data is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }

            // Try direct conversion if it's already the right type
            if (response.Data is T typedData)
            {
                return typedData;
            }

            // Serialize then deserialize for type conversion
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize response data to type {typeof(T).Name}: {ex.Message}", ex);
        }
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
