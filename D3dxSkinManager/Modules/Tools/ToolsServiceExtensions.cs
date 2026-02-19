using System.IO;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Tools.Services;

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
    public static IServiceCollection AddToolsServices(this IServiceCollection services)
    {
        // Register configuration service (required by validation and D3DMigoto) - using profile paths
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Register mod auto-detection service
        services.AddSingleton<IModAutoDetectionService, ModAutoDetectionService>();

        // Cache service is now part of ModFileService in Mods module

        // Register validation service
        services.AddSingleton<IStartupValidationService, StartupValidationService>();

        // Register facade
        services.AddSingleton<IToolsFacade, ToolsFacade>();

        return services;
    }
}
