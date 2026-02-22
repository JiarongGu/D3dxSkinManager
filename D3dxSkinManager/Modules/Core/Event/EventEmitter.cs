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
    Task EmitAsync(EventType eventType, string? eventName = null, object? data = null);
}

/// <summary>
/// Implementation of IEventEmitterHelper.
/// </summary>
public class EventEmitter : IEventEmitter
{
    private readonly IEventBus? _eventBus;

    public EventEmitter(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public async Task EmitAsync(EventType eventType, string? eventName = null, object? data = null)
    {
        if (_eventBus == null)
        {
            return;
        }

        await _eventBus.EmitAsync(new EventMessage
        {
            EventType = eventType,
            EventName = eventName,
            Data = data
        }).ConfigureAwait(false);
    }
}
