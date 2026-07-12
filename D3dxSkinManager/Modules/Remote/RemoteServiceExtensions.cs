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
        services.TryAddSingleton<IRemoteLibraryStore, RemoteLibraryStore>();
        // PER-PROFILE tag labels/aliases (were global on the source config → leaked across profiles).
        services.TryAddSingleton<IRemoteTagLabelStore, RemoteTagLabelStore>();
        // Fetch TRANSPORTS + the config-driven router. HttpPageFetcher stays the default single
        // IRemotePageFetcher (download-host resolvers inject it directly); engines fetch via the router,
        // which picks http vs the off-screen WebView2 transport by each source's `fetcher` field.
        services.TryAddSingleton<HttpPageFetcher>();
        services.TryAddSingleton<WebView2PageFetcher>();
        services.TryAddSingleton<IRemotePageFetcher>(sp => sp.GetRequiredService<HttpPageFetcher>());
        services.TryAddSingleton<IRemotePageFetcherRouter, RemotePageFetcherRouter>();
        // Site engines (remote-library-redesign.md) — one per site family; a source config's `engine`
        // field names which one handles it. Adding a site = adding an engine registration here.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRemoteSiteEngine, HttpRegexEngine>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRemoteSiteEngine, GameBananaEngine>());
        services.TryAddSingleton<IRemoteBrowseService, RemoteBrowseService>();
        services.TryAddSingleton<ICloudreveShareResolver, CloudreveShareResolver>();
        // Online-storage accounts (auth'd download hosts) + the Quark share resolver that uses them.
        services.TryAddSingleton<IOnlineAccountStore, OnlineAccountStore>();
        services.TryAddSingleton<IQuarkShareResolver, QuarkShareResolver>();
        services.TryAddSingleton<IExternalLoginService, ExternalLoginService>();
        services.TryAddSingleton<IRemoteIndexRepository, RemoteIndexRepository>();
        services.TryAddSingleton<IRemoteIndexService, RemoteIndexService>();
        services.TryAddSingleton<IRemoteImportService, RemoteImportService>();

        services.TryAddSingleton<IRemoteFacade, RemoteFacade>();
        services.TryAddSingleton<RemoteFacade>();

        Console.WriteLine("[RemoteFacade] Remote services registered");
        return services;
    }
}
