using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Service registration extensions for Mods module
/// Registers mod management services and facade (profile-scoped)
/// </summary>
public static class ModsServiceExtensions
{
    /// <summary>
    /// Register Mods module services and facade (profile-scoped)
    /// These services are instantiated per profile and use ProfileContext
    /// </summary>
    public static IServiceCollection AddModsServices(this IServiceCollection services)
    {
        Console.WriteLine("[ModFacade] Registering Mods services (profile-scoped)...");
        //// Register data layer (repositories) - using profile-specific paths
        services.TryAddSingleton<IModRepository, ModRepository>();
        services.TryAddSingleton<IClassificationRepository, ClassificationRepository>();
        services.TryAddSingleton<ITagRepository, TagRepository>();

        //// Register domain services
        services.TryAddSingleton<IModFileService, ModFileService>();
        services.TryAddSingleton<IModManagementService, ModManagementService>();
        services.TryAddSingleton<IModImportService, ModImportService>();
        services.TryAddSingleton<IModQueryService, ModQueryService>();
        services.TryAddSingleton<IModMetadataService, ModMetadataService>();
        services.TryAddSingleton<ITagService, TagService>();

        //// Register classification service
        services.TryAddSingleton<IClassificationService, ClassificationService>();

        // Register facade
        services.TryAddSingleton<IModFacade, ModFacade>();

        Console.WriteLine("[ModFacade] Mods services registered");
        return services;
    }
}
