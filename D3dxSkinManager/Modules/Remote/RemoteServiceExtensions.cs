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

        // Site adapter configs: JSON files ({data}/remote-sources) are the editable DEFINITION; the
        // per-profile RemoteSources table is the runtime store the app reads from (synced on load).
        services.TryAddSingleton<IRemoteSourceRepository, RemoteSourceRepository>();
        services.TryAddSingleton<IRemoteSourceStore, RemoteSourceStore>();
        // Pure 3-tier config resolver (res ← sparse local ← library params) — remote-library-redesign.md.
        services.TryAddSingleton<IRemoteSourceResolver, RemoteSourceResolver>();
        // PER-PROFILE library config lives in the profile SQLite DB (RemoteLibraries table), not JSON.
        services.TryAddSingleton<IRemoteLibraryRepository, RemoteLibraryRepository>();
        services.TryAddSingleton<IRemoteLibraryStore, RemoteLibraryStore>();
        // PER-PROFILE tag labels/aliases in SQLite (RemoteTagLabels table), not JSON.
        services.TryAddSingleton<IRemoteTagLabelRepository, RemoteTagLabelRepository>();
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
        // MEGA folder-share resolver (anonymous; client-side crypto in MegaCrypto).
        services.TryAddSingleton<IMegaShareResolver, MegaShareResolver>();
        // kodbox share resolver (anonymous; huihui's IP/VPN Hui盘 mirror runs kodbox, not Cloudreve).
        services.TryAddSingleton<IKodboxShareResolver, KodboxShareResolver>();
        // kodbox HOST detector — auto-detect fallback (config-opt-in) for an unmatched Hui盘 mirror.
        services.TryAddSingleton<IKodboxHostDetector, KodboxHostDetector>();
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
