using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.SystemUtils.Models;

namespace D3dxSkinManager.Modules.SystemUtils.Services;

/// <summary>
/// Service for managing system-level settings
/// Settings are stored in data/settings/system.json
/// </summary>
public interface ISystemSettingsService
{
    /// <summary>
    /// Get current system settings
    /// </summary>
    Task<SystemSettings> GetSettingsAsync();

    /// <summary>
    /// Update system settings
    /// </summary>
    Task UpdateSettingsAsync(SystemSettings settings);

    /// <summary>
    /// Remember a file dialog path by key
    /// </summary>
    Task RememberFileDialogPathAsync(string key, string path);

    /// <summary>
    /// Get remembered file dialog path by key
    /// </summary>
    Task<string?> GetFileDialogPathAsync(string key);

    /// <summary>
    /// Reset settings to default values
    /// </summary>
    Task ResetSettingsAsync();
}

/// <summary>
/// Service for managing system-level settings
/// Settings are stored in data/settings/system.json
/// </summary>
public class SystemSettingsService : ISystemSettingsService
{
    private readonly string _settingsFilePath;
    private SystemSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogHelper _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SystemSettingsService(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var globalPathsService = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));

        // Use GetGlobalSettingsFilePath for system settings file
        _settingsFilePath = globalPathsService.GetGlobalSettingsFilePath("system.json");

        // Ensure settings directory exists
        globalPathsService.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Get current system settings
    /// </summary>
    public async Task<SystemSettings> GetSettingsAsync()
    {
        _logger.Debug($"GetSettingsAsync called", "SystemSettingsService");
        _logger.Debug($"Settings file path: {_settingsFilePath}", "SystemSettingsService");

        await _lock.WaitAsync();
        try
        {
            // Return cached if available
            if (_cachedSettings != null)
            {
                _logger.Debug($"Returning cached settings", "SystemSettingsService");
                return _cachedSettings;
            }

            _logger.Debug($"No cached settings, loading from file...", "SystemSettingsService");

            // Load from file or create default
            if (File.Exists(_settingsFilePath))
            {
                _logger.Debug($"Settings file exists, reading...", "SystemSettingsService");
                _cachedSettings = await JsonHelper.DeserializeFromFileAsync<SystemSettings>(_settingsFilePath)
                                  ?? new SystemSettings();
                _logger.Info($"Settings loaded from file", "SystemSettingsService");
            }
            else
            {
                _logger.Info($"Settings file not found, creating default...", "SystemSettingsService");
                _cachedSettings = new SystemSettings();
                await SaveSettingsAsync(_cachedSettings);
                _logger.Info($"Default settings created and saved", "SystemSettingsService");
            }

            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Update system settings
    /// </summary>
    public async Task UpdateSettingsAsync(SystemSettings settings)
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
    /// Remember a file dialog path by key
    /// </summary>
    public async Task RememberFileDialogPathAsync(string key, string path)
    {
        _logger.Debug($"RememberFileDialogPathAsync called - Key: {key}, Path: {path}", "SystemSettingsService");
        await _lock.WaitAsync();
        try
        {
            // Load settings from cache or file (without calling GetSettingsAsync to avoid deadlock)
            SystemSettings settings;
            if (_cachedSettings != null)
            {
                _logger.Debug($"Using cached settings", "SystemSettingsService");
                settings = _cachedSettings;
            }
            else if (File.Exists(_settingsFilePath))
            {
                _logger.Debug($"Loading settings from file", "SystemSettingsService");
                settings = await JsonHelper.DeserializeFromFileAsync<SystemSettings>(_settingsFilePath) ?? new SystemSettings();
            }
            else
            {
                _logger.Debug($"Creating new default settings", "SystemSettingsService");
                settings = new SystemSettings();
            }

            // Update the path
            settings.FileDialogPaths[key] = path;
            settings.LastUpdated = DateTime.UtcNow;

            _logger.Debug($"Saving settings to: {_settingsFilePath}", "SystemSettingsService");
            await SaveSettingsAsync(settings);
            _cachedSettings = settings;
            _logger.Info($"File dialog path remembered for key: {key}", "SystemSettingsService");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get remembered file dialog path by key
    /// </summary>
    public async Task<string?> GetFileDialogPathAsync(string key)
    {
        _logger.Debug($"GetFileDialogPathAsync called - Key: {key}", "SystemSettingsService");
        var settings = await GetSettingsAsync();

        if (settings.FileDialogPaths.TryGetValue(key, out var path))
        {
            _logger.Debug($"Found remembered path for key '{key}': {path}", "SystemSettingsService");
            return path;
        }

        _logger.Debug($"No remembered path found for key: {key}", "SystemSettingsService");
        return null;
    }

    /// <summary>
    /// Reset settings to default values
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var defaultSettings = new SystemSettings();
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
    private async Task SaveSettingsAsync(SystemSettings settings)
    {
        await JsonHelper.SerializeToFileAsync(_settingsFilePath, settings);
    }
}
