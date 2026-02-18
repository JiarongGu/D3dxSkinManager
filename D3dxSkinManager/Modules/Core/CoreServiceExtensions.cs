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
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Low-level services (no dependencies)
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        return services;
    }

    /// <summary>
    /// Register image service with data path
    /// </summary>
    public static IServiceCollection AddImageService(this IServiceCollection services, string dataPath)
    {
        services.AddSingleton<IImageService>(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? System.IO.Path.Combine(dataPath, "profiles", "default");
            return new ImageService(profileDataPath);
        });
        return services;
    }

    /// <summary>
    /// Register image HTTP server
    /// </summary>
    public static IServiceCollection AddImageServer(this IServiceCollection services, string dataPath)
    {
        services.AddSingleton(sp =>
        {
            var profileContext = sp.GetService<Profiles.Services.IProfileContext>();
            var profileDataPath = profileContext?.GetProfileDataPath() ?? System.IO.Path.Combine(dataPath, "profiles", "default");
            return new ImageServerService(profileDataPath);
        });
        return services;
    }
}
