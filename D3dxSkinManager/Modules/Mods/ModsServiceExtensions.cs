using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Service registration extensions for Mods module
/// Registers mod management services and facade
/// </summary>
public static class ModsServiceExtensions
{
    /// <summary>
    /// Register Mods module services and facade
    /// </summary>
    public static IServiceCollection AddModsServices(this IServiceCollection services)
    {
        // Register data layer (repositories) - using profile-specific paths
        services.AddSingleton<IModRepository, ModRepository>();

        services.AddSingleton<IClassificationRepository, ClassificationRepository>();

        // Register domain services
        services.AddSingleton<IModFileService, ModFileService>();

        // Register centralized mod management service
        services.AddSingleton<IModManagementService, ModManagementService>();

        services.AddSingleton<IModImportService, ModImportService>();
        services.AddSingleton<IModQueryService, ModQueryService>();

        // Register classification service
        services.AddSingleton<IClassificationService, ClassificationService>();

        // Register facade
        services.AddSingleton<IModFacade, ModFacade>();

        return services;
    }
}
