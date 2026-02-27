using System.Reflection;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;

namespace D3dxSkinManager.Modules.Plugin.Services;


public interface IPluginLoader
{
    Task<int> LoadPluginsAsync();

    Task InitPluginsAsync();
}

/// <summary>
/// Loads plugins from .dll assemblies in the plugins directory.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly IProfilePathService _profilePaths;
    private readonly IPluginContext _pluginContext;
    private readonly IPluginRegistry _registry;
    private readonly ILogHelper _logger;

    public PluginLoader(IProfilePathService profilePaths, IPluginContext pluginContext, IPluginRegistry registry, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _pluginContext = pluginContext;
        _registry = registry;
        _logger = logger;
    }

    public async Task<int> LoadPluginsAsync()
    {
        _logger.Log(LogLevel.Info, $"Loading plugins from: {_profilePaths.PluginsDirectory}", "PluginLoader");

        if (!Directory.Exists(_profilePaths.PluginsDirectory))
        {
            _logger.Log(LogLevel.Info, "Plugins directory does not exist. Creating it.", "PluginLoader");
            Directory.CreateDirectory(_profilePaths.PluginsDirectory);
            return 0;
        }

        var loadedCount = 0;
        var dllFiles = Directory.GetFiles(_profilePaths.PluginsDirectory, "*.dll", SearchOption.AllDirectories);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                if (await LoadPluginFromAssemblyAsync(dllFile))
                    loadedCount++;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to load plugin from {dllFile}: {ex.Message}", "PluginLoader", ex);
            }
        }

        _logger.Log(LogLevel.Info, $"Loaded {loadedCount} plugin(s)", "PluginLoader");
        return loadedCount;
    }

    private Task<bool> LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        _logger.Log(LogLevel.Debug, $"Loading assembly: {assemblyPath}", "PluginLoader");

        var assembly = Assembly.LoadFrom(assemblyPath);
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            _logger.Log(LogLevel.Warn, $"No plugin types found in {Path.GetFileName(assemblyPath)}", "PluginLoader");
            return Task.FromResult(false);
        }

        var loaded = false;
        foreach (var pluginType in pluginTypes)
        {
            try
            {
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                if (plugin == null)
                {
                    _logger.Log(LogLevel.Error, $"Failed to create instance of {pluginType.Name}", "PluginLoader");
                    continue;
                }

                _registry.RegisterPlugin(plugin);
                _logger.Log(LogLevel.Info, $"Loaded plugin: {plugin.Name} v{plugin.Version}", "PluginLoader");
                loaded = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to load plugin {pluginType.Name}: {ex.Message}", "PluginLoader", ex);
            }
        }

        return Task.FromResult(loaded);
    }

    public async Task InitPluginsAsync()
    {
        _logger.Log(LogLevel.Info, "Initializing plugins...", "PluginLoader");

        var plugins = _registry.GetAllPlugins().ToList();
        var initTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"Initializing plugin: {plugin.Name}", "PluginLoader");
                await plugin.InitAsync(_pluginContext).ConfigureAwait(false);
                _logger.Log(LogLevel.Info, $"Initialized plugin: {plugin.Name}", "PluginLoader");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to initialize plugin {plugin.Name}: {ex.Message}", "PluginLoader", ex);
            }
        });

        await Task.WhenAll(initTasks).ConfigureAwait(false);
        _logger.Log(LogLevel.Info, $"Initialized {plugins.Count} plugin(s)", "PluginLoader");
    }

    public async Task DisposePluginsAsync()
    {
        _logger.Log(LogLevel.Info, "Shutting down plugins...", "PluginLoader");

        var plugins = _registry.GetAllPlugins().ToList();
        var shutdownTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"Shutting down plugin: {plugin.Name}", "PluginLoader");
                await plugin.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error shutting down plugin {plugin.Name}: {ex.Message}", "PluginLoader", ex);
            }
        });

        await Task.WhenAll(shutdownTasks).ConfigureAwait(false);
        _logger.Log(LogLevel.Info, "All plugins shut down", "PluginLoader");
    }
}
