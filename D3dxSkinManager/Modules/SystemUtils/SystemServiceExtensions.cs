using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.SystemUtils.Services;

namespace D3dxSkinManager.Modules.SystemUtils;

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
        // Register services
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();

        // Register facade
        services.AddSingleton<ISystemFacade, SystemFacade>();

        return services;
    }
}
