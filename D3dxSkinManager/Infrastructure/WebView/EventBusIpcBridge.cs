using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Bridge between backend EventBus and frontend via IPC
/// Subscribes to backend events and forwards them to the frontend
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
        _logger.Info("Initializing EventBus IPC Bridge - forwarding all events", "EventBridge");

        // Subscribe to ALL events (all modules, all types, all profiles)
        var registrationId = _eventBus.RegisterHandlerForAll(async (message) =>
        {
            await ForwardEventToFrontend(message);
        });

        _registrationIds.Add(registrationId);

        _logger.Info("EventBus IPC Bridge initialized - forwarding all events to frontend", "EventBridge");
    }

    /// <summary>
    /// Forward a backend event to the frontend via IPC
    /// Forwards Module and Type separately, following IpcRequest pattern
    /// </summary>
    private async Task ForwardEventToFrontend(EventMessage message)
    {
        try
        {
            var eventId = $"{message.Module}.{message.Type}";
            _logger.Verbose($"Forwarding event to frontend: {eventId}", "EventBridge");

            // Send notification with Module, Type, and Payload at top level
            // Frontend will receive: { category: "notification", module, type, payload }
            _ipcHandler.SendNotification(
                module: message.Module,
                type: message.Type,
                payload: message.Payload
            );

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error forwarding event to frontend: {ex.Message}", "EventBridge", ex);
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
            _eventBus.UnregisterHandler(registrationId);
        }

        _registrationIds.Clear();
        _disposed = true;
    }
}
