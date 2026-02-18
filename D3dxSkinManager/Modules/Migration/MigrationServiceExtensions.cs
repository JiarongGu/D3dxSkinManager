using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Services;

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
    public static IServiceCollection AddMigrationServices(this IServiceCollection services, string dataPath)
    {
        // Register thumbnail service
        services.AddSingleton<IClassificationThumbnailService>(sp =>
        {
            var classificationRepository = sp.GetRequiredService<IClassificationRepository>();
            return new ClassificationThumbnailService(classificationRepository);
        });

        // Register migration service
        services.AddSingleton<IMigrationService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            var repository = sp.GetRequiredService<IModRepository>();
            var classificationRepository = sp.GetRequiredService<IClassificationRepository>();
            var thumbnailService = sp.GetRequiredService<IClassificationThumbnailService>();
            var fileService = sp.GetRequiredService<IFileService>();
            var imageService = sp.GetRequiredService<IImageService>();
            var configService = sp.GetRequiredService<IConfigurationService>();
            var modManagementService = sp.GetRequiredService<IModManagementService>();
            return new MigrationService(profileDataPath, repository, classificationRepository, thumbnailService, fileService, imageService, configService, modManagementService);
        });

        // Register facade
        services.AddSingleton<IMigrationFacade, MigrationFacade>();

        return services;
    }
}
