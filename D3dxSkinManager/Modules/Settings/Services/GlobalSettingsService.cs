using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalSettingsService(IPathHelper pathHelper)
    {
        // Store global settings in data/settings/global.json
        var settingsDir = Path.Combine(pathHelper.BaseDataPath, "settings");
        _settingsFilePath = Path.Combine(settingsDir, "global.json");

        // Ensure settings directory exists
        if (!Directory.Exists(settingsDir))
        {
            Directory.CreateDirectory(settingsDir);
        }
    }

    /// <summary>
    /// Get current global settings
    /// </summary>
    public async Task<GlobalSettings> GetSettingsAsync()
    {
        Console.WriteLine($"[GlobalSettingsService] GetSettingsAsync called");
        Console.WriteLine($"[GlobalSettingsService] Settings file path: {_settingsFilePath}");

        await _lock.WaitAsync();
        try
        {
            // Return cached if available
            if (_cachedSettings != null)
            {
                Console.WriteLine($"[GlobalSettingsService] Returning cached settings");
                return _cachedSettings;
            }

            Console.WriteLine($"[GlobalSettingsService] No cached settings, loading from file...");

            // Load from file or create default
            if (File.Exists(_settingsFilePath))
            {
                Console.WriteLine($"[GlobalSettingsService] Settings file exists, reading...");
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions)
                                  ?? new GlobalSettings();
                Console.WriteLine($"[GlobalSettingsService] Settings loaded from file");
            }
            else
            {
                Console.WriteLine($"[GlobalSettingsService] Settings file not found, creating default...");
                _cachedSettings = new GlobalSettings();
                await SaveSettingsAsync(_cachedSettings);
                Console.WriteLine($"[GlobalSettingsService] Default settings created and saved");
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
        Console.WriteLine($"[GlobalSettingsService] UpdateSettingAsync called - Key: {key}, Value: {value}");
        await _lock.WaitAsync();
        try
        {
            // Load settings from cache or file (without calling GetSettingsAsync to avoid deadlock)
            GlobalSettings settings;
            if (_cachedSettings != null)
            {
                Console.WriteLine($"[GlobalSettingsService] Using cached settings");
                settings = _cachedSettings;
            }
            else if (File.Exists(_settingsFilePath))
            {
                Console.WriteLine($"[GlobalSettingsService] Loading settings from file");
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                settings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
            }
            else
            {
                Console.WriteLine($"[GlobalSettingsService] Creating new default settings");
                settings = new GlobalSettings();
            }

            // Update the specific field
            switch (key.ToLowerInvariant())
            {
                case "theme":
                    Console.WriteLine($"[GlobalSettingsService] Updating theme from '{settings.Theme}' to '{value}'");
                    settings.Theme = value;
                    break;
                case "annotationlevel":
                    Console.WriteLine($"[GlobalSettingsService] Updating annotationLevel from '{settings.AnnotationLevel}' to '{value}'");
                    settings.AnnotationLevel = value;
                    break;
                case "loglevel":
                    Console.WriteLine($"[GlobalSettingsService] Updating logLevel from '{settings.LogLevel}' to '{value}'");
                    settings.LogLevel = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown setting key: {key}");
            }

            settings.LastUpdated = DateTime.UtcNow;
            Console.WriteLine($"[GlobalSettingsService] Saving settings to: {_settingsFilePath}");
            await SaveSettingsAsync(settings);
            _cachedSettings = settings;
            Console.WriteLine($"[GlobalSettingsService] Settings saved successfully");
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
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
}
