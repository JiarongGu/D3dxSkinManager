using D3dxSkinManager.Composition;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public static IServiceCollection AddSettingsServices(this IServiceCollection services)
    {
        Console.WriteLine("[SettingsFacade] Registering Settings services...");

        // Register the underlying services (using TryAdd to avoid duplicates)
        services.TryAddSingleton<ISettingsFileService, SettingsFileService>();
        services.TryAddSingleton<ILanguageService, LanguageService>();
        services.TryAddSingleton<IWindowStateService, WindowStateService>();
        services.TryAddSingleton<IGlobalSettingsService, GlobalSettingsService>();

        // Register the facade itself
        services.TryAddSingleton<ISettingsFacade, SettingsFacade>();

        Console.WriteLine("[SettingsFacade] Settings services registered");
        return services;
    }

    /// <summary>
    /// Register SettingsFacade message handlers with the MessageDispatcher
    /// </summary>
    public static MessageDispatcher UseSettingsFacade(this MessageDispatcher dispatcher, ServiceProvider serviceProvider)
    {
        var facade = serviceProvider.GetService<ISettingsFacade>();
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
