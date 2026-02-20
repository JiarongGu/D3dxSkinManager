using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Settings.Models;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Modules.Settings;


/// <summary>
/// Interface for Settings facade
/// Handles: SETTINGS_GET, SETTINGS_UPDATE, settings file operations
/// Prefix: SETTINGS_*
/// </summary>
public interface ISettingsFacade : IModuleFacade
{
    // Global Settings
    Task<GlobalSettings> GetGlobalSettingsAsync();
    Task UpdateGlobalSettingsAsync(GlobalSettings settings);
    Task UpdateGlobalSettingAsync(string key, string value);
    Task ResetGlobalSettingsAsync();
}


/// <summary>
/// Facade for settings operations
/// Responsibility: Global settings and settings file management
/// IPC Prefix: SETTINGS_*
/// </summary>
public class SettingsFacade : BaseFacade, ISettingsFacade
{
    protected override string ModuleName => "SettingsFacade";

    private readonly IGlobalSettingsService _globalSettingsService;
    private readonly ISettingsFileService _settingsFileService;
    private readonly ILanguageService _languageService;
    private readonly IPayloadHelper _payloadHelper;

    public SettingsFacade(
        IGlobalSettingsService globalSettingsService,
        ISettingsFileService settingsFileService,
        ILanguageService languageService,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _globalSettingsService = globalSettingsService ?? throw new ArgumentNullException(nameof(globalSettingsService));
        _settingsFileService = settingsFileService ?? throw new ArgumentNullException(nameof(settingsFileService));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    protected override async Task<object?> RouteMessageAsync(MessageRequest request)
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
            "GET_AVAILABLE_LANGUAGES" => await GetAvailableLanguagesHandlerAsync(request),
            "LANGUAGE_EXISTS" => await LanguageExistsHandlerAsync(request),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<GlobalSettings> GetGlobalSettingsAsync()
    {
        return await _globalSettingsService.GetSettingsAsync();
    }

    public async Task UpdateGlobalSettingsAsync(GlobalSettings settings)
    {
        await _globalSettingsService.UpdateSettingsAsync(settings);
    }

    public async Task UpdateGlobalSettingAsync(string key, string value)
    {
        await _globalSettingsService.UpdateSettingAsync(key, value);
    }

    public async Task ResetGlobalSettingsAsync()
    {
        await _globalSettingsService.ResetSettingsAsync();
    }

    // IPC Message Handlers

    private async Task<GlobalSettings> GetGlobalSettingsHandlerAsync(MessageRequest request)
    {
        _logger.Debug("GetGlobalSettingsHandlerAsync called", "SettingsFacade");
        var result = await GetGlobalSettingsAsync();
        _logger.Debug($"Settings retrieved: Theme={result.Theme}, LogLevel={result.LogLevel}", "SettingsFacade");
        return result;
    }

    private async Task<object> UpdateGlobalSettingsHandlerAsync(MessageRequest request)
    {
        var theme = _payloadHelper.GetOptionalValue<string>(request.Payload, "theme");
        var annotationLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "annotationLevel");
        var logLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "logLevel");

        var settings = await GetGlobalSettingsAsync();

        if (theme != null) settings.Theme = theme;
        if (annotationLevel != null) settings.AnnotationLevel = annotationLevel;
        if (logLevel != null) settings.LogLevel = logLevel;

        await UpdateGlobalSettingsAsync(settings);

        return new { success = true, message = "Global settings updated", settings };
    }

    private async Task<object> UpdateGlobalSettingHandlerAsync(MessageRequest request)
    {
        var key = _payloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var value = _payloadHelper.GetRequiredValue<string>(request.Payload, "value");

        await UpdateGlobalSettingAsync(key, value);

        return new { success = true, message = $"Setting '{key}' updated to '{value}'" };
    }

    private async Task<object> ResetGlobalSettingsHandlerAsync(MessageRequest request)
    {
        await ResetGlobalSettingsAsync();
        var settings = await GetGlobalSettingsAsync();

        return new { success = true, message = "Global settings reset to defaults", settings };
    }

    // Settings File Handlers

    private async Task<object> GetSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = await _settingsFileService.GetSettingsFileAsync(filename);

        if (content == null)
        {
            return new { success = false, message = $"Settings file not found: {filename}", content = (string?)null };
        }

        return new { success = true, content };
    }

    private async Task<object> SaveSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = _payloadHelper.GetRequiredValue<string>(request.Payload, "content");

        await _settingsFileService.SaveSettingsFileAsync(filename, content);

        return new { success = true, message = $"Settings file saved: {filename}" };
    }

    private async Task<object> DeleteSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");

        await _settingsFileService.DeleteSettingsFileAsync(filename);

        return new { success = true, message = $"Settings file deleted: {filename}" };
    }

    private async Task<object> SettingsFileExistsHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var exists = await _settingsFileService.SettingsFileExistsAsync(filename);

        return new { exists };
    }

    private async Task<object> ListSettingsFilesHandlerAsync(MessageRequest request)
    {
        var files = await _settingsFileService.ListSettingsFilesAsync();

        return new { files };
    }

    // Language/i18n Handlers

    private async Task<object> GetLanguageHandlerAsync(MessageRequest request)
    {
        var languageCode = _payloadHelper.GetRequiredValue<string>(request.Payload, "languageCode");
        var language = await _languageService.GetLanguageAsync(languageCode);

        if (language == null)
        {
            return new { success = false, message = $"Language not found: {languageCode}", language = (LanguageSettings?)null };
        }

        return new { success = true, language };
    }

    private async Task<object> GetAvailableLanguagesHandlerAsync(MessageRequest request)
    {
        var languages = await _languageService.GetAvailableLanguagesAsync();
        return new { success = true, languages };
    }

    private async Task<object> LanguageExistsHandlerAsync(MessageRequest request)
    {
        var languageCode = _payloadHelper.GetRequiredValue<string>(request.Payload, "languageCode");
        var exists = await _languageService.LanguageExistsAsync(languageCode);

        return new { exists };
    }
}
