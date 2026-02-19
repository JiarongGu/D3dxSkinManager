using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Settings.Models;

namespace D3dxSkinManager.Modules.Settings;

/// <summary>
/// Interface for Settings facade
/// Handles: SETTINGS_GET, SETTINGS_UPDATE, etc.
/// Prefix: SETTINGS_*
/// </summary>
public interface ISettingsFacade : IModuleFacade
{
    // Global Settings
    Task<GlobalSettings> GetGlobalSettingsAsync();
    Task UpdateGlobalSettingsAsync(GlobalSettings settings);
    Task UpdateGlobalSettingAsync(string key, string value);
    Task ResetGlobalSettingsAsync();

    // File System Operations (moved from Core)
    Task OpenFileInExplorerAsync(string filePath);
    Task OpenDirectoryAsync(string directoryPath);
    Task OpenFileAsync(string filePath);

    // File Dialogs (moved from Core)
    Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null);
    Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null);
    Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null);
}
