using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;

namespace D3dxSkinManager.Modules.Plugin.Services;

public interface IPluginRegistry
{
    void RegisterPlugin(IPlugin plugin);
    IPlugin? GetPlugin(string pluginId);
    IEnumerable<IPlugin> GetAllPlugins();
    IEnumerable<T> GetPlugins<T>() where T : IPlugin;
    int GetPluginCount();
    bool UnregisterPlugin(string pluginId);
}

/// <summary>
/// Registry for managing loaded plugins.
/// Provides plugin discovery, registration, and retrieval.
/// </summary>
public class PluginRegistry: IPluginRegistry
{
    private readonly ILogHelper _logger;
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly object _lock = new();

    public PluginRegistry(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a plugin in the registry.
    /// </summary>
    /// <param name="plugin">Plugin to register</param>
    /// <exception cref="InvalidOperationException">If plugin ID is already registered</exception>
    public void RegisterPlugin(IPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (string.IsNullOrWhiteSpace(plugin.Id))
            throw new ArgumentException("Plugin ID cannot be null or empty");

        lock (_lock)
        {
            if (_plugins.ContainsKey(plugin.Id))
                throw new InvalidOperationException($"Plugin with ID '{plugin.Id}' is already registered");

            _plugins[plugin.Id] = plugin;

            _logger.Info($"Registered plugin: {plugin.Name} v{plugin.Version} ({plugin.Id})", "PluginRegistry");
        }
    }

    /// <summary>
    /// Get a plugin by ID.
    /// </summary>
    /// <param name="pluginId">Plugin ID</param>
    /// <returns>Plugin instance or null if not found</returns>
    public IPlugin? GetPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Get all registered plugins.
    /// </summary>
    public IEnumerable<IPlugin> GetAllPlugins()
    {
        lock (_lock)
        {
            return _plugins.Values.ToList();
        }
    }

    /// <summary>
    /// Get plugins of a specific type.
    /// </summary>
    /// <typeparam name="T">Plugin type</typeparam>
    public IEnumerable<T> GetPlugins<T>() where T : IPlugin
    {
        lock (_lock)
        {
            return _plugins.Values.OfType<T>().ToList();
        }
    }

    /// <summary>
    /// Get count of registered plugins.
    /// </summary>
    public int GetPluginCount()
    {
        lock (_lock)
        {
            return _plugins.Count;
        }
    }

    /// <summary>
    /// Unregister a plugin.
    /// </summary>
    /// <param name="pluginId">Plugin ID to unregister</param>
    /// <returns>True if plugin was unregistered, false if not found</returns>
    public bool UnregisterPlugin(string pluginId)
    {
        lock (_lock)
        {
            if (_plugins.Remove(pluginId, out var plugin))
            {
                _logger.Info($"Unregistered plugin: {plugin.Name} ({plugin.Id})", "PluginRegistry");
                return true;
            }

            return false;
        }
    }
}
