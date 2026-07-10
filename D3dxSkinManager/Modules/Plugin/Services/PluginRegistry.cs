using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>One registered plugin + its runtime state.</summary>
public sealed class PluginEntry
{
    public required IPlugin Plugin { get; init; }

    /// <summary>Disabled plugins stay listed (management UI) but are invisible to consumers.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>InitAsync has run — a plugin enabled later still needs its init.</summary>
    public bool Initialized { get; set; }
}

public interface IPluginRegistry
{
    void RegisterPlugin(IPlugin plugin, bool enabled = true);
    /// <summary>ENABLED plugin by id, or null.</summary>
    IPlugin? GetPlugin(string pluginId);
    /// <summary>All ENABLED plugins.</summary>
    IEnumerable<IPlugin> GetAllPlugins();
    /// <summary>ENABLED plugins exposing a typed capability.</summary>
    IEnumerable<T> GetPlugins<T>() where T : IPlugin;
    /// <summary>Every registered plugin with its state (management UI).</summary>
    IReadOnlyList<PluginEntry> GetAllEntries();
    PluginEntry? GetEntry(string pluginId);
    int GetPluginCount();
    bool UnregisterPlugin(string pluginId);
}

/// <summary>
/// Registry for managing loaded plugins — SHARED between the global container (capability
/// consumers, e.g. the content veil) and the profile containers (loader/facade).
/// </summary>
public class PluginRegistry : IPluginRegistry
{
    private readonly ILogHelper _logger;
    private readonly Dictionary<string, PluginEntry> _plugins = new();
    private readonly object _lock = new();

    public PluginRegistry(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterPlugin(IPlugin plugin, bool enabled = true)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (string.IsNullOrWhiteSpace(plugin.Id))
            throw new ArgumentException("Plugin ID cannot be null or empty");

        lock (_lock)
        {
            if (_plugins.ContainsKey(plugin.Id))
            {
                // The registry is SHARED across profile containers — a profile switch re-loading
                // the same plugin id keeps the first instance (it may be in active use).
                _logger.Info($"Plugin '{plugin.Id}' already registered — keeping the existing instance", "PluginRegistry");
                return;
            }

            _plugins[plugin.Id] = new PluginEntry { Plugin = plugin, Enabled = enabled };

            _logger.Info($"Registered plugin: {plugin.Name} v{plugin.Version} ({plugin.Id}, enabled={enabled})", "PluginRegistry");
        }
    }

    public IPlugin? GetPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(pluginId, out var entry) && entry.Enabled ? entry.Plugin : null;
        }
    }

    public IEnumerable<IPlugin> GetAllPlugins()
    {
        lock (_lock)
        {
            return _plugins.Values.Where(e => e.Enabled).Select(e => e.Plugin).ToList();
        }
    }

    public IEnumerable<T> GetPlugins<T>() where T : IPlugin
    {
        lock (_lock)
        {
            return _plugins.Values.Where(e => e.Enabled).Select(e => e.Plugin).OfType<T>().ToList();
        }
    }

    public IReadOnlyList<PluginEntry> GetAllEntries()
    {
        lock (_lock)
        {
            return _plugins.Values.ToList();
        }
    }

    public PluginEntry? GetEntry(string pluginId)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(pluginId, out var entry) ? entry : null;
        }
    }

    public int GetPluginCount()
    {
        lock (_lock)
        {
            return _plugins.Count;
        }
    }

    public bool UnregisterPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _plugins.Remove(pluginId);
        }
    }
}
