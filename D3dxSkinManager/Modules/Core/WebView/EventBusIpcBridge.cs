using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Bridge between backend EventBus and frontend via IPC
/// Subscribes to backend events and forwards them to the frontend
/// IpcHandler automatically batches events every 50ms to reduce IPC overhead
/// </summary>
public class EventBusIpcBridge : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IpcHandler _ipcHandler;
    private readonly ILogHelper _logger;
    private readonly List<string> _registrationIds = new();
    private bool _disposed;

    public EventBusIpcBridge(
        IEventBus eventBus,
        IpcHandler ipcHandler,
        ILogHelper logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _ipcHandler = ipcHandler ?? throw new ArgumentNullException(nameof(ipcHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialize the bridge by subscribing to ALL event types using wildcard
    /// This ensures all events from any module are forwarded to frontend
    /// </summary>
    public void Init()
    {
        _logger.Info("Initializing EventBus IPC Bridge", "EventBridge");

        // Subscribe to ALL events (all modules, all types, all profiles)
        var registrationId = _eventBus.SubscribeToAll(async (message) =>
        {
            await ForwardEventToFrontend(message);
        });

        _registrationIds.Add(registrationId);

        _logger.Info("EventBus IPC Bridge initialized - IpcHandler will batch events every 50ms", "EventBridge");
    }

    /// <summary>
    /// Forward a backend event to the frontend via IPC
    /// IpcHandler automatically queues and batches events
    /// </summary>
    private Task ForwardEventToFrontend(EventMessage message)
    {
        try
        {
            var eventId = $"{message.Module}.{message.Type}";
            _logger.Verbose($"Forwarding event to frontend: {eventId}", "EventBridge");

            // Send notification - IpcHandler will automatically queue and batch
            _ipcHandler.SendNotification(
                module: message.Module,
                type: message.Type,
                payload: message.Payload
            );

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error forwarding event to frontend: {ex.Message}", "EventBridge", ex);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Clean up subscriptions
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _logger.Info("Disposing EventBus IPC Bridge", "EventBridge");

        foreach (var registrationId in _registrationIds)
        {
            _eventBus.Unsubscribe(registrationId);
        }

        _registrationIds.Clear();
        _disposed = true;
    }
}


