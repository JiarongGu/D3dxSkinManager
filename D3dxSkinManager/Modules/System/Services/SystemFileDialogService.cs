using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// Service for native file dialogs
/// </summary>
public interface ISystemFileDialogService
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
public class SystemFileDialogService : ISystemFileDialogService
{
    private readonly ISystemSettingsService _systemSettings;
    private readonly IPathHelper _pathHelper;
    private readonly ILogHelper _logger;
    private readonly IFormInteractionService _formInteractionService;

    public SystemFileDialogService(
        ISystemSettingsService systemSettings,
        IPathHelper pathHelper,
        ILogHelper logger,
        IFormInteractionService formInteractionService)
    {
        _systemSettings = systemSettings ?? throw new ArgumentNullException(nameof(systemSettings));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _formInteractionService = formInteractionService ?? throw new ArgumentNullException(nameof(formInteractionService));
    }

    /// <summary>
    /// Open file dialog to select a file
    /// </summary>
    public async Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null)
    {
        // Load initial path BEFORE showing dialog
        var initialPath = await GetInitialPathAsync(options).ConfigureAwait(false);

        // Get main form window handle for dialog ownership (thread-safe)
        var ownerHandle = _formInteractionService.GetMainFormHandle();

        // Block form interaction before showing dialog
        _formInteractionService.BlockInteraction();

        try
        {
            // ALWAYS use RunInStaThread to avoid WebView2 threading conflicts
            // This ensures the dialog runs on a dedicated STA thread separate from WebView2
            return await RunInStaThread(() => ShowOpenFileDialog(options, initialPath, ownerHandle));
        }
        finally
        {
            // Unblock form interaction after dialog closes
            _formInteractionService.UnblockInteraction();
        }
    }

    private FileDialogResult ShowOpenFileDialog(FileDialogOptions? options, string initialPath, IntPtr ownerHandle)
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Title = options?.Title ?? "Select File",
                InitialDirectory = initialPath,
                RestoreDirectory = false, // We handle this manually for better control
                // Use configurable options or defaults for file selection
                CheckFileExists = options?.CheckFileExists ?? true,  // Default: true for file dialogs
                CheckPathExists = options?.CheckPathExists ?? true,  // Default: true
                ValidateNames = options?.ValidateNames ?? true,      // Default: true for file dialogs
                FileName = options?.FileName ?? "" // Default: empty
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

            // Show dialog with owner window handle to maintain proper z-order
            var result = ownerHandle != IntPtr.Zero
                ? dialog.ShowDialog(new WindowHandleWrapper(ownerHandle))
                : dialog.ShowDialog();

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
    }

    /// <summary>
    /// Open folder dialog to select a directory
    /// </summary>
    public async Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null)
    {
        // Load initial path BEFORE showing dialog
        var initialPath = await GetInitialPathAsync(options).ConfigureAwait(false);

        // Get main form window handle for dialog ownership (thread-safe)
        var ownerHandle = _formInteractionService.GetMainFormHandle();

        // Block form interaction before showing dialog
        _formInteractionService.BlockInteraction();

        try
        {
            // ALWAYS use RunInStaThread to avoid WebView2 threading conflicts
            // This ensures the dialog runs on a dedicated STA thread separate from WebView2
            return await RunInStaThread(() => ShowFolderDialog(options, initialPath, ownerHandle));
        }
        finally
        {
            // Unblock form interaction after dialog closes
            _formInteractionService.UnblockInteraction();
        }
    }

    private FileDialogResult ShowFolderDialog(FileDialogOptions? options, string initialPath, IntPtr ownerHandle)
    {
        try
        {
            // If AllowFileSelection is true, use OpenFileDialog with relaxed validation
            if (options?.AllowFileSelection == true)
            {
                using var dialog = new OpenFileDialog
                {
                    Title = options.Title ?? "Select Folder or File",
                    InitialDirectory = initialPath,
                    RestoreDirectory = false,
                    CheckFileExists = false,  // Allow non-file selections
                    CheckPathExists = true,   // But path must exist
                    ValidateNames = false,    // Allow folder paths
                    FileName = "Folder Selection"  // Placeholder to enable folder selection
                };

                // Set file type filters
                if (options.Filters != null && options.Filters.Count > 0)
                {
                    var filterStrings = options.Filters
                        .Select(f => $"{f.Name}|{string.Join(";", f.Extensions.Select(ext => $"*.{ext}"))}")
                        .ToList();
                    dialog.Filter = string.Join("|", filterStrings);
                }

                // Show dialog
                var result = ownerHandle != IntPtr.Zero
                    ? dialog.ShowDialog(new WindowHandleWrapper(ownerHandle))
                    : dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    var selectedPath = dialog.FileName;

                    // Check if user selected the placeholder (folder selection)
                    var fileName = Path.GetFileName(selectedPath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(selectedPath);

                    // If the filename matches the placeholder (with or without extension), extract directory
                    if (fileName == "Folder Selection" || fileNameWithoutExt == "Folder Selection")
                    {
                        selectedPath = Path.GetDirectoryName(selectedPath) ?? selectedPath;
                    }

                    _logger.Debug($"[FileDialog] Selected path: '{selectedPath}'", "FileDialogService");

                    // Validate the path exists (file or folder)
                    if (File.Exists(selectedPath) || Directory.Exists(selectedPath))
                    {
                        var isFile = File.Exists(selectedPath);
                        var pathToRemember = isFile
                            ? Path.GetDirectoryName(selectedPath)
                            : selectedPath;
                        SaveLastUsedPath(options, pathToRemember);

                        _logger.Info($"[FileDialog] Selected {(isFile ? "file" : "folder")}: '{selectedPath}'", "FileDialogService");

                        return new FileDialogResult
                        {
                            Success = true,
                            FilePath = selectedPath
                        };
                    }

                    _logger.Warn($"[FileDialog] Path does not exist: '{selectedPath}'", "FileDialogService");
                }

                return new FileDialogResult
                {
                    Success = false,
                    Error = "User cancelled selection"
                };
            }

            // Use traditional FolderBrowserDialog for folder-only selection
            using var folderDialog = new FolderBrowserDialog
            {
                Description = options?.Title ?? "Select Folder",
                SelectedPath = initialPath,
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true // Better title display on modern Windows
            };

            // Show dialog with owner window handle to maintain proper z-order
            var folderResult = ownerHandle != IntPtr.Zero
                ? folderDialog.ShowDialog(new WindowHandleWrapper(ownerHandle))
                : folderDialog.ShowDialog();

            if (folderResult == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                // Remember the selected folder for next time
                SaveLastUsedPath(options, folderDialog.SelectedPath);

                return new FileDialogResult
                {
                    Success = true,
                    FilePath = folderDialog.SelectedPath
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
    }

    /// <summary>
    /// Open save file dialog
    /// </summary>
    public async Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null)
    {
        // Load initial path BEFORE showing dialog
        var initialPath = await GetInitialPathAsync(options).ConfigureAwait(false);

        // Get main form window handle for dialog ownership (thread-safe)
        var ownerHandle = _formInteractionService.GetMainFormHandle();

        // Block form interaction before showing dialog
        _formInteractionService.BlockInteraction();

        try
        {
            // ALWAYS use RunInStaThread to avoid WebView2 threading conflicts
            // This ensures the dialog runs on a dedicated STA thread separate from WebView2
            return await RunInStaThread(() => ShowSaveFileDialog(options, initialPath, ownerHandle));
        }
        finally
        {
            // Unblock form interaction after dialog closes
            _formInteractionService.UnblockInteraction();
        }
    }

    private FileDialogResult ShowSaveFileDialog(FileDialogOptions? options, string initialPath, IntPtr ownerHandle)
    {
        try
        {
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

            // Show dialog with owner window handle to maintain proper z-order
            var result = ownerHandle != IntPtr.Zero
                ? dialog.ShowDialog(new WindowHandleWrapper(ownerHandle))
                : dialog.ShowDialog();

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
    }

    /// <summary>
    /// Get the initial path for the dialog, using remembered path if available
    /// Async to avoid blocking when loading settings
    /// </summary>
    private async Task<string> GetInitialPathAsync(FileDialogOptions? options)
    {
        // Priority:
        // 1. Remembered path (if RememberPathKey is set and we have a saved path)
        // 2. Explicitly provided DefaultPath
        // 3. My Documents folder

        if (!string.IsNullOrWhiteSpace(options?.RememberPathKey))
        {
            try
            {
                // Load settings asynchronously (no blocking!)
                var settings = await _systemSettings.GetSettingsAsync().ConfigureAwait(false);
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
                            var updatedSettings = await _systemSettings.GetSettingsAsync().ConfigureAwait(false);
                            updatedSettings.FileDialogPaths.Remove(options.RememberPathKey);
                            await _systemSettings.UpdateSettingsAsync(updatedSettings).ConfigureAwait(false);
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
    /// Called from STA thread context (Windows Forms dialog)
    /// Uses fire-and-forget pattern to avoid blocking the UI thread
    /// </summary>
    private void SaveLastUsedPath(FileDialogOptions? options, string? path)
    {
        if (string.IsNullOrWhiteSpace(options?.RememberPathKey) ||
            string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            return;
        }

        // Convert to relative path if within data folder for portability
        var pathToSave = _pathHelper.ToRelativePath(path) ?? path;

        // Fire-and-forget: Save asynchronously without blocking
        _ = Task.Run(async () =>
        {
            try
            {
                await _systemSettings.RememberFileDialogPathAsync(options.RememberPathKey, pathToSave).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save file dialog path: {ex.Message}", "FileDialogService");
            }
        });
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

/// <summary>
/// Wrapper class to use window handle as IWin32Window for cross-thread dialog ownership
/// </summary>
internal class WindowHandleWrapper : IWin32Window
{
    private readonly IntPtr _handle;

    public WindowHandleWrapper(IntPtr handle)
    {
        _handle = handle;
    }

    public IntPtr Handle => _handle;
}
