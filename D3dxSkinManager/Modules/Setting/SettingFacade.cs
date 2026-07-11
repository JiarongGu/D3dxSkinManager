using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Setting.Models;
using D3dxSkinManager.Modules.Setting.Services;

namespace D3dxSkinManager.Modules.Setting;


/// <summary>
/// Interface for Settings facade
/// Module: SETTING
/// Handles: GET_GLOBAL, UPDATE_GLOBAL, GET_FILE, SAVE_FILE, etc.
/// </summary>
public interface ISettingFacade : IModuleFacade
{
    // Global Settings
    Task<GlobalSettings> GetGlobalSettingsAsync();
    Task UpdateGlobalSettingsAsync(GlobalSettings settings);
    Task UpdateGlobalSettingAsync(string key, string value);
    Task ResetGlobalSettingsAsync();
}


/// <summary>
/// Facade for settings operations
/// Module: SETTING
/// Responsibility: Global settings and settings file management
/// </summary>
public class SettingFacade : BaseFacade, ISettingFacade
{
    protected override string ModuleName => "SettingsFacade";

    private readonly IGlobalSettingService _globalSettingsService;
    private readonly ISettingFileService _settingsFileService;
    private readonly ILanguageService _languageService;
    private readonly IWindowStateService _windowStateService;
    private readonly IEventBus _eventBus;
    private readonly IPayloadHelper _payloadHelper;

    public SettingFacade(
        IGlobalSettingService globalSettingsService,
        ISettingFileService settingsFileService,
        ILanguageService languageService,
        IWindowStateService windowStateService,
        IEventBus eventBus,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _globalSettingsService = globalSettingsService ?? throw new ArgumentNullException(nameof(globalSettingsService));
        _settingsFileService = settingsFileService ?? throw new ArgumentNullException(nameof(settingsFileService));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // Global settings
            "GET_GLOBAL" => await GetGlobalSettingsHandlerAsync(request),
            "UPDATE_GLOBAL" => await UpdateGlobalSettingsHandlerAsync(request),
            "UPDATE_FIELD" => await UpdateGlobalSettingHandlerAsync(request),
            "RESET_GLOBAL" => await ResetGlobalSettingsHandlerAsync(request),

            // Settings files
            "GET_FILE" => await GetSettingsFileHandlerAsync(request),
            "SAVE_FILE" => await SaveSettingsFileHandlerAsync(request),
            "DELETE_FILE" => await DeleteSettingsFileHandlerAsync(request),
            "FILE_EXISTS" => await SettingsFileExistsHandlerAsync(request),
            "LIST_FILES" => await ListSettingsFilesHandlerAsync(request),

            // Language/i18n
            "GET_LANGUAGE" => await GetLanguageHandlerAsync(request),

            // Window state
            "RESET_WINDOW_STATE" => await ResetWindowStateHandlerAsync(request),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<GlobalSettings> GetGlobalSettingsAsync()
    {
        return await _globalSettingsService.GetSettingsAsync().ConfigureAwait(false);
    }

    public async Task UpdateGlobalSettingsAsync(GlobalSettings settings)
    {
        await _globalSettingsService.UpdateSettingsAsync(settings).ConfigureAwait(false);
    }

    public async Task UpdateGlobalSettingAsync(string key, string value)
    {
        await _globalSettingsService.UpdateSettingAsync(key, value).ConfigureAwait(false);
    }

    public async Task ResetGlobalSettingsAsync()
    {
        await _globalSettingsService.ResetSettingsAsync().ConfigureAwait(false);
    }

    // IPC Message Handlers

    private async Task<GlobalSettings> GetGlobalSettingsHandlerAsync(IpcRequest request)
    {
        _logger.Debug("GetGlobalSettingsHandlerAsync called", "SettingsFacade");
        var result = await GetGlobalSettingsAsync().ConfigureAwait(false);
        _logger.Debug($"Settings retrieved: Theme={result.Theme}, LogLevel={result.LogLevel}", "SettingsFacade");
        return result;
    }

    private async Task<object> UpdateGlobalSettingsHandlerAsync(IpcRequest request)
    {
        var theme = _payloadHelper.GetOptionalValue<string>(request.Payload, "theme");
        var annotationLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "annotationLevel");
        var logLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "logLevel");

