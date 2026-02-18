namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Event bus for plugin event handling.
/// Manages event subscriptions and emission.
/// </summary>
public class PluginEventBus
{
    private readonly Dictionary<string, Func<PluginEventArgs, Task>> _handlers = new();
    private readonly object _lock = new();
    private int _registrationCounter = 0;

    /// <summary>
    /// Register an event handler.
    /// </summary>
    /// <param name="eventType">Event type to listen for</param>
    /// <param name="handler">Event handler callback</param>
    /// <returns>Registration ID for unregistering later</returns>
    public string RegisterHandler(PluginEventType eventType, Func<PluginEventArgs, Task> handler)
    {
        lock (_lock)
        {
            var registrationId = $"{eventType}_{++_registrationCounter}_{Guid.NewGuid()}";
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
    /// <param name="args">Event arguments</param>
    public virtual async Task EmitAsync(PluginEventArgs args)
    {
        List<Func<PluginEventArgs, Task>> handlersToInvoke;

        lock (_lock)
        {
            // Get handlers that match this event type
            handlersToInvoke = _handlers
                .Where(kvp => kvp.Key.StartsWith($"{args.EventType}_"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        // Invoke handlers outside the lock to prevent deadlocks
        var tasks = handlersToInvoke.Select(handler => SafeInvokeHandler(handler, args));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Safely invoke a handler, catching and logging any exceptions.
    /// </summary>
    private async Task SafeInvokeHandler(Func<PluginEventArgs, Task> handler, PluginEventArgs args)
    {
        try
        {
            await handler(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PluginEventBus] Error in event handler: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
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
