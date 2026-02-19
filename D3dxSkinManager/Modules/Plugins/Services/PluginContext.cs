using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;

using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Default implementation of IPluginContext.
/// Provides plugins with access to core services and functionality.
/// </summary>
public class PluginContext : IPluginContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dataPath;
    private readonly IPluginEventBus _eventBus;
    private readonly ILogHelper _logger;

    public IModFacade ModFacade { get; }
    public IModRepository ModRepository { get; }
    public IFileService FileService { get; }
    public IModAutoDetectionService ModAutoDetectionService { get; }
    public IImageService ImageService { get; }

    public PluginContext(
        IServiceProvider serviceProvider,
        IProfileContext profileContext,
        IPluginEventBus eventBus,
        ILogHelper logger)
    {
        _serviceProvider = serviceProvider;
        _dataPath = profileContext.ProfilePath;
        _eventBus = eventBus;
        _logger = logger;

        // Resolve core services
        ModFacade = _serviceProvider.GetRequiredService<IModFacade>();
        ModRepository = _serviceProvider.GetRequiredService<IModRepository>();
        FileService = _serviceProvider.GetRequiredService<IFileService>();
        ModAutoDetectionService = _serviceProvider.GetRequiredService<IModAutoDetectionService>();
        ImageService = _serviceProvider.GetRequiredService<IImageService>();
    }

    public string GetPluginDataPath(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        var pluginDataPath = Path.Combine(_dataPath, "plugins", pluginId);

        // Create directory if it doesn't exist
        if (!Directory.Exists(pluginDataPath))
            Directory.CreateDirectory(pluginDataPath);

        return pluginDataPath;
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }

    public string RegisterEventHandler(PluginEventType eventType, Func<PluginEventArgs, Task> handler)
    {
        return _eventBus.RegisterHandler(eventType, handler);
    }

    public void UnregisterEventHandler(string registrationId)
    {
        _eventBus.UnregisterHandler(registrationId);
    }

    public Task EmitEventAsync(string eventName, object? data = null)
    {
        return _eventBus.EmitAsync(new PluginEventArgs
        {
            EventType = PluginEventType.CustomEvent,
            EventName = eventName,
            Data = data
        });
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, "PluginContext", exception);
    }
}