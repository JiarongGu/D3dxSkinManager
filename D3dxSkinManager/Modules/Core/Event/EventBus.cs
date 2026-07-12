using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Core.Event;


// NOTE: `interface IEventBus` moved to the D3dxSkinManager.Plugin.Sdk assembly (same namespace) so
// plugins can consume it via IPluginContext.EventBus. This class implements it.

/// <summary>
/// Event bus for pub/sub messaging between modules, services, and plugins.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogHelper _logger;
    private readonly ConcurrentDictionary<string, Func<EventMessage, Task>> _handlers = new();
    private readonly ConcurrentDictionary<string, (string modulePattern, string typePattern, string? profileIdPattern)> _handlerPatterns = new();

    // Cache: HandlerId -> (EventId -> matches?)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _handlerEventCache = new();

    public EventBus(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Subscribe(string module, string type, string profileId, Func<EventMessage, Task> handler)
    {
        return SubscribeInternal(module, type, profileId, handler);
    }

    public string Subscribe(string module, string type, Func<EventMessage, Task> handler)
    {
        return SubscribeInternal(module, type, null, handler);
    }

    public string SubscribeToModule(string module, string profileId, Func<EventMessage, Task> handler)
    {
        return SubscribeInternal(module, "*", profileId, handler);
    }

    public string SubscribeToModule(string module, Func<EventMessage, Task> handler)
    {
        return SubscribeInternal(module, "*", null, handler);
    }

    public string SubscribeToAll(Func<EventMessage, Task> handler)
    {
        return SubscribeInternal("*", "*", null, handler);
    }

    private string SubscribeInternal(string modulePattern, string typePattern, string? profileIdPattern, Func<EventMessage, Task> handler)
    {
        var subscriptionId = $"{modulePattern}.{typePattern}.{profileIdPattern ?? "*"}_{Guid.NewGuid()}";

        _handlers[subscriptionId] = handler;
        _handlerPatterns[subscriptionId] = (modulePattern, typePattern, profileIdPattern);
        _handlerEventCache[subscriptionId] = new ConcurrentDictionary<string, bool>();

        return subscriptionId;
    }

    public void Unsubscribe(string subscriptionId)
    {
        _handlers.TryRemove(subscriptionId, out _);
        _handlerPatterns.TryRemove(subscriptionId, out _);
        _handlerEventCache.TryRemove(subscriptionId, out _);
    }

    public virtual async Task EmitAsync(EventMessage message)
    {
        var eventId = string.IsNullOrEmpty(message.ProfileId)
            ? $"{message.Module}.{message.Type}"
            : $"{message.Module}.{message.Type}.{message.ProfileId}";

        var handlersToInvoke = new List<Func<EventMessage, Task>>();

        foreach (var kvp in _handlerPatterns)
        {
            var handlerId = kvp.Key;
            var (modulePattern, typePattern, profileIdPattern) = kvp.Value;

            if (!_handlerEventCache.TryGetValue(handlerId, out var eventCache))
                continue;

            if (!eventCache.TryGetValue(eventId, out var matches))
            {
                var moduleMatch = modulePattern == "*" || modulePattern == message.Module;
                var typeMatch = typePattern == "*" || typePattern == message.Type;
                var profileMatch = string.IsNullOrEmpty(profileIdPattern) || profileIdPattern == "*" ||
                                   string.IsNullOrEmpty(message.ProfileId) ||
                                   profileIdPattern == message.ProfileId;

                matches = moduleMatch && typeMatch && profileMatch;
                eventCache[eventId] = matches;
            }

            if (matches && _handlers.TryGetValue(handlerId, out var handler))
            {
                handlersToInvoke.Add(handler);
            }
        }

        _logger.Verbose($"[EventBus] Emitting {eventId} to {handlersToInvoke.Count} handler(s)", "EventBus");

        var tasks = handlersToInvoke.Select(handler => SafeInvokeHandler(handler, message));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task EmitAsync(string module, string type, object? payload = null, string? profileId = null)
    {
        var message = new EventMessage
        {
            Module = module,
            Type = type,
            Payload = payload,
            ProfileId = profileId
        };

        await EmitAsync(message);
    }

    private async Task SafeInvokeHandler(Func<EventMessage, Task> handler, EventMessage message)
    {
        try
        {
            await handler(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in event handler: {ex.Message}", "EventBus", ex);
        }
    }

    public int GetHandlerCount()
    {
        return _handlers.Count;
    }
}
