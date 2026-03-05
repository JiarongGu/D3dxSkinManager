using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Setting.Models;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Modules.Setting.Services;

/// <summary>
/// Service for managing global application settings
/// Settings are stored in data/global.json
/// </summary>
public interface IGlobalSettingService
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
public class GlobalSettingService : IGlobalSettingService
{
    private readonly string _settingsFilePath;
    private readonly IMemoryCache _cache;
    private readonly IAppEnvironment _appEnvironment;
    private readonly IEventBus? _eventBus;
    private const string CacheKey = "GlobalSettings";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    public GlobalSettingService(IGlobalPathService globalPaths, IAppEnvironment appEnvironment, IMemoryCache cache, IEventBus? eventBus = null)
    {
        _appEnvironment = appEnvironment ?? throw new ArgumentNullException(nameof(appEnvironment));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _eventBus = eventBus;
        var globalPathsService = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));

        // Use GlobalPathService direct property for global settings file
        _settingsFilePath = globalPathsService.GlobalSettingsFilePath;

        // Ensure settings directory exists
        globalPathsService.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Get current global settings
    /// Uses IMemoryCache with automatic expiration
    /// </summary>
    public async Task<GlobalSettings> GetSettingsAsync()
    {
        // Try to get from cache, or create if not exists
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = CacheExpiry;

            // Load from file or create default
            GlobalSettings settings;
            if (File.Exists(_settingsFilePath))
            {
                settings = await JsonHelper.DeserializeFromFileAsync<GlobalSettings>(_settingsFilePath).ConfigureAwait(false)
                          ?? new GlobalSettings();

                // Apply log level to AppEnvironment
                _appEnvironment.MinimumLogLevel = ParseLogLevel(settings.LogLevel);
            }
            else
            {
                settings = new GlobalSettings();

                // Apply default log level to AppEnvironment
                _appEnvironment.MinimumLogLevel = ParseLogLevel(settings.LogLevel);

                await SaveSettingsAsync(settings).ConfigureAwait(false);
            }

            return settings;
        }).ConfigureAwait(false) ?? new GlobalSettings();
    }

    public async Task<LogLevel> GetLogLevelAsync()
    {
        var settings = await GetSettingsAsync().ConfigureAwait(false);
        return ParseLogLevel(settings.LogLevel);
    }

    /// <summary>
    /// Update global settings and invalidate cache
    /// </summary>
    public async Task UpdateSettingsAsync(GlobalSettings settings)
    {
        settings.LastUpdated = DateTime.UtcNow;
        await SaveSettingsAsync(settings).ConfigureAwait(false);

        // Invalidate cache so next read gets fresh data
        InvalidateCache();

        // Emit event to notify all windows of settings change
        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(ModuleNames.SETTING, SettingEvents.GLOBAL_SETTINGS_CHANGED, settings).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invalidate the cache - next GetSettingsAsync call will reload from file
    /// </summary>
    private void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }

    /// <summary>
    /// Update a single setting field and invalidate cache
    /// </summary>
    public async Task UpdateSettingAsync(string key, string value)
    {
        // Load current settings
        var settings = await GetSettingsAsync().ConfigureAwait(false);

        // Update the specific field - store values in lowercase for consistency
        switch (key.ToLowerInvariant())
        {
            case "theme":
                settings.Theme = value.ToLowerInvariant();
                break;
            case "annotationlevel":
                settings.AnnotationLevel = value.ToLowerInvariant();
                break;
            case "loglevel":
                settings.LogLevel = value.ToLowerInvariant();
                // Update AppEnvironment immediately so LogHelper uses the new level (case-insensitive parse)
                _appEnvironment.MinimumLogLevel = ParseLogLevel(value);
                break;
            case "language":
                settings.Language = value.ToLowerInvariant();
                break;
            default:
                throw new ArgumentException($"Unknown setting key: {key}");
        }

        settings.LastUpdated = DateTime.UtcNow;
        await SaveSettingsAsync(settings).ConfigureAwait(false);

        // Invalidate cache so next read gets fresh data
        InvalidateCache();

        // Emit event to notify all windows of settings change
        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(ModuleNames.SETTING, SettingEvents.GLOBAL_SETTINGS_CHANGED, settings).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reset settings to default values and invalidate cache
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        var defaultSettings = new GlobalSettings();
        await SaveSettingsAsync(defaultSettings).ConfigureAwait(false);

        // Invalidate cache so next read gets fresh data
        InvalidateCache();

        // Emit event to notify all windows of settings change
        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(ModuleNames.SETTING, SettingEvents.GLOBAL_SETTINGS_CHANGED, defaultSettings).ConfigureAwait(false);
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
