using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Plugin.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Services;

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

    /// <summary>
    /// Register PLUGIN module facade with MessageDispatcher.
    /// Routes all PLUGIN module messages to PluginFacade which then sub-routes to individual plugins.
    /// </summary>
    public static MessageDispatcher UsePluginFacade(this MessageDispatcher dispatcher, IServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<IPluginFacade>();
        if (facade == null)
        {
            Console.WriteLine("[PluginsFacade] Warning: PluginFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine($"[PluginsFacade] Registering PLUGIN module handlers");

        // Register the module handler
        dispatcher.UseModule("PLUGIN", facade.HandleMessageAsync);

        return dispatcher;
    }
}
