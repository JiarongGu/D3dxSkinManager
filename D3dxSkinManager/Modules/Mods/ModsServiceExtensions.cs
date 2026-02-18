using System.IO;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Core.Services;

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
    public static IServiceCollection AddModsServices(this IServiceCollection services, string dataPath)
    {
        // Register data layer (repositories) - using profile-specific paths
        services.AddSingleton<IModRepository>(sp =>
        {
            // Try to get ProfileContext, fall back to base dataPath if not available
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            return new ModRepository(profileDataPath);
        });

        services.AddSingleton<IClassificationRepository>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            return new ClassificationRepository(profileDataPath);
        });

        // Register domain services
        services.AddSingleton<IModArchiveService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            var fileService = sp.GetRequiredService<IFileService>();
            return new ModArchiveService(profileDataPath, fileService);
        });

        // Register centralized mod management service
        services.AddSingleton<IModManagementService, ModManagementService>();

        services.AddSingleton<IModImportService, ModImportService>();
        services.AddSingleton<IModQueryService, ModQueryService>();

        // Register classification service
        services.AddSingleton<IClassificationService>(sp =>
        {
            var repository = sp.GetRequiredService<IClassificationRepository>();
            var imageServer = sp.GetRequiredService<Core.Services.ImageServerService>();
            return new ClassificationService(repository, imageServer);
        });

        // Register facade
        services.AddSingleton<IModFacade, ModFacade>();

        return services;
    }
}
