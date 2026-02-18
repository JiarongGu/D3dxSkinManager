using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Settings;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Modules.Settings;

/// <summary>
/// Service registration extensions for Settings module
/// Registers settings and file dialog services and facade
/// </summary>
public static class SettingsServiceExtensions
{
    /// <summary>
    /// Register Settings module services and facade
    /// </summary>
    public static IServiceCollection AddSettingsServices(this IServiceCollection services, string dataPath)
    {
        // Register Global Settings Service
        services.AddSingleton<IGlobalSettingsService>(sp => new GlobalSettingsService(dataPath));

        // Register Settings File Service (for generic JSON file storage)
        services.AddSingleton<ISettingsFileService>(sp => new SettingsFileService(dataPath));

        // Register facade (depends on Core.FileSystemService, Core.FileDialogService, GlobalSettingsService, and SettingsFileService)
        services.AddSingleton<ISettingsFacade, SettingsFacade>();

        return services;
    }
}
