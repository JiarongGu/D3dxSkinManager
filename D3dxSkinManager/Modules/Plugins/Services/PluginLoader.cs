using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Loads plugins from the plugins directory.
/// Supports both assembly-based (.dll) and directory-based plugins.
/// </summary>
public class PluginLoader
{
    private readonly string _pluginsPath;
    private readonly PluginRegistry _registry;
    private readonly IServiceCollection _services;
    private readonly ILogger _logger;

    public PluginLoader(string pluginsPath, PluginRegistry registry, IServiceCollection services, ILogger logger)
    {
        _pluginsPath = pluginsPath ?? throw new ArgumentNullException(nameof(pluginsPath));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discover and load all plugins from the plugins directory.
    /// </summary>
    public async Task<int> LoadPluginsAsync()
    {
        _logger.Log(LogLevel.Info, $"[PluginLoader] Loading plugins from: {_pluginsPath}");

        if (!Directory.Exists(_pluginsPath))
        {
            _logger.Log(LogLevel.Info, "[PluginLoader] Plugins directory does not exist. Creating it.");
            Directory.CreateDirectory(_pluginsPath);
            return 0;
        }

        var loadedCount = 0;

        // Load assembly-based plugins (.dll files)
        var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories);
        foreach (var dllFile in dllFiles)
        {
            try
            {
                if (await LoadPluginFromAssemblyAsync(dllFile))
                    loadedCount++;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to load plugin from {dllFile}: {ex.Message}", ex);
            }
        }

        _logger.Log(LogLevel.Info, $"[PluginLoader] Loaded {loadedCount} plugin(s)");
        return loadedCount;
    }

    /// <summary>
    /// Load a plugin from a .NET assembly (.dll file).
    /// </summary>
    private Task<bool> LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        _logger.Log(LogLevel.Debug, $"[PluginLoader] Loading assembly: {assemblyPath}");

        // Load assembly
        var assembly = Assembly.LoadFrom(assemblyPath);

        // Find types that implement IPlugin
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            _logger.Log(LogLevel.Warning, $"[PluginLoader] No plugin types found in {Path.GetFileName(assemblyPath)}");
            return Task.FromResult(false);
        }

        var loaded = false;
        foreach (var pluginType in pluginTypes)
        {
            try
            {
                // Create plugin instance
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                if (plugin == null)
                {
                    _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to create instance of {pluginType.Name}");
                    continue;
                }

                // Register services if plugin implements IServicePlugin
                if (plugin is IServicePlugin servicePlugin)
                {
                    _logger.Log(LogLevel.Debug, $"[PluginLoader] Configuring services for plugin: {plugin.Name}");
                    servicePlugin.ConfigureServices(_services);
                }

                // Register plugin
                _registry.RegisterPlugin(plugin);

                _logger.Log(LogLevel.Info, $"[PluginLoader] Successfully loaded plugin: {plugin.Name} v{plugin.Version}");
                loaded = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to load plugin {pluginType.Name}: {ex.Message}", ex);
            }
        }

        return Task.FromResult(loaded);
    }

    /// <summary>
    /// Initialize all loaded plugins.
    /// Must be called after all services are built.
    /// </summary>
    public async Task InitializePluginsAsync(IPluginContext context)
    {
        _logger.Log(LogLevel.Info, "[PluginLoader] Initializing plugins...");

        var plugins = _registry.GetAllPlugins().ToList();
        var initTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"[PluginLoader] Initializing plugin: {plugin.Name}");
                await plugin.InitializeAsync(context);
                _logger.Log(LogLevel.Info, $"[PluginLoader] Initialized plugin: {plugin.Name}");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to initialize plugin {plugin.Name}: {ex.Message}", ex);
            }
        });

        await Task.WhenAll(initTasks);

        _logger.Log(LogLevel.Info, $"[PluginLoader] Initialized {plugins.Count} plugin(s)");
    }

    /// <summary>
    /// Shutdown all loaded plugins.
    /// </summary>
    public async Task ShutdownPluginsAsync()
    {
        _logger.Log(LogLevel.Info, "[PluginLoader] Shutting down plugins...");

        var plugins = _registry.GetAllPlugins().ToList();
        var shutdownTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"[PluginLoader] Shutting down plugin: {plugin.Name}");
                await plugin.ShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Error shutting down plugin {plugin.Name}: {ex.Message}", ex);
            }
        });

        await Task.WhenAll(shutdownTasks);

        _logger.Log(LogLevel.Info, "[PluginLoader] All plugins shut down");
    }
}
