using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        Console.WriteLine("[LaunchFacade] Registering Launch services...");

        // Register 3DMigoto service
        services.TryAddSingleton<I3DMigotoService, D3DMigotoService>();

        // Register facade
        services.TryAddSingleton<ILaunchFacade, LaunchFacade>();
        services.TryAddSingleton<LaunchFacade>();

        Console.WriteLine("[LaunchFacade] Launch services registered");
        return services;
    }
}
