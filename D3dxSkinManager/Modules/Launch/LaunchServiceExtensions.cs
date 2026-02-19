using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Launch.Services;

namespace D3dxSkinManager.Modules.Launch;

/// <summary>
/// Service registration extensions for Launch module
/// Registers 3DMigoto and game launch services and facade
/// </summary>
public static class LaunchServiceExtensions
{
    /// <summary>
    /// Register Launch module services and facade
    /// </summary>
    public static IServiceCollection AddLaunchServices(this IServiceCollection services)
    {
        // Register 3DMigoto service
        services.AddSingleton<I3DMigotoService, D3DMigotoService>();

        // Register facade
        services.AddSingleton<ILaunchFacade, LaunchFacade>();

        return services;
    }
}
