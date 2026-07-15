using D3dxSkinManager.Modules.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.WebView;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Service registration extensions for Profiles module
/// Registers profile management services and facade
/// </summary>
public static class ProfileServiceExtensions
{
    private static readonly List<Type> _registerdServices = new List<Type>();

    /// <summary>
    /// Register global Profile services (no ProfileContext needed)
    /// </summary>
    public static IServiceCollection AddProfileServices(this IServiceCollection services)
    {
        Console.WriteLine("[ProfileFacade] Registering Profile services...");

        // Register profile service provider and service
        AddSingleton<IProfileService, ProfileService>(services);
        AddSingleton<IProfileRepository, ProfileRepository>(services);

        // Profile settings export/import (.zip bundle). GLOBAL service: profile metadata/config/thumbnail
        // via IProfileService; category + remote data via IProfileServiceProvider (source scope for
        // export, new-profile scope for import) without switching the active profile.
        AddSingleton<IProfileBundleService, ProfileBundleService>(services);

        // Register the facade itself
        AddSingleton<IProfileFacade, ProfileFacade>(services);

        Console.WriteLine("[ProfileFacade] Profile services registered");
        return services;
    }

    public static IServiceCollection AddProfileServices(this IServiceCollection services, IServiceProvider serviceProvider)
    {
        foreach (var serviceType in _registerdServices)
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
        _registerdServices.Add(typeof(TService));
        return services;
    }

    /// <summary>
    /// Register ProfileFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseProfileFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<IProfileFacade>();
        if (facade == null)
        {
            Console.WriteLine("[ProfileFacade] Warning: ProfileFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine("[ProfileFacade] Registering PROFILE module handlers");

        // Register the module handler
        dispatcher.UseModule("PROFILE", facade.HandleMessageAsync);

        return dispatcher;
    }
}
