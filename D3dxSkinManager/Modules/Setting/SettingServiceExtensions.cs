using D3dxSkinManager.Infrastructure;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.WebView;
using D3dxSkinManager.Modules.Setting.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3dxSkinManager.Modules.Setting;

/// <summary>
/// Service registration extensions for Settings module
/// Registers settings and file dialog services and facade
/// </summary>
public static class SettingServiceExtensions
{
    /// <summary>
    /// The Setting singletons SHARED from the global container into each profile container (resolved from
    /// the parent provider, re-registered as the same instance — see the (services, serviceProvider)
    /// overload). A fixed list, NOT a mutable static that <see cref="AddSettingServices(IServiceCollection)"/>
    /// appended to: that grew on every call and was not thread-safe under concurrent container builds.
    /// Must stay in sync with the registrations below.
    /// </summary>
    private static readonly Type[] SharedServiceTypes =
    {
        typeof(ISettingFileService),
        typeof(ILanguageService),
        typeof(IWindowStateService),
        typeof(IGlobalSettingService),
        typeof(ISettingFacade),
    };

    /// <summary>
    /// Register Settings module services and facade
    /// </summary>
    public static IServiceCollection AddSettingServices(this IServiceCollection services)
    {
        Console.WriteLine("[SettingsFacade] Registering Settings services...");

        // Register the underlying services (using TryAdd to avoid duplicates)
        AddSingleton<ISettingFileService, SettingFileService>(services);
        AddSingleton<ILanguageService, LanguageService>(services);
        AddSingleton<IWindowStateService, WindowStateService>(services);
        AddSingleton<IGlobalSettingService, GlobalSettingService>(services);

        // Register the facade itself
        AddSingleton<ISettingFacade, SettingFacade>(services);

        Console.WriteLine("[SettingsFacade] Settings services registered");
        return services;
    }


    public static IServiceCollection AddSettingServices(this IServiceCollection services, IServiceProvider serviceProvider)
    {
        foreach (var serviceType in SharedServiceTypes)
        {
            var service = serviceProvider.GetService(serviceType);
            if (service != null)
            {
                services.AddSingleton(serviceType, service);
            }
        }
        return services;
    }

    private static IServiceCollection AddSingleton<TService, TImplementation>(IServiceCollection services)
    {
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        return services;
    }

    /// <summary>
    /// Register SettingsFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseSettingsFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<ISettingFacade>();
        if (facade == null)
        {
            Console.WriteLine("[SettingsFacade] Warning: SettingsFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine($"[SettingsFacade] Registering {ModuleNames.SETTING} module handlers");

        // Register the module handler
        dispatcher.UseModule(ModuleNames.SETTING, facade.HandleMessageAsync);

        return dispatcher;
    }
}
