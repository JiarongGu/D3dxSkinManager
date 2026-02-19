using D3dxSkinManager.Modules.Plugins.Models;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Helper service for emitting plugin events with null-safe handling.
/// Encapsulates event bus null checks and boilerplate.
/// </summary>
public interface IEventEmitterHelper
{
    /// <summary>
    /// Emits an event to the plugin event bus if available.
    /// Silently returns if event bus is not available.
    /// </summary>
    Task EmitAsync(PluginEventType eventType, string? eventName = null, object? data = null);
}

/// <summary>
/// Implementation of IEventEmitterHelper.
/// </summary>
public class EventEmitterHelper : IEventEmitterHelper
{
    private readonly IPluginEventBus? _eventBus;

    public EventEmitterHelper(IPluginEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public async Task EmitAsync(PluginEventType eventType, string? eventName = null, object? data = null)
    {
        if (_eventBus == null)
        {
            return;
        }

        await _eventBus.EmitAsync(new PluginEventArgs
        {
            EventType = eventType,
            EventName = eventName,
            Data = data
        });
    }
}
