using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Interface for IPC (Inter-Process Communication) handler between backend and frontend.
/// Abstracts WebView2-specific implementation for testability and dependency injection.
/// </summary>
public interface IIpcHandler
{
    /// <summary>
    /// Event fired when a message is received from the frontend
    /// </summary>
    event EventHandler<IpcMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Initialize IPC message handlers
    /// </summary>
    void Init();

    /// <summary>
    /// Subscribe to notifications of a specific module and type
    /// </summary>
    void Subscribe(string module, string type);

    /// <summary>
    /// Unsubscribe from notifications of a specific module and type
    /// </summary>
    void Unsubscribe(string module, string type);

    /// <summary>
    /// Clear all subscriptions
    /// </summary>
    void ClearSubscriptions();

    /// <summary>
    /// Send a push notification to the frontend (queued for batching)
    /// Events are automatically batched and sent every 50ms to reduce IPC overhead
    /// </summary>
    /// <param name="module">Module name (e.g., "MOD", "PROFILE")</param>
    /// <param name="type">Event type (e.g., "LOADED", "DELETED")</param>
    /// <param name="payload">Optional event payload</param>
    void SendNotification(string module, string type, object? payload = null);
}
