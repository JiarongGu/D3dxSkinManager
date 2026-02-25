using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Core.Event;


public interface IEventBus
{
    string RegisterHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler);

    void UnregisterHandler(string registrationId);

    Task EmitAsync(EventMessage message);

    Task EmitAsync(string module, string type, object? payload = null);
}

/// <summary>
/// Event bus for plugin event handling.
/// Manages event subscriptions and emission.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogHelper _logger;
    private readonly ConcurrentDictionary<string, Func<EventMessage, Task>> _handlers = new();
    private readonly ConcurrentDictionary<string, (string modulePattern, string typePattern)> _handlerPatterns = new();

    // Cache: HandlerId -> Dictionary of EventIds this handler has been evaluated for
    // The inner dictionary maps: EventId -> bool (true if handler matches this event, false if not)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _handlerEventCache = new();

    public EventBus(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register an event handler.
    /// </summary>
    /// <param name="modulePattern">Module pattern to listen for.
    /// Use "*" for all modules or specific module name (e.g., "MOD", "TASK_QUEUE")
    /// </param>
    /// <param name="typePattern">Type pattern to listen for.
    /// Use "*" for all types or specific event type (e.g., "LOADED", "ADDED")
    /// </param>
    /// <param name="handler">Event handler callback</param>
    /// <returns>Registration ID for unregistering later</returns>
    public string RegisterHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler)
    {
        var registrationId = $"{modulePattern}.{typePattern}_{Guid.NewGuid()}";

        _handlers[registrationId] = handler;
        _handlerPatterns[registrationId] = (modulePattern, typePattern);

        // Create cache entry for this handler (initially empty)
        _handlerEventCache[registrationId] = new ConcurrentDictionary<string, bool>();

        return registrationId;
    }

    /// <summary>
    /// Unregister an event handler.
    /// </summary>
    /// <param name="registrationId">Registration ID from RegisterHandler</param>
    public void UnregisterHandler(string registrationId)
    {
        _handlers.TryRemove(registrationId, out _);
        _handlerPatterns.TryRemove(registrationId, out _);

        // Remove the entire cache for this handler - single operation!
        _handlerEventCache.TryRemove(registrationId, out _);
    }

    /// <summary>
    /// Emit an event to all registered handlers.
    /// </summary>
    /// <param name="message">Event message</param>
    public virtual async Task EmitAsync(EventMessage message)
    {
        // Build the event identifier: MODULE.TYPE
        var eventId = $"{message.Module}.{message.Type}";

        var handlersToInvoke = new List<Func<EventMessage, Task>>();

        // Iterate through all handlers and check their individual caches
        foreach (var kvp in _handlerPatterns)
        {
            var handlerId = kvp.Key;
            var (modulePattern, typePattern) = kvp.Value;

            // Get this handler's event cache
            if (!_handlerEventCache.TryGetValue(handlerId, out var eventCache))
            {
                // Handler was unregistered, skip
                continue;
            }

            // Check if we've already evaluated this event for this handler
            if (!eventCache.TryGetValue(eventId, out var matches))
            {
                // Not in cache - evaluate if this handler matches this event
                var moduleMatch = modulePattern == "*" || modulePattern == message.Module;
                var typeMatch = typePattern == "*" || typePattern == message.Type;
                matches = moduleMatch && typeMatch;

                // Cache the result in this handler's cache
                eventCache[eventId] = matches;
            }

            // If handler matches and still exists, add it to invoke list
            if (matches && _handlers.TryGetValue(handlerId, out var handler))
            {
                handlersToInvoke.Add(handler);
            }
        }

        _logger.Verbose($"[EventBus] Emitting {eventId} to {handlersToInvoke.Count} handler(s)", "EventBus");

        // Invoke handlers
        var tasks = handlersToInvoke.Select(handler => SafeInvokeHandler(handler, message));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Emit an event to all registered handlers (convenience overload).
    /// </summary>
    /// <param name="module">Module name (e.g., "CORE", "MOD", "TASK_QUEUE")</param>
    /// <param name="type">Event type (e.g., "APPLICATION_STARTED", "MOD_LOADED")</param>
    /// <param name="payload">Optional event payload</param>
    public async Task EmitAsync(string module, string type, object? payload = null)
    {
        var message = new EventMessage
        {
            Module = module,
            Type = type,
            Payload = payload
        };

        await EmitAsync(message);
    }

    /// <summary>
    /// Safely invoke a handler, catching and logging any exceptions.
    /// </summary>
    private async Task SafeInvokeHandler(Func<EventMessage, Task> handler, EventMessage message)
    {
        try
        {
            await handler(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in event handler: {ex.Message}", "PluginEventBus", ex);
        }
    }

    /// <summary>
    /// Get count of registered handlers.
    /// </summary>
    public int GetHandlerCount()
    {
        return _handlers.Count;
    }
}
