using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Models;
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

    /// <summary>Track a long-running plugin operation in the app's Activity panel / status bar.
    /// Returns a handle — wrap work in a <c>using</c> (auto-completes) or call Complete/Fail. The
    /// host owns the ProcessRegistry entry, so plugins never touch it (or ProcessType) directly.</summary>
    IPluginProgress ReportProgress(string title, bool cancellable = false);

    void Log(LogLevel level, string message, Exception? exception = null);
}

public class PluginContext : IPluginContext
{
    private readonly IProfilePathService _profilePathService;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    public IMessageDispatcher MessageDispatcher { get; }
    public IEventBus EventBus { get; }

    public PluginContext(
        IMessageDispatcher messageDispatcher,
        IEventBus eventBus,
        IProfilePathService profilePathService,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        MessageDispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _profilePathService = profilePathService ?? throw new ArgumentNullException(nameof(profilePathService));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IPluginProgress ReportProgress(string title, bool cancellable = false)
    {
        // Plugin work is ProcessType.Other on the shared registry — same status-bar/Activity
        // treatment as everything else, without leaking the enum into the plugin contract.
        var id = _processRegistry.Start(ProcessType.Other, title, cancellable);
        return new PluginProgress(_processRegistry, id);
    }

    /// <summary>ProcessRegistry-backed <see cref="IPluginProgress"/>. Finish is idempotent
    /// (registry Complete/Fail are); Dispose completes if the plugin didn't already finish.</summary>
    private sealed class PluginProgress : IPluginProgress
    {
        private readonly IProcessRegistry _registry;
        private readonly string _id;
        private bool _finished;

        public PluginProgress(IProcessRegistry registry, string id)
        {
            _registry = registry;
            _id = id;
        }

        public CancellationToken Token => _registry.GetToken(_id);
        public void Report(int? percent = null, string? detail = null) => _registry.Report(_id, percent, detail);
        public void Complete() { _finished = true; _registry.Complete(_id); }
        public void Fail(string error) { _finished = true; _registry.Fail(_id, error); }
        public void Dispose() { if (!_finished) _registry.Complete(_id); }
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
        return EventBus.Subscribe(modulePattern, typePattern, handler);
    }

    public void UnregisterEventHandler(string registrationId)
    {
        EventBus.Unsubscribe(registrationId);
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

