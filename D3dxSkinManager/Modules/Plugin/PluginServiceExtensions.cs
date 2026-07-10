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
    /// Register ONLY the plugin registry — for the GLOBAL container. The registry is the shared
    /// seam between the plugin system (profile-scoped loader fills it) and global consumers
    /// (e.g. ContentVeilService discovering IImageReviewPlugin capabilities). Profile containers
    /// re-share the same instance (ProfileServiceRouter.CreateProfileServices).
    /// </summary>
    public static IServiceCollection AddPluginRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<IPluginRegistry, PluginRegistry>();
        return services;
    }

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
        services.TryAddSingleton<IPluginStateStore, PluginStateStore>();
        services.TryAddSingleton<IPluginInstallService, PluginInstallService>();

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
