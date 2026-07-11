using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Cleanup;
using D3dxSkinManager.Modules.Core.Cleanup.Steps;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.WebView;

namespace D3dxSkinManager.Modules.Core;

/// <summary>
/// Service registration extensions for Core module
/// Registers shared/common services used across all modules
/// </summary>
public static class CoreServiceExtensions
{
    private static readonly List<Type> _registerdServices = new List<Type>();

    /// <summary>
    /// Register Core module services
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Memory cache for application-wide caching (settings, category tree, etc.)
        // No size limit for general application cache
        // Note: AddMemoryCache() registers IMemoryCache as a singleton automatically
        services.AddMemoryCache();

        // Dedicated path cache for CustomSchemeHandler with LRU eviction
        // Size-limited to 500 entries to prevent unbounded memory growth from file path caching
        AddSingleton<PathCache, PathCache>(services);

        // Low-level services (no dependencies)
        AddSingleton<IFileHelper, FileHelper>(services);
        AddSingleton<IHashHelper, HashHelper>(services);
        AddSingleton<IArchiveHelper, ArchiveHelper>(services);
        AddSingleton<IImageHelper, ImageHelper>(services);

        // File system seam — lets the file-operation pipeline run against an in-memory fake in tests
        AddSingleton<IFileSystem, SystemFileSystem>(services);

        // Global path service for application-level paths
        AddSingleton<IGlobalPathService, GlobalPathService>(services);

        // Path helper for relative path conversion (ensures portability)
        AddSingleton<IPathHelper, PathHelper>(services);

        // Reusable HTTP download service (streamed file/string fetch with progress + sha256).
        AddSingleton<IDownloadService, DownloadService>(services);

        // Startup self-cleanup — the CENTRAL app-level cleanup/migration pipeline: the runner
        // executes every registered IStartupCleanupStep in registration order (each isolated +
        // non-fatal). Add new startup sweeps/legacy-file migrations as steps HERE, never as
        // bootstrap one-offs.
        AddSingleton<IStartupCleanupService, StartupCleanupService>(services);
        services.AddSingleton<IStartupCleanupStep, ManagedDownloadsCleanupStep>();
        services.AddSingleton<IStartupCleanupStep, OrphanedUpdateStagingCleanupStep>();
        services.AddSingleton<IStartupCleanupStep, LegacyProcessStateCleanupStep>();
        services.AddSingleton<IStartupCleanupStep, LegacyRemoteIndexCacheCleanupStep>();
        // NOTE: the orphaned pre-migration launcher (D3dxSkinManager Launcher.exe) is swept by the C++
        // launcher itself on boot (RemoveLegacyLauncher in main.cpp) — it runs first + before the app, so
        // no app-level cleanup step is needed. See .claude/knowledge/launcher-topology.md.

        // Path validator for centralized file/directory validation
        AddSingleton<IPathValidator, PathValidator>(services);

        // Payload helper for message parsing (testable DI version)
        AddSingleton<IPayloadHelper, PayloadHelper>(services);

        // Log helper for centralized logging with AppEnvironment and GlobalPathService
        AddSingleton<ILogHelper, LogHelper>(services);

        // Event emitter helper for null-safe plugin event emission
        AddSingleton<IEventEmitter, EventEmitter>(services);

        // Global on-demand remote-image cache behind the app://remote-image/ proxy URLs.
        AddSingleton<IRemoteImageProxy, RemoteImageProxy>(services);

        // Content veil: pure-CPU sensitivity heuristic for preview images (skin-tone analysis;
        // verdicts cached per session). The UI blurs flagged previews when the toggle is on.
        // The standalone analyzer composes the verification STYLES (ordered IContentVerifier set —
        // both registrations survive for IEnumerable injection); the service is orchestration only.
        AddSingleton<IContentVerifier, PointAnatomyVerifier>(services);
        AddSingleton<IContentVerifier, ChestBandZoomVerifier>(services);
        AddSingleton<IContentVeilAnalyzer, ContentVeilAnalyzer>(services);
        AddSingleton<IContentVeilService, ContentVeilService>(services);

        // Custom scheme handler for app:// URLs (image serving)
        // Uses dedicated path cache configured above
        AddSingleton<ICustomSchemeHandler, CustomSchemeHandler>(services);

        // Event bus for event messaging between services
        AddSingleton<IEventBus, EventBus>(services);
        AddSingleton<IEventEmitter, EventEmitter>(services);

        // Authoritative registry of long-running processes (status bar + Activity panel).
        // App-level singleton; emits consolidated PROCESS_LIST_UPDATED via the global event bus.
        AddSingleton<IProcessRegistry, ProcessRegistry>(services);

        // Message dispatcher (singleton shared across all sessions)
        // - Routes IPC messages from WebView to module facades via middleware pipeline
        // - Allows plugins/services to send messages programmatically via IMessageDispatcher
        AddSingleton<MessageDispatcher, MessageDispatcher>(services);
        services.AddSingleton<IMessageDispatcher>(sp => sp.GetRequiredService<MessageDispatcher>());

        // Performance monitor for tracking application performance
        AddSingleton<IPerformanceMonitor, PerformanceMonitor>(services);

        // Eager loading service for startup optimizations
        AddSingleton<IEagerLoadingService, EagerLoadingService>(services);

        // WebView infrastructure services (shared with profile-scoped services)
        AddSingleton<IWebViewSessionManager, WebViewSessionManager>(services);

        AddSingleton<IFormInteractionService, FormInteractionService>(services);

        return services;
    }

    public static IServiceCollection AddCoreServices(this IServiceCollection services, IServiceProvider serviceProvider)
    {
        services.AddSingleton(serviceProvider.GetRequiredService<IMemoryCache>());
        services.AddSingleton(serviceProvider.GetRequiredService<IMessageDispatcher>());
        services.AddSingleton(serviceProvider.GetRequiredService<IEmbeddedResourceProvider>());

        foreach (var serviceType in _registerdServices)
        {
            var service = serviceProvider.GetService(serviceType);
            if (service != null)
            {
                services.AddSingleton(serviceType, service);
            }
        }
        return services;
    }

    private static IServiceCollection AddSingleton<TService, TImplementation>(IServiceCollection services)
    {
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        _registerdServices.Add(typeof(TService));
        return services;
    }
}
