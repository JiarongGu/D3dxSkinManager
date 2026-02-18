using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Facades;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.D3DMigoto;
using D3dxSkinManager.Modules.Game;
using D3dxSkinManager.Modules.Tools;
using D3dxSkinManager.Modules.Settings;
using D3dxSkinManager.Modules.Plugins;
using D3dxSkinManager.Modules.Warehouse;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Configuration;

/// <summary>
/// Main service registration extensions
/// Orchestrates registration of all application modules
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all application services using modular extensions
    /// </summary>
    public static IServiceCollection AddD3dxSkinManagerServices(this IServiceCollection services, string dataPath)
    {
        // Register Core module services (shared services used across all modules)
        services.AddCoreServices();
        services.AddImageService(dataPath);
        services.AddImageServer(dataPath);

        // Register Profiles module (required by Game and other modules)
        services.AddProfilesServices(dataPath);

        // Register Tools module (provides Configuration, Cache, Validation services)
        services.AddToolsServices(dataPath);

        // Register Mods module (mod management)
        services.AddModsServices(dataPath);

        // Register D3DMigoto module (3DMigoto version management)
        services.AddD3DMigotoServices(dataPath);

        // Register Game module (game launching)
        services.AddGameServices();

        // Register Settings module (settings and file dialogs)
        services.AddSettingsServices(dataPath);

        // Register Plugins module (plugin management)
        services.AddPluginsServices();

        // Register Warehouse module (future: mod discovery and download)
        services.AddWarehouseServices();

        // Register Migration module (Python to React migration)
        services.AddMigrationServices(dataPath);

        // Register top-level application facade (routes IPC messages to module facades)
        services.AddSingleton<IAppFacade, AppFacade>();

        // Register plugin system (root-level plugin infrastructure)
        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddSingleton<PluginRegistry>();
        services.AddSingleton<PluginEventBus>();
        services.AddSingleton<PluginLoader>(sp =>
        {
            var pluginsPath = Path.Combine(dataPath, "plugins");
            var registry = sp.GetRequiredService<PluginRegistry>();
            var logger = sp.GetRequiredService<ILogger>();
            return new PluginLoader(pluginsPath, registry, services, logger);
        });
        services.AddSingleton(sp =>
        {
            var eventBus = sp.GetRequiredService<PluginEventBus>();
            var logger = sp.GetRequiredService<ILogger>();
            return new PluginContext(sp, dataPath, eventBus, logger);
        });

        return services;
    }

    /// <summary>
    /// Load and initialize plugins from the plugins directory.
    /// This should be called after services are built but before the application starts.
    /// </summary>
    public static Task<IServiceProvider> LoadAndInitializePluginsAsync(
        this IServiceProvider serviceProvider,
        string dataPath)
    {
        var logger = serviceProvider.GetRequiredService<ILogger>();
        var registry = serviceProvider.GetRequiredService<PluginRegistry>();
        var context = serviceProvider.GetRequiredService<PluginContext>();

        // Create plugins directory if it doesn't exist
        var pluginsPath = Path.Combine(dataPath, "plugins");
        if (!Directory.Exists(pluginsPath))
        {
            Directory.CreateDirectory(pluginsPath);
            logger.Log(LogLevel.Info, $"[Plugin System] Created plugins directory: {pluginsPath}");
        }

        // Note: We need to pass IServiceCollection for plugin service registration,
        // but at this point services are already built. Plugins loaded after this
        // won't be able to register services. For full plugin support, we'd need
        // to load plugins before building the service provider.
        // For now, plugins can only access existing services.

        logger.Log(LogLevel.Info, "[Plugin System] Plugin system initialized (services already built)");
        logger.Log(LogLevel.Info, $"[Plugin System] Plugins directory: {pluginsPath}");

        return Task.FromResult(serviceProvider);
    }
}
