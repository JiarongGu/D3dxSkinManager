using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Default implementation of IPluginContext.
/// Provides plugins with access to core services and functionality.
/// </summary>
public class PluginContext : IPluginContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dataPath;
    private readonly PluginEventBus _eventBus;
    private readonly ILogger _logger;

    public IModFacade ModFacade { get; }
    public IModRepository ModRepository { get; }
    public IFileService FileService { get; }
    public IModAutoDetectionService ModAutoDetectionService { get; }
    public IImageService ImageService { get; }

    public PluginContext(
        IServiceProvider serviceProvider,
        string dataPath,
        PluginEventBus eventBus,
        ILogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        _logger.Log(level, message, exception);
    }
}

/// <summary>
/// Simple logger interface for plugin logging.
/// </summary>
public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
}

/// <summary>
/// Console-based logger implementation.
/// </summary>
public class ConsoleLogger : ILogger
{
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logMessage = $"[{timestamp}] [{level}] {message}";

        if (exception != null)
            logMessage += $"\n{exception}";

        Console.WriteLine(logMessage);
    }
}
