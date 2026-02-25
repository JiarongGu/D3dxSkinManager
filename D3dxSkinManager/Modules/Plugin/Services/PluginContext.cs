using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Modules.Plugin.Services;

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
    IFileHelper FileService { get; }

    /// <summary>
    /// Access to mod auto-detection service (Category based on file patterns).
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
    /// <param name="modulePattern">Module pattern ("*" for all modules, or specific module like "MOD")</param>
    /// <param name="typePattern">Type pattern ("*" for all types, or specific type like "LOADED")</param>
    /// <param name="handler">Event handler callback</param>
    /// <returns>Registration ID for unregistering later</returns>
    string RegisterEventHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler);

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
/// Default implementation of IPluginContext.
/// Provides plugins with access to core services and functionality.
/// </summary>
public class PluginContext : IPluginContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProfilePathService _profilePathService;
    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;

    public IModFacade ModFacade { get; }
    public IModRepository ModRepository { get; }
    public IFileHelper FileService { get; }
    public IModAutoDetectionService ModAutoDetectionService { get; }
    public IImageService ImageService { get; }

    public PluginContext(
        IServiceProvider serviceProvider,
        IProfileContext profileContext,
        IProfilePathService profilePathService,
        IEventBus eventBus,
        ILogHelper logger)
    {
        _serviceProvider = serviceProvider;
        _profilePathService = profilePathService;
        _eventBus = eventBus;
        _logger = logger;

        // Resolve core services
        ModFacade = _serviceProvider.GetRequiredService<IModFacade>();
        ModRepository = _serviceProvider.GetRequiredService<IModRepository>();
        FileService = _serviceProvider.GetRequiredService<IFileHelper>();
        ModAutoDetectionService = _serviceProvider.GetRequiredService<IModAutoDetectionService>();
        ImageService = _serviceProvider.GetRequiredService<IImageService>();
    }

    public string GetPluginDataPath(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        var pluginDataPath = Path.Combine(_profilePathService.PluginsDirectory, pluginId);

        // Create directory if it doesn't exist
        if (!Directory.Exists(pluginDataPath))
            Directory.CreateDirectory(pluginDataPath);

        return pluginDataPath;
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }

    public string RegisterEventHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler)
    {
        return _eventBus.RegisterHandler(modulePattern, typePattern, handler);
    }

    public void UnregisterEventHandler(string registrationId)
    {
        _eventBus.UnregisterHandler(registrationId);
    }

    public Task EmitEventAsync(string eventName, object? data = null)
    {
        // Plugins can emit custom events with dynamic event names
        return _eventBus.EmitAsync(ModuleNames.PLUGIN, eventName, data);
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, "PluginContext", exception);
    }
}