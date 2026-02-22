using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Modules.Tools;

/// <summary>
/// Service registration extensions for Tools module
/// Registers cache, classification, validation services and facade
/// </summary>
public static class ToolsServiceExtensions
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

        // Register facade
        services.TryAddSingleton<IToolsFacade, ToolsFacade>();
        services.TryAddSingleton<ToolsFacade>();

        Console.WriteLine("[ToolsFacade] Tools services registered");
        return services;
    }
}
