using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        Console.WriteLine("[PluginsFacade] Registering Plugins services...");

        // Register plugin infrastructure
        services.TryAddSingleton<IPluginLoader, PluginLoader>();
        services.TryAddSingleton<IPluginContext, PluginContext>();
        services.TryAddSingleton<IPluginRegistry, PluginRegistry>();

        // Register facade
        services.TryAddSingleton<IPluginsFacade, PluginsFacade>();
        services.TryAddSingleton<PluginsFacade>();

        Console.WriteLine("[PluginsFacade] Plugins services registered");
        return services;
    }
}
