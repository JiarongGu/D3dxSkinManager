using System.Threading.Tasks;
using D3dxSkinManager.Modules.Settings.Models;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing global application settings
/// Settings are stored in data/global_settings.json
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
