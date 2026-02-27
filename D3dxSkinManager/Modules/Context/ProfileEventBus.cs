using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Profile-scoped event bus that filters events by ProfileId
/// - Emits: Automatically adds ProfileId to all emitted events
/// - Subscribes: Only receives events for this specific ProfileId
/// </summary>
public interface IProfileEventBus
{
    /// <summary>
    /// Emit event with automatic ProfileId injection
    /// </summary>
    Task EmitAsync(string module, string type, object? payload = null);

    /// <summary>
    /// Emit event message with automatic ProfileId injection
    /// </summary>
    Task EmitAsync(EventMessage message);

    /// <summary>
    /// Register a handler for events in this profile only
    /// </summary>
    /// <param name="module">Module name (e.g., "MOD")</param>
    /// <param name="type">Event type (e.g., "LOADED")</param>
    /// <param name="handler">Event handler</param>
    /// <returns>Registration ID for unregistering</returns>
    string RegisterHandler(string module, string type, Func<EventMessage, Task> handler);

    /// <summary>
    /// Unregister a handler
    /// </summary>
    void UnregisterHandler(string registrationId);
}

/// <summary>
/// Profile-scoped EventBus implementation
/// Acts as a filtered sub-bus of the global EventBus
/// </summary>
public class ProfileEventBus : IProfileEventBus
{
    private readonly IEventBus _globalEventBus;
    private readonly IProfileContext _profileContext;
    private readonly ILogHelper? _logger;

    public ProfileEventBus(IEventBus globalEventBus, IProfileContext profileContext, ILogHelper? logger = null)
    {
        _globalEventBus = globalEventBus ?? throw new ArgumentNullException(nameof(globalEventBus));
        _profileContext = profileContext ?? throw new ArgumentNullException(nameof(profileContext));
        _logger = logger;
    }

    /// <summary>
    /// Emit event with automatic ProfileId injection
    /// </summary>
    public async Task EmitAsync(string module, string type, object? payload = null)
    {
        _logger?.Verbose($"[ProfileEventBus] Emitting {module}.{type} for profile {_profileContext.ProfileId}", "ProfileEventBus");
        await _globalEventBus.EmitAsync(module, type, payload, _profileContext.ProfileId);
    }

    /// <summary>
    /// Emit event message with automatic ProfileId injection (if not already set)
    /// </summary>
    public async Task EmitAsync(EventMessage message)
    {
        // If message doesn't have a profileId, inject it from context
        if (string.IsNullOrEmpty(message.ProfileId))
        {
            message.ProfileId = _profileContext.ProfileId;
        }

        _logger?.Verbose($"[ProfileEventBus] Emitting {message.Module}.{message.Type} for profile {message.ProfileId}", "ProfileEventBus");
        await _globalEventBus.EmitAsync(message);
    }

    /// <summary>
    /// Register a handler that only receives events for this profile
    /// </summary>
    public string RegisterHandler(string module, string type, Func<EventMessage, Task> handler)
    {
        // Register with global bus but filter by this profile's ID
        _logger?.Debug($"[ProfileEventBus] Registering handler for {module}.{type} in profile {_profileContext.ProfileId}", "ProfileEventBus");
        return _globalEventBus.RegisterHandler(module, type, _profileContext.ProfileId, handler);
    }

    /// <summary>
    /// Unregister a handler
    /// </summary>
    public void UnregisterHandler(string registrationId)
    {
        _globalEventBus.UnregisterHandler(registrationId);
    }
}
