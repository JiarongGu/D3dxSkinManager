using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using D3dxSkinManager.Modules.Tool.ModPackage.Services;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Tool;

/// <summary>
/// Service registration extensions for Tools module
/// Registers cache, Category, validation services and facade
/// </summary>
public static class ToolServiceExtensions
{
    /// <summary>
    /// Register Tools module services and facade (profile-scoped)
    /// </summary>
    public static IServiceCollection AddToolsServices(this IServiceCollection services)
    {
        Console.WriteLine("[ToolsFacade] Registering Tools services (profile-scoped)...");

        // Register configuration service (required by validation and D3DMigoto) - using profile paths
        services.TryAddSingleton<IConfigurationService, ConfigurationService>();

        // Register validation service
        services.TryAddSingleton<IStartupValidationService, StartupValidationService>();

        // Register screen capture services
        services.TryAddSingleton<IScreenCaptureProfileRepository, ScreenCaptureProfileRepository>();
        services.TryAddSingleton<IScreenCaptureService, ScreenCaptureService>();

        // Register mod package service
        services.TryAddSingleton<IModPackageService, ModPackageService>();

        // Register file cleanup service
        services.TryAddSingleton<IFileCleanupService, FileCleanupService>();

        // Register mod analysis services
        services.TryAddSingleton<IModAnalysisRepository, ModAnalysisRepository>();
        services.TryAddSingleton<IModAnalysisService, ModAnalysisService>();

        // Register mod ID migration service
        services.TryAddSingleton<IModIdMigrationService, ModIdMigrationService>();

        // Register facade
        services.TryAddSingleton<IToolFacade, ToolFacade>();
        services.TryAddSingleton<ToolFacade>();

        Console.WriteLine("[ToolsFacade] Tools services registered");
        return services;
    }
}
