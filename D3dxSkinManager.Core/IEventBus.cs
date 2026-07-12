namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Pub/sub event bus for messaging between modules, services, and plugins. Part of the plugin SDK —
/// plugins subscribe/emit via <c>IPluginContext.EventBus</c>. The implementation (EventBus) lives in the host.
/// </summary>
public interface IEventBus
{
    // Module + Type + ProfileId - Specific event in specific profile
    string Subscribe(string module, string type, string profileId, Func<EventMessage, Task> handler);

    // Module + Type - Specific event in all profiles
    string Subscribe(string module, string type, Func<EventMessage, Task> handler);

    // Module + ProfileId - All events from module in specific profile
    string SubscribeToModule(string module, string profileId, Func<EventMessage, Task> handler);

    // Module only - All events from module (all types, all profiles)
    string SubscribeToModule(string module, Func<EventMessage, Task> handler);

    // All - Everything (wildcard)
    string SubscribeToAll(Func<EventMessage, Task> handler);

    void Unsubscribe(string subscriptionId);

    Task EmitAsync(EventMessage message);
    Task EmitAsync(string module, string type, object? payload = null, string? profileId = null);
}
