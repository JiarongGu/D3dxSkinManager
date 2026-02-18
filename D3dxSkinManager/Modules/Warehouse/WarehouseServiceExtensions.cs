using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Warehouse;

namespace D3dxSkinManager.Modules.Warehouse;

/// <summary>
/// Service registration extensions for Warehouse module
/// Registers mod warehouse services and facade (future implementation)
/// </summary>
public static class WarehouseServiceExtensions
{
    /// <summary>
    /// Register Warehouse module services and facade
    /// </summary>
    public static IServiceCollection AddWarehouseServices(this IServiceCollection services)
    {
        // Register facade (placeholder for future implementation)
        services.AddSingleton<IWarehouseFacade, WarehouseFacade>();

        return services;
    }
}
