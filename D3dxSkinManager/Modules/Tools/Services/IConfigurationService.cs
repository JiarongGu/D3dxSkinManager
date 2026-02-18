using System.Threading.Tasks;

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