        var settings = await GetGlobalSettingsAsync().ConfigureAwait(false);

        if (theme != null) settings.Theme = theme;
        if (annotationLevel != null) settings.AnnotationLevel = annotationLevel;
        if (logLevel != null) settings.LogLevel = logLevel;

        await UpdateGlobalSettingsAsync(settings).ConfigureAwait(false);

        return new { success = true, message = "Global settings updated", settings };
    }

    private async Task<object> UpdateGlobalSettingHandlerAsync(IpcRequest request)
    {
        var key = _payloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var value = _payloadHelper.GetRequiredValue<string>(request.Payload, "value");

        await UpdateGlobalSettingAsync(key, value).ConfigureAwait(false);

        return new { success = true, message = $"Setting '{key}' updated to '{value}'" };
    }

    private async Task<object> ResetGlobalSettingsHandlerAsync(IpcRequest request)
    {
        await ResetGlobalSettingsAsync().ConfigureAwait(false);
        var settings = await GetGlobalSettingsAsync().ConfigureAwait(false);

        return new { success = true, message = "Global settings reset to defaults", settings };
    }

    // Settings File Handlers

    private async Task<object> GetSettingsFileHandlerAsync(IpcRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = await _settingsFileService.GetSettingsFileAsync(filename).ConfigureAwait(false);

        if (content == null)
        {
            return new { success = false, message = $"Settings file not found: {filename}", content = (string?)null };
        }

        return new { success = true, content };
    }

    private async Task<object> SaveSettingsFileHandlerAsync(IpcRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = _payloadHelper.GetRequiredValue<string>(request.Payload, "content");

        await _settingsFileService.SaveSettingsFileAsync(filename, content).ConfigureAwait(false);

        return new { success = true, message = $"Settings file saved: {filename}" };
    }

    private async Task<object> DeleteSettingsFileHandlerAsync(IpcRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");

        await _settingsFileService.DeleteSettingsFileAsync(filename).ConfigureAwait(false);

        return new { success = true, message = $"Settings file deleted: {filename}" };
    }

    private async Task<object> SettingsFileExistsHandlerAsync(IpcRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var exists = await _settingsFileService.SettingsFileExistsAsync(filename).ConfigureAwait(false);

        return new { exists };
    }

    private async Task<object> ListSettingsFilesHandlerAsync(IpcRequest request)
    {
        var files = await _settingsFileService.ListSettingsFilesAsync().ConfigureAwait(false);

        return new { files };
    }

    // Language/i18n Handlers

    private async Task<object> GetLanguageHandlerAsync(IpcRequest request)
    {
        var languageCode = _payloadHelper.GetRequiredValue<string>(request.Payload, "languageCode");
        var language = await _languageService.GetLanguageAsync(languageCode).ConfigureAwait(false);

        if (language == null)
        {
            return new { success = false, message = $"Language not found: {languageCode}", language = (LanguageSettings?)null };
        }

        return new { success = true, language };
    }

    // Window State Handlers

    private async Task<object> ResetWindowStateHandlerAsync(IpcRequest request)
    {
        _logger.Info("Resetting window state to defaults", "SettingsFacade");

        var settings = await _globalSettingsService.GetSettingsAsync().ConfigureAwait(false);

        // Reset window settings to null/defaults
        settings.Window.X = null;
        settings.Window.Y = null;
        settings.Window.Width = null;
        settings.Window.Height = null;
        settings.Window.Maximized = false;

        await _globalSettingsService.UpdateSettingsAsync(settings).ConfigureAwait(false);

        // Load the default window state values to send in event
        var (width, height, x, y, maximized) = await _windowStateService.LoadWindowStateAsync().ConfigureAwait(false);

        // Emit event for ApplicationHost to handle window state reset
        await _eventBus.EmitAsync(ModuleNames.SETTING, SettingEvents.WINDOW_STATE_RESET, new
        {
            Width = width,
            Height = height,
            Maximized = false  // Always reset to non-maximized state
        });

        _logger.Info("Window state reset event emitted successfully", "SettingsFacade");

        return new { success = true, message = "Window state reset to defaults and applied immediately" };
    }

}
