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
    /// <summary>
    /// Register global Profile services (no ProfileContext needed)
    /// </summary>
    public static IServiceCollection AddProfileServices(this IServiceCollection services)
    {
        Console.WriteLine("[ProfileFacade] Registering Profile services...");

        // Register profile service provider and service
        services.TryAddSingleton<IProfileService, ProfileService>();
        services.TryAddSingleton<IProfileRepository, ProfileRepository>();

        // Register the facade itself
        services.TryAddSingleton<IProfileFacade, ProfileFacade>();

        Console.WriteLine("[ProfileFacade] Profile services registered");
        return services;
    }

    /// <summary>
    /// Register profile-scoped services (requires ProfileContext)
    /// This is called for each profile's ServiceProvider
    /// </summary>
    public static IServiceCollection AddProfileScopedServices(this IServiceCollection services)
    {
        // Register profile path service for centralized path management
        services.TryAddScoped<IProfilePathService, ProfilePathService>();

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
