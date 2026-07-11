using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.System.Models;

namespace D3dxSkinManager.Modules.System.Services;

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
}

/// <summary>
/// Service for managing system-level settings
/// Settings are stored in data/settings/system.json
/// </summary>
public class SystemSettingsService : ISystemSettingsService
{
    private readonly string _settingsFilePath;
    private readonly IMemoryCache _cache;
    private readonly ILogHelper _logger;
    private const string CacheKey = "SystemSettings";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SystemSettingsService(IGlobalPathService globalPaths, ILogHelper logger, IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        var globalPathsService = globalPaths ?? throw new ArgumentNullException(nameof(globalPaths));

        // Use GetGlobalSettingsFilePath for system settings file
        _settingsFilePath = globalPathsService.GetGlobalSettingsFilePath("system.json");

        // Ensure settings directory exists
        globalPathsService.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Get current system settings
    /// Uses IMemoryCache with automatic expiration
    /// </summary>
    public async Task<SystemSettings> GetSettingsAsync()
    {
        _logger.Debug($"GetSettingsAsync called", "SystemSettingsService");
        _logger.Debug($"Settings file path: {_settingsFilePath}", "SystemSettingsService");

        // Try to get from cache, or create if not exists
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = CacheExpiry;

            _logger.Debug($"No cached settings, loading from file...", "SystemSettingsService");

            // Load from file or create default
            SystemSettings settings;
            if (File.Exists(_settingsFilePath))
            {
                _logger.Debug($"Settings file exists, reading...", "SystemSettingsService");
                settings = await JsonHelper.DeserializeFromFileAsync<SystemSettings>(_settingsFilePath).ConfigureAwait(false)
                          ?? new SystemSettings();
                _logger.Info($"Settings loaded from file", "SystemSettingsService");
            }
            else
            {
                _logger.Info($"Settings file not found, creating default...", "SystemSettingsService");
                settings = new SystemSettings();
                await SaveSettingsAsync(settings).ConfigureAwait(false);
                _logger.Info($"Default settings created and saved", "SystemSettingsService");
            }

            return settings;
        }).ConfigureAwait(false) ?? new SystemSettings();
    }

    /// <summary>
    /// Update system settings and invalidate cache
    /// </summary>
    public async Task UpdateSettingsAsync(SystemSettings settings)
    {
        settings.LastUpdated = DateTime.UtcNow;
        await SaveSettingsAsync(settings).ConfigureAwait(false);

        // Invalidate cache so next read gets fresh data
        InvalidateCache();
    }

    /// <summary>
    /// Invalidate the cache - next GetSettingsAsync call will reload from file
    /// </summary>
    private void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }

    /// <summary>
    /// Remember a file dialog path by key and invalidate cache
    /// </summary>
    public async Task RememberFileDialogPathAsync(string key, string path)
    {
        // Load current settings
        var settings = await GetSettingsAsync().ConfigureAwait(false);

        // Update the path
        settings.FileDialogPaths[key] = path;
        settings.LastUpdated = DateTime.UtcNow;

        await SaveSettingsAsync(settings).ConfigureAwait(false);

        // Invalidate cache so next read gets fresh data
        InvalidateCache();
    }

    /// <summary>
    /// Get remembered file dialog path by key
    /// </summary>
    public async Task<string?> GetFileDialogPathAsync(string key)
    {
        _logger.Debug($"GetFileDialogPathAsync called - Key: {key}", "SystemSettingsService");
        var settings = await GetSettingsAsync().ConfigureAwait(false);

        if (settings.FileDialogPaths.TryGetValue(key, out var path))
        {
            _logger.Debug($"Found remembered path for key '{key}': {path}", "SystemSettingsService");
            return path;
        }

        _logger.Debug($"No remembered path found for key: {key}", "SystemSettingsService");
        return null;
    }

    /// <summary>
    /// Save settings to file
    /// </summary>
    private async Task SaveSettingsAsync(SystemSettings settings)
    {
        await JsonHelper.SerializeToFileAsync(_settingsFilePath, settings).ConfigureAwait(false);
    }
}
