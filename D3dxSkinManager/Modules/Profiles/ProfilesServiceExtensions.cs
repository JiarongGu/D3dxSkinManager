using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles;

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
    public static IServiceCollection AddProfilesServices(this IServiceCollection services, string dataPath)
    {
        // Register profile service
        services.AddSingleton<IProfileService>(sp => new ProfileService(dataPath));

        // Register profile context (manages active profile and provides profile-specific paths)
        services.AddSingleton<IProfileContext>(sp =>
        {
            var profileService = sp.GetRequiredService<IProfileService>();
            return new ProfileContext(dataPath, profileService);
        });

        // Register facade
        services.AddSingleton<IProfileFacade, ProfileFacade>();

        return services;
    }
}
