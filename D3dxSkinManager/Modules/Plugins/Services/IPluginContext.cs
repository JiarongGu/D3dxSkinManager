using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Provides plugins with access to core services and functionality.
/// Acts as a service locator for plugin dependencies.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Access to mod operations (get, load, unload, delete, import).
    /// </summary>
    IModFacade ModFacade { get; }

    /// <summary>
    /// Access to mod repository (data access layer).
    /// </summary>
    IModRepository ModRepository { get; }

    /// <summary>
    /// Access to file operations (SHA256, 7-Zip extraction).
    /// </summary>
    IFileService FileService { get; }

    /// <summary>
    /// Access to mod auto-detection service.
    /// </summary>
    IModAutoDetectionService ModAutoDetectionService { get; }

    /// <summary>
    /// Access to image processing service.
    /// </summary>
    IImageService ImageService { get; }

    /// <summary>
    /// Plugin data directory for storing plugin-specific files.
    /// Format: {AppData}/plugins/{pluginId}/
    /// </summary>
    string GetPluginDataPath(string pluginId);

    /// <summary>
    /// Get a service from the DI container.
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <returns>Service instance or null if not registered</returns>
    T? GetService<T>() where T : class;

    /// <summary>
    /// Register an event handler for system events.
    /// </summary>
    /// <param name="eventType">Event type to listen for</param>
    /// <param name="handler">Event handler callback</param>
    /// <returns>Registration ID for unregistering later</returns>
    string RegisterEventHandler(PluginEventType eventType, Func<PluginEventArgs, Task> handler);

    /// <summary>
    /// Unregister an event handler.
    /// </summary>
    /// <param name="registrationId">Registration ID from RegisterEventHandler</param>
    void UnregisterEventHandler(string registrationId);

    /// <summary>
    /// Emit a custom event that other plugins can listen to.
    /// </summary>
    /// <param name="eventName">Custom event name</param>
    /// <param name="data">Event data</param>
    Task EmitEventAsync(string eventName, object? data = null);

    /// <summary>
    /// Log a message from the plugin.
    /// </summary>
    /// <param name="level">Log level</param>
    /// <param name="message">Log message</param>
    /// <param name="exception">Optional exception</param>
    void Log(LogLevel level, string message, Exception? exception = null);
}

/// <summary>
/// Log levels for plugin logging.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// System event types that plugins can listen to.
/// </summary>
public enum PluginEventType
{
    /// <summary>
    /// Fired when application starts up (after plugin initialization).
    /// </summary>
    ApplicationStarted,

    /// <summary>
    /// Fired when application is shutting down.
    /// </summary>
    ApplicationShutdown,

    /// <summary>
    /// Fired when a mod is loaded into the game.
    /// </summary>
    ModLoaded,

    /// <summary>
    /// Fired when a mod is unloaded from the game.
    /// </summary>
    ModUnloaded,

    /// <summary>
    /// Fired when a mod is deleted.
    /// </summary>
    ModDeleted,

    /// <summary>
    /// Fired when a new mod is imported.
    /// </summary>
    ModImported,

    /// <summary>
    /// Fired when mod data is refreshed.
    /// </summary>
    ModsRefreshed,

    /// <summary>
    /// Fired when classification tree is updated.
    /// </summary>
    ClassificationTreeChanged,

    /// <summary>
    /// Custom event emitted by other plugins.
    /// </summary>
    CustomEvent
}

/// <summary>
/// Event arguments passed to plugin event handlers.
/// </summary>
public class PluginEventArgs
{
    public PluginEventType EventType { get; set; }
    public string? EventName { get; set; }  // For CustomEvent type
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
