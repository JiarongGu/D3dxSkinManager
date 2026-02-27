using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Core.Event;


public interface IEventBus
{
    // All possible combinations of Module + Type + ProfileId filters

    // Module + Type + ProfileId - Specific event in specific profile
    string RegisterHandler(string module, string type, string profileId, Func<EventMessage, Task> handler);

    // Module + Type - Specific event in all profiles
    string RegisterHandler(string module, string type, Func<EventMessage, Task> handler);

    // Module + ProfileId - All events from module in specific profile
    string RegisterHandlerForModule(string module, string profileId, Func<EventMessage, Task> handler);

    // Module only - All events from module (all types, all profiles)
    string RegisterHandlerForModule(string module, Func<EventMessage, Task> handler);

    // All - Everything (wildcard)
    string RegisterHandlerForAll(Func<EventMessage, Task> handler);

    void UnregisterHandler(string registrationId);

    Task EmitAsync(EventMessage message);
    Task EmitAsync(string module, string type, object? payload = null, string? profileId = null);
}

/// <summary>
/// Event bus for plugin event handling.
/// Manages event subscriptions and emission.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogHelper _logger;
    private readonly ConcurrentDictionary<string, Func<EventMessage, Task>> _handlers = new();
    private readonly ConcurrentDictionary<string, (string modulePattern, string typePattern, string? profileIdPattern)> _handlerPatterns = new();

    // Cache: HandlerId -> Dictionary of EventIds this handler has been evaluated for
    // The inner dictionary maps: EventId -> bool (true if handler matches this event, false if not)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _handlerEventCache = new();

    public EventBus(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register handler for specific module + type + profileId
    /// </summary>
    public string RegisterHandler(string module, string type, string profileId, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, type, profileId, handler);
    }

    /// <summary>
    /// Register handler for specific module + type (all profiles)
    /// </summary>
    public string RegisterHandler(string module, string type, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, type, null, handler);
    }

    /// <summary>
    /// Register handler for module + profileId (all types from module in specific profile)
    /// </summary>
    public string RegisterHandlerForModule(string module, string profileId, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, "*", profileId, handler);
    }

    /// <summary>
    /// Register handler for module only (all types, all profiles)
    /// </summary>
    public string RegisterHandlerForModule(string module, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, "*", null, handler);
    }

    /// <summary>
    /// Register handler for all events (wildcard)
    /// </summary>
    public string RegisterHandlerForAll(Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal("*", "*", null, handler);
    }

    /// <summary>
    /// Internal method that performs the actual registration
    /// </summary>
    private string RegisterHandlerInternal(string modulePattern, string typePattern, string? profileIdPattern, Func<EventMessage, Task> handler)
    {
        var registrationId = $"{modulePattern}.{typePattern}.{profileIdPattern ?? "*"}_{Guid.NewGuid()}";

        _handlers[registrationId] = handler;
        _handlerPatterns[registrationId] = (modulePattern, typePattern, profileIdPattern);

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
        // Build the event identifier: MODULE.TYPE.PROFILEID (or MODULE.TYPE for global events)
        var eventId = string.IsNullOrEmpty(message.ProfileId)
            ? $"{message.Module}.{message.Type}"
            : $"{message.Module}.{message.Type}.{message.ProfileId}";

        var handlersToInvoke = new List<Func<EventMessage, Task>>();

        // Iterate through all handlers and check their individual caches
        foreach (var kvp in _handlerPatterns)
        {
            var handlerId = kvp.Key;
            var (modulePattern, typePattern, profileIdPattern) = kvp.Value;

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

                // ProfileId matching logic:
                // - null or "*" pattern matches all events (global handler)
                // - specific profileId pattern matches only events with that profileId
                // - global events (null/empty profileId) match all handlers
                var profileMatch = string.IsNullOrEmpty(profileIdPattern) || profileIdPattern == "*" ||
                                   string.IsNullOrEmpty(message.ProfileId) ||
                                   profileIdPattern == message.ProfileId;

                matches = moduleMatch && typeMatch && profileMatch;

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
    /// <param name="profileId">Optional profileId for profile-scoped events</param>
    public async Task EmitAsync(string module, string type, object? payload = null, string? profileId = null)
    {
        var message = new EventMessage
        {
            Module = module,
            Type = type,
            Payload = payload,
            ProfileId = profileId
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
