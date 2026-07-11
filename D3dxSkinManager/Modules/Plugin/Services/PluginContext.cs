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

    /// <summary>Plugin data directory — the folder the plugin's OWN DLL was loaded from (its install
    /// dir), so a pack is ONE folder (dll + any extracted natives together) rather than split across an
    /// install dir and a separate per-id data dir.</summary>
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
    private readonly IPluginRegistry _registry;
    private readonly ILogHelper _logger;

    public IMessageDispatcher MessageDispatcher { get; }
    public IEventBus EventBus { get; }

    public PluginContext(
        IMessageDispatcher messageDispatcher,
        IEventBus eventBus,
        IProfilePathService profilePathService,
        IProcessRegistry processRegistry,
        IPluginRegistry registry,
        ILogHelper logger)
    {
        MessageDispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _profilePathService = profilePathService ?? throw new ArgumentNullException(nameof(profilePathService));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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

        // A plugin's data lives NEXT TO ITS DLL (its install dir) → one folder per pack, instead of a
        // separate {plugins}/{pluginId} dir that split a pack across two folders (dll under the pack-id
        // dir, extracted natives under the plugin-id dir). Packs always load from disk, so the assembly
        // location is populated — no fallback (an in-memory/single-file load would throw here; noted).
        var location = _registry.GetEntry(pluginId)!.Plugin.GetType().Assembly.Location;
        var dataPath = Path.GetDirectoryName(location)!;
        Directory.CreateDirectory(dataPath);

        // One-time migration: retire the legacy {plugins}/{pluginId} data dir (only ever held
        // regenerable extracted natives) so the pack really is a single folder. No-op when the dll
        // already sits at {plugins}/{pluginId}.
        var legacyDir = Path.Combine(_profilePathService.PluginsDirectory, pluginId);
        if (Directory.Exists(legacyDir) &&
            !string.Equals(Path.GetFullPath(legacyDir), Path.GetFullPath(dataPath), StringComparison.OrdinalIgnoreCase))
        {
            try { Directory.Delete(legacyDir, recursive: true); }
            catch (Exception ex) { _logger.Log(LogLevel.Debug, $"Legacy plugin data dir cleanup skipped: {ex.Message}", "PluginContext"); }
        }

        return dataPath;
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

