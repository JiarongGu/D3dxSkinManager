using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Event;


public interface IEventBus
{
    string RegisterHandler(string eventType, Func<EventMessage, Task> handler);

    void UnregisterHandler(string registrationId);

    Task EmitAsync(EventMessage message);
}

/// <summary>
/// Event bus for plugin event handling.
/// Manages event subscriptions and emission.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogHelper _logger;
    private readonly Dictionary<string, Func<EventMessage, Task>> _handlers = new();
    private readonly object _lock = new();

    public EventBus(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register an event handler.
    /// </summary>
    /// <param name="eventType">Event type constant to listen for (SCREAMING_SNAKE_CASE)</param>
    /// <param name="handler">Event handler callback</param>
    /// <returns>Registration ID for unregistering later</returns>
    public string RegisterHandler(string eventType, Func<EventMessage, Task> handler)
    {
        lock (_lock)
        {
            var registrationId = $"{eventType}_{Guid.NewGuid()}";
            _handlers[registrationId] = handler;
            return registrationId;
        }
    }

    /// <summary>
    /// Unregister an event handler.
    /// </summary>
    /// <param name="registrationId">Registration ID from RegisterHandler</param>
    public void UnregisterHandler(string registrationId)
    {
        lock (_lock)
        {
            _handlers.Remove(registrationId);
        }
    }

    /// <summary>
    /// Emit an event to all registered handlers.
    /// </summary>
    /// <param name="message">Event arguments</param>
    public virtual async Task EmitAsync(EventMessage message)
    {
        List<Func<EventMessage, Task>> handlersToInvoke;

        lock (_lock)
        {
            // Get handlers that match this event type
            handlersToInvoke = _handlers
                .Where(kvp => kvp.Key.StartsWith($"{message.EventType}_"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        // Invoke handlers outside the lock to prevent deadlocks
        var tasks = handlersToInvoke.Select(handler => SafeInvokeHandler(handler, message));
        await Task.WhenAll(tasks).ConfigureAwait(false);
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
        lock (_lock)
        {
            return _handlers.Count;
        }
    }
}
