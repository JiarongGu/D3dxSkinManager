using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Tool.Services;

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
    private readonly ILogHelper _logger;
    private Dictionary<string, object> _config;
    private readonly Lazy<Task> _init;

    public ConfigurationService(IProfilePathService profilePaths, ILogHelper logger)
    {
        _configPath = profilePaths?.ConfigPath ?? throw new ArgumentNullException(nameof(profilePaths));
        _logger = logger;
        _config = new Dictionary<string, object>();

        // Lazy initialization to avoid blocking constructor
        _init = new Lazy<Task>(LoadAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    public string? GetWorkDirectory()
    {
        // Ensure initialized synchronously - this is safe because Lazy<Task> caches the result
        _init.Value.ConfigureAwait(false).GetAwaiter().GetResult();
        return GetValue<string>("workDirectory");
    }

    public async Task SetWorkDirectoryAsync(string path)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await SetValueAsync("workDirectory", path).ConfigureAwait(false);
        await SaveAsync().ConfigureAwait(false);
    }

    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        // Ensure initialized synchronously - this is safe because Lazy<Task> caches the result
        _init.Value.ConfigureAwait(false).GetAwaiter().GetResult();

        if (_config.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Try to convert JSON values
                if (value is JsonElement jsonElement)
                {
                    return JsonHelper.Deserialize<T>(jsonElement.GetRawText());
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
        await EnsureInitializedAsync().ConfigureAwait(false);

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
        await EnsureInitializedAsync().ConfigureAwait(false);

        try
        {
            await JsonHelper.SerializeToFileAsync(_configPath, _config).ConfigureAwait(false);
            _logger.Info($"Configuration saved to {_configPath}", "ConfigurationService");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save configuration: {ex.Message}", "ConfigurationService", ex);
            throw;
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.Info($"Configuration file not found. Using defaults.", "ConfigurationService");
                _config = new Dictionary<string, object>();
                return;
            }

            _config = await JsonHelper.DeserializeFromFileAsync<Dictionary<string, object>>(_configPath).ConfigureAwait(false)
                ?? new Dictionary<string, object>();

            _logger.Info($"Configuration loaded from {_configPath}", "ConfigurationService");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load configuration: {ex.Message}", "ConfigurationService", ex);
            _config = new Dictionary<string, object>();
        }
    }
}
