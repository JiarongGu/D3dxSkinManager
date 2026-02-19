using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Migration.Services;

namespace D3dxSkinManager.Modules.Migration;

/// <summary>
/// Service registration extensions for Migration module
/// Registers Python to React migration services and facade
/// </summary>
public static class MigrationServiceExtensions
{
    /// <summary>
    /// Register Migration module services and facade
    /// </summary>
    public static IServiceCollection AddMigrationServices(this IServiceCollection services)
    {
        // Register thumbnail service
        services.AddSingleton<IClassificationThumbnailService, ClassificationThumbnailService>();

        // Register migration service
        services.AddSingleton<IMigrationService, MigrationService>();

        // Register facade
        services.AddSingleton<IMigrationFacade, MigrationFacade>();

        return services;
    }
}
