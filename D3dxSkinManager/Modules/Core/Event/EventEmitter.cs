using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Helper for emitting events with null-safe EventBus handling.
/// </summary>
public interface IEventEmitter
{
    Task EmitAsync(string module, string type, object? payload = null);
}

public class EventEmitter : IEventEmitter
{
    private readonly IEventBus? _eventBus;
    private readonly ILogHelper? _logger;

    public EventEmitter(IEventBus? eventBus = null, ILogHelper? logger = null)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task EmitAsync(string module, string type, object? payload = null)
    {
        if (_eventBus == null)
        {
            _logger?.Warn($"Cannot emit {module}.{type} - EventBus is null", "EventEmitter");
            return;
        }

        _logger?.Verbose($"Emitting event: {module}.{type}", "EventEmitter");
        await _eventBus.EmitAsync(module, type, payload).ConfigureAwait(false);
    }
}
