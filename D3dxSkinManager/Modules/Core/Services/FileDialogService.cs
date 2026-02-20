using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Service for native file dialogs
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Open file dialog to select a file
    /// </summary>
    Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null);

    /// <summary>
    /// Open folder dialog to select a directory
    /// </summary>
    Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null);

    /// <summary>
    /// Open save file dialog
    /// </summary>
    Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null);
}

/// <summary>
/// Windows Forms-based file dialog implementation
/// Uses STA thread for proper dialog display with path memory
/// Path memory persists across sessions via SystemSettings
/// Paths within data folder are stored as relative for portability
/// </summary>
public class FileDialogService : IFileDialogService
{
    private readonly SystemUtils.Services.ISystemSettingsService _systemSettings;
    private readonly IPathHelper _pathHelper;
    private readonly ILogHelper _logger;

    public FileDialogService(SystemUtils.Services.ISystemSettingsService systemSettings, IPathHelper pathHelper, ILogHelper logger)
    {
        _systemSettings = systemSettings ?? throw new ArgumentNullException(nameof(systemSettings));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Open file dialog to select a file
    /// </summary>
    public async Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null)
    {
        return await RunInStaThread(() =>
        {
            try
            {
                var initialPath = GetInitialPath(options);

                using var dialog = new OpenFileDialog
                {
                    Title = options?.Title ?? "Select File",
                    InitialDirectory = initialPath,
                    RestoreDirectory = false, // We handle this manually for better control
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                // Set filters
                if (options?.Filters != null && options.Filters.Count > 0)
                {
                    var filterStrings = options.Filters
                        .Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(ext => $"*.{ext}"))}")
                        .ToList();
                    dialog.Filter = string.Join("|", filterStrings);
                }
                else
                {
                    dialog.Filter = "All Files (*.*)|*.*";
                }

                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    // Remember the directory for next time
                    SaveLastUsedPath(options, Path.GetDirectoryName(dialog.FileName));

                    return new FileDialogResult
                    {
                        Success = true,
                        FilePath = dialog.FileName
                    };
                }

                return new FileDialogResult
                {
                    Success = false,
                    Error = "User cancelled file selection"
                };
            }
            catch (Exception ex)
            {
                return new FileDialogResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        });
    }

    /// <summary>
    /// Open folder dialog to select a directory
    /// </summary>
    public async Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null)
    {
        return await RunInStaThread(() =>
        {
            try
            {
                var initialPath = GetInitialPath(options);

                using var dialog = new FolderBrowserDialog
                {
                    Description = options?.Title ?? "Select Folder",
                    SelectedPath = initialPath,
                    ShowNewFolderButton = true,
                    UseDescriptionForTitle = true // Better title display on modern Windows
                };

                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    // Remember the selected folder for next time
                    SaveLastUsedPath(options, dialog.SelectedPath);

                    return new FileDialogResult
                    {
                        Success = true,
                        FilePath = dialog.SelectedPath
                    };
                }

                return new FileDialogResult
                {
                    Success = false,
                    Error = "User cancelled folder selection"
                };
            }
            catch (Exception ex)
            {
                return new FileDialogResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        });
    }

    /// <summary>
    /// Open save file dialog
    /// </summary>
    public async Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null)
    {
        return await RunInStaThread(() =>
        {
            try
            {
                var initialPath = GetInitialPath(options);

                using var dialog = new SaveFileDialog
                {
                    Title = options?.Title ?? "Save File",
                    InitialDirectory = initialPath,
                    RestoreDirectory = false, // We handle this manually for better control
                    CheckPathExists = true,
                    OverwritePrompt = true
                };

                // Set filters
                if (options?.Filters != null && options.Filters.Count > 0)
                {
                    var filterStrings = options.Filters
                        .Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(ext => $"*.{ext}"))}")
                        .ToList();
                    dialog.Filter = string.Join("|", filterStrings);
                }
                else
                {
                    dialog.Filter = "All Files (*.*)|*.*";
                }

                var result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    // Remember the directory for next time
                    SaveLastUsedPath(options, Path.GetDirectoryName(dialog.FileName));

                    return new FileDialogResult
                    {
                        Success = true,
                        FilePath = dialog.FileName
                    };
                }

                return new FileDialogResult
                {
                    Success = false,
                    Error = "User cancelled save dialog"
                };
            }
            catch (Exception ex)
            {
                return new FileDialogResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        });
    }

    /// <summary>
    /// Get the initial path for the dialog, using remembered path if available
    /// </summary>
    private string GetInitialPath(FileDialogOptions? options)
    {
        // Priority:
        // 1. Remembered path (if RememberPathKey is set and we have a saved path)
        // 2. Explicitly provided DefaultPath
        // 3. My Documents folder

        if (!string.IsNullOrWhiteSpace(options?.RememberPathKey))
        {
            try
            {
                var settings = _systemSettings.GetSettingsAsync().GetAwaiter().GetResult();
                if (settings.FileDialogPaths.TryGetValue(options.RememberPathKey, out var rememberedPath))
                {
                    // Convert relative path to absolute if needed
                    var absolutePath = _pathHelper.ToAbsolutePath(rememberedPath) ?? rememberedPath;

                    // Verify the path still exists
                    if (Directory.Exists(absolutePath))
                    {
                        return absolutePath;
                    }
                    else
                    {
                        // Clean up invalid path asynchronously (fire and forget)
                        _ = Task.Run(async () =>
                        {
                            var updatedSettings = await _systemSettings.GetSettingsAsync();
                            updatedSettings.FileDialogPaths.Remove(options.RememberPathKey);
                            await _systemSettings.UpdateSettingsAsync(updatedSettings);
                        });
                    }
                }
            }
            catch
            {
                // If settings load fails, just continue to fallback
            }
        }

        // Fall back to provided default path
        if (!string.IsNullOrWhiteSpace(options?.DefaultPath) && Directory.Exists(options.DefaultPath))
        {
            return options.DefaultPath;
        }

        // Final fallback
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    /// <summary>
    /// Save the last used path for future use (persists to global settings)
    /// Called from STA thread context (Windows Forms dialog), so we use sync-over-async pattern
    /// </summary>
    private void SaveLastUsedPath(FileDialogOptions? options, string? path)
    {
        _logger.Debug($"SaveLastUsedPath called - Key: '{options?.RememberPathKey}', Path: '{path}'", "FileDialogService");

        if (string.IsNullOrWhiteSpace(options?.RememberPathKey))
        {
            _logger.Debug("SaveLastUsedPath: No RememberPathKey provided, skipping save", "FileDialogService");
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.Debug("SaveLastUsedPath: Path is null or empty, skipping save", "FileDialogService");
            return;
        }

        if (!Directory.Exists(path))
        {
            _logger.Debug($"SaveLastUsedPath: Directory does not exist: '{path}', skipping save", "FileDialogService");
            return;
        }

        try
        {
            _logger.Debug($"SaveLastUsedPath: Loading system settings...", "FileDialogService");

            // Convert to relative path if within data folder for portability
            var pathToSave = _pathHelper.ToRelativePath(path) ?? path;
            _logger.Debug($"SaveLastUsedPath: Setting key '{options.RememberPathKey}' to '{pathToSave}' (original: '{path}')", "FileDialogService");

            _logger.Debug($"SaveLastUsedPath: Saving settings...", "FileDialogService");
            _systemSettings.RememberFileDialogPathAsync(options.RememberPathKey, pathToSave).GetAwaiter().GetResult();

            _logger.Debug($"SaveLastUsedPath: Successfully saved path for key '{options.RememberPathKey}'", "FileDialogService");
        }
        catch (Exception ex)
        {
            _logger.Error($"SaveLastUsedPath: Error saving path - {ex.Message}", "FileDialogService");
            _logger.Debug($"SaveLastUsedPath: Stack trace: {ex.StackTrace}", "FileDialogService");
        }
    }

    /// <summary>
    /// Runs a function on an STA thread (required for Windows Forms dialogs)
    /// </summary>
    private static Task<T> RunInStaThread<T>(Func<T> function)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                var result = function();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true; // Don't block app shutdown
        thread.Start();

        return tcs.Task;
    }
}
