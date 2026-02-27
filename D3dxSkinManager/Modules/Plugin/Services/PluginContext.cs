using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// Provides plugins access to core services via EventBus and MessageDispatcher.
/// </summary>
public interface IPluginContext
{
    IMessageDispatcher MessageDispatcher { get; }
    IEventBus EventBus { get; }

    /// <summary>Plugin data directory: {ProfilePath}/plugins/{pluginId}/</summary>
    string GetPluginDataPath(string pluginId);

    string RegisterEventHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler);
    void UnregisterEventHandler(string registrationId);

    /// <summary>NOTE: Plugin events are NOT currently used. Reserved for future cross-plugin communication.</summary>
    Task EmitEventAsync(string eventType, object? payload = null);

    void Log(LogLevel level, string message, Exception? exception = null);
}

public class PluginContext : IPluginContext
{
    private readonly IProfilePathService _profilePathService;
    private readonly ILogHelper _logger;

    public IMessageDispatcher MessageDispatcher { get; }
    public IEventBus EventBus { get; }

    public PluginContext(
        IMessageDispatcher messageDispatcher,
        IEventBus eventBus,
        IProfilePathService profilePathService,
        ILogHelper logger)
    {
        MessageDispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _profilePathService = profilePathService ?? throw new ArgumentNullException(nameof(profilePathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string GetPluginDataPath(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        var pluginDataPath = Path.Combine(_profilePathService.PluginsDirectory, pluginId);

        if (!Directory.Exists(pluginDataPath))
            Directory.CreateDirectory(pluginDataPath);

        return pluginDataPath;
    }

    public string RegisterEventHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler)
    {
        return EventBus.RegisterHandler(modulePattern, typePattern, handler);
    }

    public void UnregisterEventHandler(string registrationId)
    {
        EventBus.UnregisterHandler(registrationId);
    }

    public Task EmitEventAsync(string eventType, object? payload = null)
    {
        return EventBus.EmitAsync(ModuleNames.PLUGIN, eventType, payload);
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, "Plugin", exception);
    }
}