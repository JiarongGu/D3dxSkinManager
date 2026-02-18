using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Game;

namespace D3dxSkinManager.Modules.Game;

/// <summary>
/// Service registration extensions for Game module
/// Registers game launch services and facade
/// </summary>
public static class GameServiceExtensions
{
    /// <summary>
    /// Register Game module services and facade
    /// </summary>
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        // Register facade (depends on Core.ProcessService and Profiles.ProfileService)
        services.AddSingleton<IGameFacade, GameFacade>();

        return services;
    }
}
