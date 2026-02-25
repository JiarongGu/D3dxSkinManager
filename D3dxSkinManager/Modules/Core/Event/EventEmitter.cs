using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Helper service for emitting plugin events with null-safe handling.
/// Encapsulates event bus null checks and boilerplate.
/// </summary>
public interface IEventEmitter
{
    /// <summary>
    /// Emits an event to the plugin event bus if available.
    /// Silently returns if event bus is not available.
    /// </summary>
    /// <param name="module">Module name (e.g., "CORE", "MOD", "TASK_QUEUE")</param>
    /// <param name="type">Event type (SCREAMING_SNAKE_CASE)</param>
    /// <param name="payload">Event payload data</param>
    Task EmitAsync(string module, string type, object? payload = null);
}

/// <summary>
/// Implementation of IEventEmitterHelper.
/// </summary>
public class EventEmitter : IEventEmitter
{
    private readonly IEventBus? _eventBus;
    private readonly ILogHelper? _logger;

    public EventEmitter(IEventBus? eventBus = null, ILogHelper? logger = null)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EmitAsync(string module, string type, object? payload = null)
    {
        if (_eventBus == null)
        {
            _logger?.Warn($"[EventEmitter] Cannot emit {module}.{type} - EventBus is null", "EventEmitter");
            return;
        }

        _logger?.Verbose($"[EventEmitter] Emitting event: {module}.{type}, HasPayload: {payload != null}", "EventEmitter");

        await _eventBus.EmitAsync(module, type, payload).ConfigureAwait(false);
    }
}
