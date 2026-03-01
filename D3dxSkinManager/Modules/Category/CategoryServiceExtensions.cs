using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Category.Services;

namespace D3dxSkinManager.Modules.Category;

/// <summary>
/// Service registration extensions for Category module
/// Registers category management services and facade (profile-scoped)
/// </summary>
public static class CategoryServiceExtensions
{
    /// <summary>
    /// Register Category module services and facade (profile-scoped)
    /// These services are instantiated per profile and use ProfileContext
    /// </summary>
    public static IServiceCollection AddCategoryServices(this IServiceCollection services)
    {
        Console.WriteLine("[CategoryFacade] Registering Category services (profile-scoped)...");

        // Register data layer (repository) - using profile-specific paths
        services.TryAddSingleton<ICategoryRepository, CategoryRepository>();

        // Register domain service
        services.TryAddSingleton<ICategoryService, CategoryService>();

        // Register facade
        services.TryAddSingleton<ICategoryFacade, CategoryFacade>();

        // Register event handler (subscribes to mod category changes)
        services.TryAddSingleton<ICategoryEventHandler, CategoryEventHandler>();

        Console.WriteLine("[CategoryFacade] Category services registered");
        return services;
    }

    /// <summary>
    /// Ensures CategoryEventHandler is eagerly instantiated when profile scope is created
    /// Call this after the service provider is built for the profile
    /// </summary>
    public static void InitializeCategoryEventHandler(IServiceProvider serviceProvider)
    {
        // Eagerly resolve the event handler to ensure it subscribes to events
        var handler = serviceProvider.GetRequiredService<ICategoryEventHandler>();
        Console.WriteLine("[CategoryFacade] CategoryEventHandler initialized and listening for events");
    }
}
