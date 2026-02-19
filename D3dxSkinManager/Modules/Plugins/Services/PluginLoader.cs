using System.Reflection;
using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;

using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Plugins.Services;


public interface IPluginLoader
{
    Task<int> LoadPluginsAsync();

    Task InitializePluginsAsync();
}

/// <summary>
/// Loads plugins from the plugins directory.
/// Supports both assembly-based (.dll) and directory-based plugins.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly IProfilePathService _profilePaths;
    private readonly IPluginContext _pluginContext;
    private readonly IPluginRegistry _registry;
    private readonly ILogHelper _logger;

    public PluginLoader(IProfilePathService profilePaths, IPluginContext pluginContext, IPluginRegistry registry, ILogHelper logger)
    {
        _profilePaths = profilePaths ?? throw new ArgumentNullException(nameof(profilePaths));
        _pluginContext = pluginContext ?? throw new ArgumentNullException(nameof(pluginContext));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure plugins directory exists
        _profilePaths.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Discover and load all plugins from the plugins directory.
    /// </summary>
    public async Task<int> LoadPluginsAsync()
    {
        _logger.Log(LogLevel.Info, $"[PluginLoader] Loading plugins from: {_profilePaths.PluginsDirectory}");

        if (!Directory.Exists(_profilePaths.PluginsDirectory))
        {
            _logger.Log(LogLevel.Info, "[PluginLoader] Plugins directory does not exist. Creating it.");
            Directory.CreateDirectory(_profilePaths.PluginsDirectory);
            return 0;
        }

        var loadedCount = 0;

        // Load assembly-based plugins (.dll files)
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
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to load plugin from {dllFile}: {ex.Message}", "PluginLoader", ex);
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
                // NOTE: Service registration at runtime is not supported in this architecture
                // Plugins that need services should be registered during application startup
                if (plugin is IServicePlugin servicePlugin)
                {
                    _logger.Log(LogLevel.Debug, $"[PluginLoader] Plugin {plugin.Name} implements IServicePlugin but runtime service registration is not supported");
                    // servicePlugin.ConfigureServices(_services); // Cannot inject services at runtime
                }

                // Register plugin
                _registry.RegisterPlugin(plugin);

                _logger.Log(LogLevel.Info, $"[PluginLoader] Successfully loaded plugin: {plugin.Name} v{plugin.Version}");
                loaded = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to load plugin {pluginType.Name}: {ex.Message}", "PluginLoader", ex);
            }
        }

        return Task.FromResult(loaded);
    }

    /// <summary>
    /// Initialize all loaded plugins.
    /// Must be called after all services are built.
    /// </summary>
    public async Task InitializePluginsAsync()
    {
        _logger.Log(LogLevel.Info, "[PluginLoader] Initializing plugins...");

        var plugins = _registry.GetAllPlugins().ToList();
        var initTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"[PluginLoader] Initializing plugin: {plugin.Name}");
                await plugin.InitializeAsync(_pluginContext);
                _logger.Log(LogLevel.Info, $"[PluginLoader] Initialized plugin: {plugin.Name}");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"[PluginLoader] Failed to initialize plugin {plugin.Name}: {ex.Message}", "PluginLoader", ex);
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
                _logger.Log(LogLevel.Error, $"[PluginLoader] Error shutting down plugin {plugin.Name}: {ex.Message}", "PluginLoader", ex);
            }
        });

        await Task.WhenAll(shutdownTasks);

        _logger.Log(LogLevel.Info, "[PluginLoader] All plugins shut down");
    }
}
