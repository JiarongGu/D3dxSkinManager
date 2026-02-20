using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core;

/// <summary>
/// Service registration extensions for Core module
/// Registers shared/common services used across all modules
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Register Core module services
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, string dataPath)
    {
        // Low-level services (no dependencies)
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IArchiveService, ArchiveService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        // Global path service for application-level paths
        services.AddSingleton<IGlobalPathService>(sp => new GlobalPathService(dataPath));

        // Path helper for relative path conversion (ensures portability)
        services.AddSingleton<IPathHelper>(sp => new PathHelper(dataPath));

        // Path validator for centralized file/directory validation
        services.AddSingleton<IPathValidator, PathValidator>();

        // Payload helper for message parsing (testable DI version)
        services.AddSingleton<IPayloadHelper, PayloadHelper>();

        // Log helper for centralized logging
        services.AddSingleton<ILogHelper, LogHelper>();

        // Event emitter helper for null-safe plugin event emission
        services.AddSingleton<IEventEmitterHelper, EventEmitterHelper>();

        // Image service for image processing (thumbnails, resizing, etc.)
        services.AddSingleton<IImageService, ImageService>();

        // Custom scheme handler for app:// URLs (image serving)
        services.AddSingleton<ICustomSchemeHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogHelper>();
            return new CustomSchemeHandler(dataPath, logger);
        });

        return services;
    }
}
