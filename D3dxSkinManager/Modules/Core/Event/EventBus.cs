using D3dxSkinManager.Modules.Core.Helpers;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Core.Event;


public interface IEventBus
{
    // All possible combinations of Module + Type + ProfileId filters

    // Module + Type + ProfileId - Specific event in specific profile
    string RegisterHandler(string module, string type, string profileId, Func<EventMessage, Task> handler);

    // Module + Type - Specific event in all profiles
    string RegisterHandler(string module, string type, Func<EventMessage, Task> handler);

    // Module + ProfileId - All events from module in specific profile
    string RegisterHandlerForModule(string module, string profileId, Func<EventMessage, Task> handler);

    // Module only - All events from module (all types, all profiles)
    string RegisterHandlerForModule(string module, Func<EventMessage, Task> handler);

    // All - Everything (wildcard)
    string RegisterHandlerForAll(Func<EventMessage, Task> handler);

    void UnregisterHandler(string registrationId);

    Task EmitAsync(EventMessage message);
    Task EmitAsync(string module, string type, object? payload = null, string? profileId = null);
}

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

    public string RegisterHandler(string module, string type, string profileId, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, type, profileId, handler);
    }

    public string RegisterHandler(string module, string type, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, type, null, handler);
    }

    public string RegisterHandlerForModule(string module, string profileId, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, "*", profileId, handler);
    }

    public string RegisterHandlerForModule(string module, Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal(module, "*", null, handler);
    }

    public string RegisterHandlerForAll(Func<EventMessage, Task> handler)
    {
        return RegisterHandlerInternal("*", "*", null, handler);
    }

    private string RegisterHandlerInternal(string modulePattern, string typePattern, string? profileIdPattern, Func<EventMessage, Task> handler)
    {
        var registrationId = $"{modulePattern}.{typePattern}.{profileIdPattern ?? "*"}_{Guid.NewGuid()}";

        _handlers[registrationId] = handler;
        _handlerPatterns[registrationId] = (modulePattern, typePattern, profileIdPattern);
        _handlerEventCache[registrationId] = new ConcurrentDictionary<string, bool>();

        return registrationId;
    }

    public void UnregisterHandler(string registrationId)
    {
        _handlers.TryRemove(registrationId, out _);
        _handlerPatterns.TryRemove(registrationId, out _);
        _handlerEventCache.TryRemove(registrationId, out _);
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
