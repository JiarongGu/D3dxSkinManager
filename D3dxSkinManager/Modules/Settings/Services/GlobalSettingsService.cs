using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Settings.Models;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing global application settings
/// Settings are stored in data/global.json
/// </summary>
public interface IGlobalSettingsService
{
    /// <summary>
    /// Get current global settings
    /// </summary>
    Task<GlobalSettings> GetSettingsAsync();

    /// <summary>
    /// Update global settings
    /// </summary>
    Task UpdateSettingsAsync(GlobalSettings settings);

    /// <summary>
    /// Update a single setting field
    /// </summary>
    Task UpdateSettingAsync(string key, string value);

    /// <summary>
    /// Reset settings to default values
    /// </summary>
    Task ResetSettingsAsync();
}

/// <summary>
/// Service for managing global application settings
/// Settings are stored in data/settings/global.json
/// </summary>
public class GlobalSettingsService : IGlobalSettingsService
{
    private readonly string _settingsFilePath;
    private GlobalSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogHelper _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalSettingsService(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var globalPathsService = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));

        // Use GlobalPathService direct property for global settings file
        _settingsFilePath = globalPathsService.GlobalSettingsFilePath;

        // Ensure settings directory exists
        globalPathsService.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Get current global settings
    /// </summary>
    public async Task<GlobalSettings> GetSettingsAsync()
    {
        _logger.Debug($"GetSettingsAsync called", "GlobalSettingsService");
        _logger.Debug($"Settings file path: {_settingsFilePath}", "GlobalSettingsService");

        await _lock.WaitAsync();
        try
        {
            // Return cached if available
            if (_cachedSettings != null)
            {
                _logger.Debug($"Returning cached settings", "GlobalSettingsService");
                return _cachedSettings;
            }

            _logger.Debug($"No cached settings, loading from file...", "GlobalSettingsService");

            // Load from file or create default
            if (File.Exists(_settingsFilePath))
            {
                _logger.Debug($"Settings file exists, reading...", "GlobalSettingsService");
                _cachedSettings = await JsonHelper.DeserializeFromFileAsync<GlobalSettings>(_settingsFilePath)
                                  ?? new GlobalSettings();
                _logger.Info($"Settings loaded from file", "GlobalSettingsService");
            }
            else
            {
                _logger.Info($"Settings file not found, creating default...", "GlobalSettingsService");
                _cachedSettings = new GlobalSettings();
                await SaveSettingsAsync(_cachedSettings);
                _logger.Info($"Default settings created and saved", "GlobalSettingsService");
            }

            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Update global settings
    /// </summary>
    public async Task UpdateSettingsAsync(GlobalSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            settings.LastUpdated = DateTime.UtcNow;
            await SaveSettingsAsync(settings);
            _cachedSettings = settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Update a single setting field
    /// </summary>
    public async Task UpdateSettingAsync(string key, string value)
    {
        _logger.Debug($"UpdateSettingAsync called - Key: {key}, Value: {value}", "GlobalSettingsService");
        await _lock.WaitAsync();
        try
        {
            // Load settings from cache or file (without calling GetSettingsAsync to avoid deadlock)
            GlobalSettings settings;
            if (_cachedSettings != null)
            {
                _logger.Debug($"Using cached settings", "GlobalSettingsService");
                settings = _cachedSettings;
            }
            else if (File.Exists(_settingsFilePath))
            {
                _logger.Debug($"Loading settings from file", "GlobalSettingsService");
                settings = await JsonHelper.DeserializeFromFileAsync<GlobalSettings>(_settingsFilePath) ?? new GlobalSettings();
            }
            else
            {
                _logger.Debug($"Creating new default settings", "GlobalSettingsService");
                settings = new GlobalSettings();
            }

            // Update the specific field
            switch (key.ToLowerInvariant())
            {
                case "theme":
                    _logger.Info($"Updating theme from '{settings.Theme}' to '{value}'", "GlobalSettingsService");
                    settings.Theme = value;
                    break;
                case "annotationlevel":
                    _logger.Info($"Updating annotationLevel from '{settings.AnnotationLevel}' to '{value}'", "GlobalSettingsService");
                    settings.AnnotationLevel = value;
                    break;
                case "loglevel":
                    _logger.Info($"Updating logLevel from '{settings.LogLevel}' to '{value}'", "GlobalSettingsService");
                    settings.LogLevel = value;
                    break;
                case "language":
                    _logger.Info($"Updating language from '{settings.Language}' to '{value}'", "GlobalSettingsService");
                    settings.Language = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown setting key: {key}");
            }

            settings.LastUpdated = DateTime.UtcNow;
            _logger.Debug($"Saving settings to: {_settingsFilePath}", "GlobalSettingsService");
            await SaveSettingsAsync(settings);
            _cachedSettings = settings;
            _logger.Info($"Settings saved successfully", "GlobalSettingsService");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reset settings to default values
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var defaultSettings = new GlobalSettings();
            await SaveSettingsAsync(defaultSettings);
            _cachedSettings = defaultSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Save settings to file
    /// </summary>
    private async Task SaveSettingsAsync(GlobalSettings settings)
    {
        await JsonHelper.SerializeToFileAsync(_settingsFilePath, settings);
    }
}
