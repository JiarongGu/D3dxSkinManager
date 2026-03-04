using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;

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

        // Global path service for application-level paths
        AddSingleton<IGlobalPathService, GlobalPathService>(services);

        // Path helper for relative path conversion (ensures portability)
        AddSingleton<IPathHelper, PathHelper>(services);

        // File transfer service for managed file copying with deduplication
        AddSingleton<IFileTransferService, FileTransferService>(services);

        // Path validator for centralized file/directory validation
        AddSingleton<IPathValidator, PathValidator>(services);

        // Payload helper for message parsing (testable DI version)
        AddSingleton<IPayloadHelper, PayloadHelper>(services);

        // Log helper for centralized logging with AppEnvironment and GlobalPathService
        AddSingleton<ILogHelper, LogHelper>(services);

        // Event emitter helper for null-safe plugin event emission
        AddSingleton<IEventEmitter, EventEmitter>(services);

        // Custom scheme handler for app:// URLs (image serving)
        // Uses dedicated path cache configured above
        AddSingleton<ICustomSchemeHandler, CustomSchemeHandler>(services);

        // Event bus for event messaging between services
        AddSingleton<IEventBus, EventBus>(services);
        AddSingleton<IEventEmitter, EventEmitter>(services);

        // Message dispatcher (singleton shared across all sessions)
        // - Routes IPC messages from WebView to module facades via middleware pipeline
        // - Allows plugins/services to send messages programmatically via IMessageDispatcher
        AddSingleton<MessageDispatcher, MessageDispatcher>(services);
        services.AddSingleton<IMessageDispatcher>(sp => sp.GetRequiredService<MessageDispatcher>());

        // Performance monitor for tracking application performance
        AddSingleton<IPerformanceMonitor, PerformanceMonitor>(services);

        // Eager loading service for startup optimizations
        AddSingleton<IEagerLoadingService, EagerLoadingService>(services);

        return services;
    }

    public static IServiceCollection AddCoreServices(this IServiceCollection services, IServiceProvider serviceProvider)
    {
        services.AddSingleton(serviceProvider.GetRequiredService<IMemoryCache>());
        services.AddSingleton(serviceProvider.GetRequiredService<IMessageDispatcher>());

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

    public static IServiceCollection AddSingleton<TService, TImplementation>(IServiceCollection services)
    {
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        _registerdServices.Add(typeof(TService));
        return services;
    }
}
