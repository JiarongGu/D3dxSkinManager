using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing generic JSON settings files
/// Allows frontend to store/retrieve any JSON settings by filename
/// Files are stored in data/settings/ directory
/// </summary>
public interface ISettingsFileService
{
    /// <summary>
    /// Get a settings file by name (without .json extension)
    /// Returns the JSON content as a string, or null if file doesn't exist
    /// </summary>
    Task<string?> GetSettingsFileAsync(string filename);

    /// <summary>
    /// Save a settings file by name (without .json extension)
    /// Content should be valid JSON string
    /// </summary>
    Task SaveSettingsFileAsync(string filename, string jsonContent);

    /// <summary>
    /// Delete a settings file by name (without .json extension)
    /// </summary>
    Task DeleteSettingsFileAsync(string filename);

    /// <summary>
    /// Check if a settings file exists
    /// </summary>
    Task<bool> SettingsFileExistsAsync(string filename);

    /// <summary>
    /// List all settings files (returns filenames without .json extension)
    /// </summary>
    Task<string[]> ListSettingsFilesAsync();
}
