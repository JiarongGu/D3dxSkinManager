using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Modules.Mod;

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
        services.TryAddSingleton<ITagRepository, TagRepository>();

        //// Register domain services
        services.TryAddSingleton<IModCacheWatcher, ModCacheWatcher>(); // File system watcher for cache directory changes

        // Core mod services (refactored from ModFileService and ModManagementService)
        services.TryAddSingleton<IModArchiveService, ModArchiveService>(); // Archive operations (extract, copy, delete)
        services.TryAddSingleton<IModCacheService, ModCacheService>(); // Cache management (enable, disable, scan, clean)
        services.TryAddSingleton<IModLifecycleService, ModLifecycleService>(); // Load/unload business logic
        services.TryAddSingleton<IModEnrichmentService, ModEnrichmentService>(); // Enrichment of mod data with transient fields

        // Other mod services
        services.TryAddSingleton<IModImportService, ModImportService>();
        services.TryAddSingleton<IModQueryService, ModQueryService>();
        services.TryAddSingleton<IModMetadataService, ModMetadataService>(); // Merged metadata + management operations
        services.TryAddSingleton<IModTagService, ModTagService>();
        services.TryAddSingleton<IModKeybindingService, ModKeybindingService>();

        // Register facade
        services.TryAddSingleton<IModFacade, ModFacade>();

        // Register event handler (subscribes to mod events and emits MOD_LIST_UPDATED)
        services.TryAddSingleton<IModListEventHandler, ModListEventHandler>();

        Console.WriteLine("[ModFacade] Mods services registered");
        return services;
    }

    /// <summary>
    /// Ensures ModListEventHandler is eagerly instantiated when profile scope is created
    /// Call this after the service provider is built for the profile
    /// </summary>
    public static void InitializeModListEventHandler(IServiceProvider serviceProvider)
    {
        // Eagerly resolve the event handler to ensure it subscribes to events
        var handler = serviceProvider.GetRequiredService<IModListEventHandler>();
        Console.WriteLine("[ModFacade] ModListEventHandler initialized and listening for events");
    }
}
