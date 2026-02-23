using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Composition;

/// <summary>
/// Bridge between backend EventBus and frontend via IPC
/// Subscribes to backend events and forwards them to the frontend
/// </summary>
public class EventBusIpcBridge
{
    private readonly IEventBus _eventBus;
    private readonly IpcCommunicationHandler _ipcHandler;
    private readonly ILogHelper _logger;
    private readonly List<string> _registrationIds = new();

    public EventBusIpcBridge(
        IEventBus eventBus,
        IpcCommunicationHandler ipcHandler,
        ILogHelper logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _ipcHandler = ipcHandler ?? throw new ArgumentNullException(nameof(ipcHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initialize the bridge by subscribing to all event types
    /// </summary>
    public void Initialize()
    {
        _logger.Info("Initializing EventBus IPC Bridge", "EventBridge");

        // Subscribe to all event types
        foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
        {
            var registrationId = _eventBus.RegisterHandler(eventType, async (message) =>
            {
                await ForwardEventToFrontend(message);
            });

            _registrationIds.Add(registrationId);
        }

        _logger.Info($"EventBus IPC Bridge initialized - subscribed to {_registrationIds.Count} event types", "EventBridge");
    }

    /// <summary>
    /// Forward a backend event to the frontend via IPC
    /// Sends as notification with type = EventType name
    /// </summary>
    private async Task ForwardEventToFrontend(EventMessage message)
    {
        try
        {
            _logger.Debug($"Forwarding event to frontend: {message.EventType}", "EventBridge");

            // Send notification with EventType as the type
            _ipcHandler.SendNotification(
                type: message.EventType.ToString(),
                data: new
                {
                    eventName = message.EventName,
                    data = message.Data
                }
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
    public void Shutdown()
    {
        _logger.Info("Shutting down EventBus IPC Bridge", "EventBridge");

        foreach (var registrationId in _registrationIds)
        {
            _eventBus.UnregisterHandler(registrationId);
        }

        _registrationIds.Clear();
    }
}
