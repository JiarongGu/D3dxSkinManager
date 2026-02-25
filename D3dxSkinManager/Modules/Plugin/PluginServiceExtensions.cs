using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Modules.Plugin;

/// <summary>
/// Service registration extensions for Plugins module
/// Registers plugin management services and facade
/// </summary>
public static class PluginServiceExtensions
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
        services.TryAddSingleton<IPluginFacade, PluginFacade>();
        services.TryAddSingleton<PluginFacade>();

        Console.WriteLine("[PluginsFacade] Plugins services registered");
        return services;
    }
}
