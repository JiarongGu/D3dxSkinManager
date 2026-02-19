using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Tools.Services;

/// <summary>
/// Interface for configuration service
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Get the 3DMigoto work directory path
    /// </summary>
    string? GetWorkDirectory();

    /// <summary>
    /// Set the 3DMigoto work directory path
    /// </summary>
    Task SetWorkDirectoryAsync(string path);

    /// <summary>
    /// Get configuration value
    /// </summary>
    T? GetValue<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Set configuration value
    /// </summary>
    Task SetValueAsync<T>(string key, T value);

    /// <summary>
    /// Save configuration to disk
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Load configuration from disk
    /// </summary>
    Task LoadAsync();
}

/// <summary>
/// Service for managing application configuration
/// Stores settings like 3DMigoto work directory, user preferences, etc.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private Dictionary<string, object> _config;

    public ConfigurationService(IProfileContext profileContext)
    {
        _configPath = Path.Combine(profileContext.ProfilePath, "config.json");
        _config = new Dictionary<string, object>();

        // Load existing configuration
        LoadAsync().Wait();
    }

    public string? GetWorkDirectory()
    {
        return GetValue<string>("workDirectory");
    }

    public async Task SetWorkDirectoryAsync(string path)
    {
        await SetValueAsync("workDirectory", path);
        await SaveAsync();
    }

    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (_config.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Try to convert JSON values
                if (value is Newtonsoft.Json.Linq.JToken jToken)
                {
                    return jToken.ToObject<T>();
                }

                // Try direct conversion
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    public async Task SetValueAsync<T>(string key, T value)
    {
        if (value == null)
        {
            _config.Remove(key);
        }
        else
        {
            _config[key] = value;
        }

        await Task.CompletedTask;
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            await File.WriteAllTextAsync(_configPath, json);
            Console.WriteLine($"[ConfigurationService] Configuration saved to {_configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigurationService] Failed to save configuration: {ex.Message}");
            throw;
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Console.WriteLine($"[ConfigurationService] Configuration file not found. Using defaults.");
                _config = new Dictionary<string, object>();
                return;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                ?? new Dictionary<string, object>();

            Console.WriteLine($"[ConfigurationService] Configuration loaded from {_configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigurationService] Failed to load configuration: {ex.Message}");
            _config = new Dictionary<string, object>();
        }
    }
}
