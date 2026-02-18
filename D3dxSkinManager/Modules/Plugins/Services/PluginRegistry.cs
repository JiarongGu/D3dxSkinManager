namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Registry for managing loaded plugins.
/// Provides plugin discovery, registration, and retrieval.
/// </summary>
public class PluginRegistry
{
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly Dictionary<string, IMessageHandlerPlugin> _messageHandlers = new();
    private readonly object _lock = new();

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

            // Register message handler if plugin implements IMessageHandlerPlugin
            if (plugin is IMessageHandlerPlugin messageHandler)
            {
                foreach (var messageType in messageHandler.GetHandledMessageTypes())
                {
                    if (_messageHandlers.ContainsKey(messageType))
                    {
                        Console.WriteLine($"[PluginRegistry] Warning: Message type '{messageType}' is already handled by another plugin. Overriding.");
                    }
                    _messageHandlers[messageType] = messageHandler;
                }
            }

            Console.WriteLine($"[PluginRegistry] Registered plugin: {plugin.Name} v{plugin.Version} ({plugin.Id})");
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
    /// Check if a message type is handled by any plugin.
    /// </summary>
    /// <param name="messageType">Message type to check</param>
    public bool CanHandleMessage(string messageType)
    {
        lock (_lock)
        {
            return _messageHandlers.ContainsKey(messageType);
        }
    }

    /// <summary>
    /// Get the plugin that handles a specific message type.
    /// </summary>
    /// <param name="messageType">Message type</param>
    /// <returns>Message handler plugin or null if not found</returns>
    public IMessageHandlerPlugin? GetMessageHandler(string messageType)
    {
        lock (_lock)
        {
            return _messageHandlers.TryGetValue(messageType, out var handler) ? handler : null;
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
                // Remove message handlers registered by this plugin
                if (plugin is IMessageHandlerPlugin messageHandler)
                {
                    foreach (var messageType in messageHandler.GetHandledMessageTypes())
                    {
                        _messageHandlers.Remove(messageType);
                    }
                }

                Console.WriteLine($"[PluginRegistry] Unregistered plugin: {plugin.Name} ({plugin.Id})");
                return true;
            }

            return false;
        }
    }
}
