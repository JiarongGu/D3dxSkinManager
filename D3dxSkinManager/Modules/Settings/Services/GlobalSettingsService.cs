using System.Text.Json;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Models;
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
    private readonly AppEnvironment _appEnvironment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalSettingsService(IGlobalPathService globalPaths, AppEnvironment appEnvironment)
    {
        _appEnvironment = appEnvironment ?? throw new ArgumentNullException(nameof(appEnvironment));
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
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Return cached if available
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            // Load from file or create default
            if (File.Exists(_settingsFilePath))
            {
                _cachedSettings = await JsonHelper.DeserializeFromFileAsync<GlobalSettings>(_settingsFilePath).ConfigureAwait(false)
                                  ?? new GlobalSettings();

                // Apply log level to AppEnvironment
                _appEnvironment.MinimumLogLevel = ParseLogLevel(_cachedSettings.LogLevel);
            }
            else
            {
                _cachedSettings = new GlobalSettings();

                // Apply default log level to AppEnvironment
                _appEnvironment.MinimumLogLevel = ParseLogLevel(_cachedSettings.LogLevel);

                await SaveSettingsAsync(_cachedSettings).ConfigureAwait(false);
            }

            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LogLevel> GetLogLevelAsync()
    {
        var settings = await GetSettingsAsync().ConfigureAwait(false);
        return ParseLogLevel(settings.LogLevel);
    }

    /// <summary>
    /// Update global settings
    /// </summary>
    public async Task UpdateSettingsAsync(GlobalSettings settings)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            settings.LastUpdated = DateTime.UtcNow;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
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
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Load settings from cache or file (without calling GetSettingsAsync to avoid deadlock)
            GlobalSettings settings;
            if (_cachedSettings != null)
            {
                settings = _cachedSettings;
            }
            else if (File.Exists(_settingsFilePath))
            {
                settings = await JsonHelper.DeserializeFromFileAsync<GlobalSettings>(_settingsFilePath).ConfigureAwait(false) ?? new GlobalSettings();
            }
            else
            {
                settings = new GlobalSettings();
            }

            // Update the specific field
            switch (key.ToLowerInvariant())
            {
                case "theme":
                    settings.Theme = value;
                    break;
                case "annotationlevel":
                    settings.AnnotationLevel = value;
                    break;
                case "loglevel":
                    settings.LogLevel = value;
                    // Update AppEnvironment immediately so LogHelper uses the new level
                    _appEnvironment.MinimumLogLevel = ParseLogLevel(value);
                    break;
                case "language":
                    settings.Language = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown setting key: {key}");
            }

            settings.LastUpdated = DateTime.UtcNow;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
            _cachedSettings = settings;
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
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var defaultSettings = new GlobalSettings();
            await SaveSettingsAsync(defaultSettings).ConfigureAwait(false);
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
        await JsonHelper.SerializeToFileAsync(_settingsFilePath, settings).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a string log level to LogLevel enum
    /// Frontend uses: ALL, DEBUG, INFO, WARN, ERROR, OFF
    /// Backend enum: All, Debug, Info, Warn, Error, Off
    /// </summary>
    private LogLevel ParseLogLevel(string logLevelStr)
    {
        // Try direct enum parse first (case-insensitive)
        // This handles: ALL, DEBUG, INFO, WARN, ERROR, OFF
        if (Enum.TryParse<LogLevel>(logLevelStr, true, out var level))
        {
            return level;
        }

        // Default to OFF for unknown values
        return LogLevel.Off;
    }
}
