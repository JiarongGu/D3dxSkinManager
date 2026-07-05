using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using D3dxSkinManager.Modules.Tool.ModPackage.Services;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Tool;

/// <summary>
/// Service registration extensions for Tools module
/// Registers cache, cleanup, analysis, fix and screen-capture services and facade
/// </summary>
public static class ToolServiceExtensions
{
    /// <summary>
    /// Register Tools module services and facade (profile-scoped)
    /// </summary>
    public static IServiceCollection AddToolsServices(this IServiceCollection services)
    {
        Console.WriteLine("[ToolsFacade] Registering Tools services (profile-scoped)...");

        // Register configuration service (required by D3DMigoto and migration) - using profile paths
        services.TryAddSingleton<IConfigurationService, ConfigurationService>();

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

        // Register mod fix (hash-fix script runner) service + its seeded tunables
        services.TryAddSingleton<ModFixOptions>();
        services.TryAddSingleton<IModFixService, ModFixService>();

        // Register the per-profile fix-tool library + its folder watcher
        services.TryAddSingleton<IModFixToolService, ModFixToolService>();
        services.TryAddSingleton<IFixToolsWatcher, FixToolsWatcher>();

        // Analyzer pop-out window (separate WebView2 window, like the capture control panel)
        services.TryAddSingleton<IAnalyzerWindowService, AnalyzerWindowService>();

        // Register facade
        services.TryAddSingleton<IToolFacade, ToolFacade>();
        services.TryAddSingleton<ToolFacade>();

        Console.WriteLine("[ToolsFacade] Tools services registered");
        return services;
    }
}
