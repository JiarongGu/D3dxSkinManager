namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Base interface for all plugins in the system.
/// Plugins can extend functionality without modifying core code.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for the plugin (e.g., "com.example.myplugin").
    /// Must be unique across all plugins.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version (semantic versioning recommended).
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin author/organization.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Initialize the plugin with access to core services.
    /// Called once during application startup after all services are registered.
    /// </summary>
    /// <param name="context">Plugin context providing access to services and configuration</param>
    Task InitializeAsync(IPluginContext context);

    /// <summary>
    /// Shutdown the plugin and cleanup resources.
    /// Called during application shutdown.
    /// </summary>
    Task ShutdownAsync();
}
