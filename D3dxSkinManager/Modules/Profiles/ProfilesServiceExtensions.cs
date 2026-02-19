using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Service registration extensions for Profiles module
/// Registers profile management services and facade
/// </summary>
public static class ProfilesServiceExtensions
{
    /// <summary>
    /// Register Profiles module services and facade
    /// </summary>
    public static IServiceCollection AddProfilesServices(this IServiceCollection services)
    {
        // Register profile service with PathHelper for portable path storage
        services.AddSingleton<IProfileService, ProfileService>();

        // Register facade
        services.AddSingleton<IProfileFacade, ProfileFacade>();

        return services;
    }
}
