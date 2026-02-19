using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Plugins;

/// <summary>
/// Service registration extensions for Plugins module
/// Registers plugin management services and facade
/// </summary>
public static class PluginsServiceExtensions
{
    /// <summary>
    /// Register Plugins module services and facade
    /// </summary>
    public static IServiceCollection AddPluginsServices(this IServiceCollection services)
    {
        // Register facade (depends on root-level PluginRegistry)
        services.AddSingleton<IPluginsFacade, PluginsFacade>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<IPluginContext, PluginContext>();
        services.AddSingleton<IPluginEventBus, PluginEventBus>();
        services.AddSingleton<IPluginRegistry, PluginRegistry>();

        return services;
    }
}
