using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Infrastructure;
using D3dxSkinManager.Modules.System.Services;
using D3dxSkinManager.Modules.Core.WebView;

namespace D3dxSkinManager.Modules.System;

/// <summary>
/// Service registration extensions for System module
/// Registers system-level services and facade
/// </summary>
public static class SystemServiceExtensions
{
    /// <summary>
    /// Register System module services and facade
    /// </summary>
    public static IServiceCollection AddSystemServices(this IServiceCollection services)
    {
        Console.WriteLine("[SystemFacade] Registering System services...");
        // Register the underlying services (using TryAdd to avoid duplicates)
        services.TryAddSingleton<IFormInteractionService, FormInteractionService>();
        services.TryAddSingleton<ISystemFileDialogService, SystemFileDialogService>();
        services.TryAddSingleton<ISystemProcessService, SystemProcessService>();
        services.TryAddSingleton<ISystemSettingsService, SystemSettingsService>();
        services.TryAddSingleton<ISystemFileService, SystemFileService>();

        // Register the facade itself
        services.TryAddSingleton<ISystemFacade, SystemFacade>();

        Console.WriteLine("[SystemFacade] System services registered");
        return services;
    }

    /// <summary>
    /// Register SystemFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseSystemFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<ISystemFacade>();
        if (facade == null)
        {
            Console.WriteLine("[SystemFacade] Warning: SystemFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine("[SystemFacade] Registering SYSTEM module handlers");

        // Register the module handler
        dispatcher.UseModule("SYSTEM", facade.HandleMessageAsync);

        return dispatcher;
    }
}
