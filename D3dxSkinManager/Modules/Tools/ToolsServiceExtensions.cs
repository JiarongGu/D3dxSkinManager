using System.IO;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Tools;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Tools;

/// <summary>
/// Service registration extensions for Tools module
/// Registers cache, classification, validation services and facade
/// </summary>
public static class ToolsServiceExtensions
{
    /// <summary>
    /// Register Tools module services and facade
    /// </summary>
    public static IServiceCollection AddToolsServices(this IServiceCollection services, string dataPath)
    {
        // Register configuration service (required by validation and D3DMigoto) - using profile paths
        services.AddSingleton<IConfigurationService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            return new ConfigurationService(profileDataPath);
        });

        // Register mod auto-detection service
        services.AddSingleton<IModAutoDetectionService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            var autoDetectionService = new ModAutoDetectionService();
            var rulesPath = Path.Combine(profileDataPath, "auto_detection_rules.json");
            autoDetectionService.LoadRulesAsync(rulesPath).Wait();
            return autoDetectionService;
        });

        // Register cache service
        services.AddSingleton<ICacheService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            var repository = sp.GetRequiredService<IModRepository>();
            return new CacheService(profileDataPath, repository);
        });

        // Register validation service
        services.AddSingleton<IStartupValidationService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? Path.Combine(dataPath, "profiles", "default");
            var configService = sp.GetRequiredService<IConfigurationService>();
            return new StartupValidationService(profileDataPath, configService);
        });

        // Register facade
        services.AddSingleton<IToolsFacade, ToolsFacade>();

        return services;
    }
}
