using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Remote.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3dxSkinManager.Modules.Remote;

/// <summary>
/// Service registration extensions for the Remote module (remote mod library:
/// site adapters + browse + Cloudreve resolve + download/import pipeline).
/// </summary>
public static class RemoteServiceExtensions
{
    public static IServiceCollection AddRemoteServices(this IServiceCollection services)
    {
        Console.WriteLine("[RemoteFacade] Registering Remote services...");

        // Import pipeline reuses the Mod module's import service.
        services.AddModsServices();

        services.TryAddSingleton<IRemoteSourceStore, RemoteSourceStore>();
        services.TryAddSingleton<IRemotePageFetcher, HttpPageFetcher>();
        services.TryAddSingleton<IRemoteBrowseService, RemoteBrowseService>();
        services.TryAddSingleton<ICloudreveShareResolver, CloudreveShareResolver>();
        services.TryAddSingleton<IRemoteImportService, RemoteImportService>();

        services.TryAddSingleton<IRemoteFacade, RemoteFacade>();
        services.TryAddSingleton<RemoteFacade>();

        Console.WriteLine("[RemoteFacade] Remote services registered");
        return services;
    }
}
