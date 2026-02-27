using D3dxSkinManager.Infrastructure;
using D3dxSkinManager.Infrastructure.WebView;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Setting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3dxSkinManager.Modules.Setting;

/// <summary>
/// Service registration extensions for Settings module
/// Registers settings and file dialog services and facade
/// </summary>
public static class SettingServiceExtensions
{
    /// <summary>
    /// Register Settings module services and facade
    /// </summary>
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        Console.WriteLine("[SettingsFacade] Registering Settings services...");

        // Register the underlying services (using TryAdd to avoid duplicates)
        services.TryAddSingleton<ISettingFileService, SettingFileService>();
        services.TryAddSingleton<ILanguageService, LanguageService>();
        services.TryAddSingleton<IWindowStateService, WindowStateService>();
        services.TryAddSingleton<IGlobalSettingService, GlobalSettingService>();

        // Register the facade itself
        services.TryAddSingleton<ISettingFacade, SettingFacade>();

        Console.WriteLine("[SettingsFacade] Settings services registered");
        return services;
    }

    /// <summary>
    /// Register SettingsFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseSettingsFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<ISettingFacade>();
        if (facade == null)
        {
            Console.WriteLine("[SettingsFacade] Warning: SettingsFacade not registered in service container");
            return dispatcher;
        }

        Console.WriteLine($"[SettingsFacade] Registering {ModuleNames.SETTING} module handlers");

        // Register the module handler
        dispatcher.UseModule(ModuleNames.SETTING, facade.HandleMessageAsync);

        return dispatcher;
    }
}
