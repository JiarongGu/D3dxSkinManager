using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.System.Models;
using D3dxSkinManager.Modules.System.Services;

namespace D3dxSkinManager.Modules.System;

/// <summary>
/// Interface for System facade
/// Handles: File system operations, file dialogs, process launching, system settings
/// Prefix: SYSTEM_*
/// </summary>
public interface ISystemFacade : IModuleFacade
{
    // File System Operations
    Task OpenFileInExplorerAsync(string filePath);
    Task OpenDirectoryAsync(string directoryPath);
    Task OpenFileAsync(string filePath);
    Task<string> GetAbsolutePathAsync(string path);

    // File Dialogs
    Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null);
    Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null);
    Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null);

    // Process Operations
    Task LaunchProcessAsync(string path, string? args = null);

    // System Settings
    Task<SystemSettings> GetSystemSettingsAsync();
    Task UpdateSystemSettingsAsync(SystemSettings settings);
    Task ResetSystemSettingsAsync();

    // Screen Info
    Task<ScreenResolution> GetScreenResolutionAsync();
}

/// <summary>
/// Facade for system-level operations
/// Responsibility: File system operations, dialogs, path utilities, process launching, system settings
/// IPC Prefix: SYSTEM_*
/// </summary>
public class SystemFacade : BaseFacade, ISystemFacade
{
    protected override string ModuleName => "SystemFacade";

    private readonly ISystemFileService _fileSystemService;
    private readonly ISystemFileDialogService _fileDialogService;
    private readonly ISystemProcessService _processService;
    private readonly IPathHelper _pathHelper;
    private readonly IPayloadHelper _payloadHelper;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IProcessRegistry _processRegistry;

    public SystemFacade(
        IPathHelper pathHelper,
        IPayloadHelper payloadHelper,
        ISystemFileDialogService fileDialogService,
        ISystemProcessService processService,
        ISystemSettingsService systemSettingsService,
        ISystemFileService fileSystemService,
        IProcessRegistry processRegistry,
        ILogHelper logger) : base(logger)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _systemSettingsService = systemSettingsService ?? throw new ArgumentNullException(nameof(systemSettingsService));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // File system operations
            "OPEN_FILE" => await OpenFileAsync(request),
            "OPEN_DIRECTORY" => await OpenDirectoryAsync(request),
            "OPEN_FILE_IN_EXPLORER" => await OpenFileInExplorerAsync(request),
            "GET_ABSOLUTE_PATH" => await GetAbsolutePathAsync(request),
            "LAUNCH_PROCESS" => await LaunchProcessAsync(request),

            // File dialogs
            "OPEN_FILE_DIALOG" => await OpenFileDialogAsync(request),
            "OPEN_FOLDER_DIALOG" => await OpenFolderDialogAsync(request),
            "SAVE_FILE_DIALOG" => await SaveFileDialogAsync(request),

            // System settings
            "GET_SETTINGS" => await GetSystemSettingsHandlerAsync(request),
            "UPDATE_SETTINGS" => await UpdateSystemSettingsHandlerAsync(request),
            "RESET_SETTINGS" => await ResetSystemSettingsHandlerAsync(request),

            // Screen info
            "GET_SCREEN_RESOLUTION" => await GetScreenResolutionAsync(),

            // Long-running process registry (Activity panel / status bar)
            "GET_PROCESSES" => GetProcessesHandler(),
            "CANCEL_PROCESS" => CancelProcessHandler(request),
            "RESUME_PROCESS" => ResumeProcessHandler(request),
            "CLEAR_COMPLETED_PROCESSES" => ClearCompletedProcessesHandler(),

            // Frontend logging
            "LOG_FROM_FRONTEND" => LogFromFrontendHandler(request),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // ============================================
    // Public Methods
    // ============================================

    public async Task OpenFileInExplorerAsync(string filePath)
    {
        // Convert relative path to absolute for file system operations
        var absolutePath = _pathHelper.ToAbsolutePath(filePath) ?? filePath;

        if (!_fileSystemService.FileExists(absolutePath))
        {
            throw new InvalidOperationException($"File not found: {filePath}");
        }

        await _fileSystemService.OpenFileInExplorerAsync(absolutePath).ConfigureAwait(false);
    }

    public async Task OpenDirectoryAsync(string directoryPath)
    {
        // Convert relative path to absolute for file system operations
        var absolutePath = _pathHelper.ToAbsolutePath(directoryPath) ?? directoryPath;

        if (!_fileSystemService.DirectoryExists(absolutePath))
        {
            throw new InvalidOperationException($"Directory not found: {directoryPath}");
        }

        await _fileSystemService.OpenDirectoryAsync(absolutePath).ConfigureAwait(false);
    }

    public async Task OpenFileAsync(string filePath)
    {
        // Convert relative path to absolute for file system operations
        var absolutePath = _pathHelper.ToAbsolutePath(filePath) ?? filePath;

        if (!_fileSystemService.FileExists(absolutePath))
        {
            throw new InvalidOperationException($"File not found: {filePath}");
        }

        await _fileSystemService.OpenFileAsync(absolutePath).ConfigureAwait(false);
    }

    public async Task<string> GetAbsolutePathAsync(string path)
    {
        // Convert relative path to absolute, or return as-is if already absolute
        return await Task.FromResult(_pathHelper.ToAbsolutePath(path) ?? path);
    }

    public async Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.OpenFileDialogAsync(options).ConfigureAwait(false);
    }

    public async Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.OpenFolderDialogAsync(options).ConfigureAwait(false);
    }

    public async Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.SaveFileDialogAsync(options).ConfigureAwait(false);
    }

    public async Task LaunchProcessAsync(string path, string? args = null)
    {
        await _processService.LaunchProcessAsync(path, args).ConfigureAwait(false);
    }

    public async Task<SystemSettings> GetSystemSettingsAsync()
    {
        return await _systemSettingsService.GetSettingsAsync().ConfigureAwait(false);
    }

    public async Task UpdateSystemSettingsAsync(SystemSettings settings)
    {
        await _systemSettingsService.UpdateSettingsAsync(settings).ConfigureAwait(false);
    }

    public async Task ResetSystemSettingsAsync()
    {
        await _systemSettingsService.ResetSettingsAsync().ConfigureAwait(false);
    }

    // ============================================
    // Private IPC Handlers
    // ============================================

    private async Task<object> GetAbsolutePathAsync(IpcRequest request)
    {
        var path = _payloadHelper.GetRequiredValue<string>(request.Payload, "path");
        var absolutePath = await GetAbsolutePathAsync(path).ConfigureAwait(false);
        return new { success = true, absolutePath };
    }

    private async Task<object> OpenFileInExplorerAsync(IpcRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        await OpenFileInExplorerAsync(filePath).ConfigureAwait(false);
        return new { success = true, message = $"Opened file in explorer: {filePath}" };
    }

    private async Task<object> OpenDirectoryAsync(IpcRequest request)
    {
        var directoryPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "directoryPath");
        await OpenDirectoryAsync(directoryPath).ConfigureAwait(false);
        return new { success = true, message = $"Opened directory: {directoryPath}" };
    }

    private async Task<object> OpenFileAsync(IpcRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        await OpenFileAsync(filePath).ConfigureAwait(false);
        return new { success = true, message = $"Opened file: {filePath}" };
    }

    private async Task<object> LaunchProcessAsync(IpcRequest request)
    {
        var path = _payloadHelper.GetRequiredValue<string>(request.Payload, "path");
        var args = _payloadHelper.GetOptionalValue<string>(request.Payload, "args");
        await LaunchProcessAsync(path, args).ConfigureAwait(false);
        return new { success = true, message = $"Launched process: {path}" };
    }

    private async Task<object> OpenFileDialogAsync(IpcRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");
        var filters = _payloadHelper.GetOptionalValue<List<FileDialogFilter>>(request.Payload, "filters");
        var checkFileExists = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "checkFileExists");
        var checkPathExists = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "checkPathExists");
        var validateNames = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "validateNames");
        var fileName = _payloadHelper.GetOptionalValue<string>(request.Payload, "fileName");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            Filters = filters,
            RememberPathKey = rememberPathKey,
            CheckFileExists = checkFileExists,
            CheckPathExists = checkPathExists,
            ValidateNames = validateNames,
            FileName = fileName
        };

        return await OpenFileDialogAsync(options).ConfigureAwait(false);
    }

    private async Task<object> OpenFolderDialogAsync(IpcRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");
        var allowFileSelection = _payloadHelper.GetOptionalValue<bool>(request.Payload, "allowFileSelection");
        var filters = _payloadHelper.GetOptionalValue<List<FileDialogFilter>>(request.Payload, "filters");
        var checkFileExists = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "checkFileExists");
        var checkPathExists = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "checkPathExists");
        var validateNames = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "validateNames");
        var fileName = _payloadHelper.GetOptionalValue<string>(request.Payload, "fileName");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            RememberPathKey = rememberPathKey,
            AllowFileSelection = allowFileSelection,
            Filters = filters,
            CheckFileExists = checkFileExists,
            CheckPathExists = checkPathExists,
            ValidateNames = validateNames,
            FileName = fileName
        };

        return await OpenFolderDialogAsync(options).ConfigureAwait(false);
    }

    private async Task<object> SaveFileDialogAsync(IpcRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");
        var filters = _payloadHelper.GetOptionalValue<List<FileDialogFilter>>(request.Payload, "filters");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            Filters = filters,
            RememberPathKey = rememberPathKey
        };

        return await SaveFileDialogAsync(options).ConfigureAwait(false);
    }

    private async Task<SystemSettings> GetSystemSettingsHandlerAsync(IpcRequest request)
    {
        _logger.Debug("GetSystemSettingsHandlerAsync called", "SystemFacade");
        var result = await GetSystemSettingsAsync().ConfigureAwait(false);
        _logger.Debug($"System settings retrieved", "SystemFacade");
        return result;
    }

    private async Task<object> UpdateSystemSettingsHandlerAsync(IpcRequest request)
    {
        var settings = _payloadHelper.GetRequiredValue<SystemSettings>(request.Payload, "settings");
        await UpdateSystemSettingsAsync(settings).ConfigureAwait(false);
        return new { success = true, message = "System settings updated" };
    }

    private async Task<object> ResetSystemSettingsHandlerAsync(IpcRequest request)
    {
        await ResetSystemSettingsAsync().ConfigureAwait(false);
        var settings = await GetSystemSettingsAsync().ConfigureAwait(false);
        return new { success = true, message = "System settings reset to defaults", settings };
    }

    // ============================================
    // Process Registry Handlers (Activity panel / status bar)
    // ============================================

    private object GetProcessesHandler()
    {
        return new { processes = _processRegistry.GetAll() };
    }

    private object CancelProcessHandler(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        _processRegistry.Cancel(id);
        return new { success = true };
    }

    private object ResumeProcessHandler(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        _processRegistry.RequestResume(id);
        return new { success = true };
    }

    private object ClearCompletedProcessesHandler()
    {
        _processRegistry.ClearCompleted();
        return new { success = true };
    }

    // ============================================
    // Frontend Logging Handler
    // ============================================

    private object LogFromFrontendHandler(IpcRequest request)
    {
        try
        {
            var level = _payloadHelper.GetOptionalValue<string>(request.Payload, "level") ?? "INFO";
            var message = _payloadHelper.GetOptionalValue<string>(request.Payload, "message") ?? "";
            var source = _payloadHelper.GetOptionalValue<string>(request.Payload, "source") ?? "Frontend";

            // Parse log level from frontend (case-insensitive)
            var logLevel = level.ToUpperInvariant() switch
            {
                "ALL" => LogLevel.All,
                "DEBUG" => LogLevel.Debug,
                "INFO" => LogLevel.Info,
                "WARN" => LogLevel.Warn,
                "ERROR" => LogLevel.Error,
                "OFF" => LogLevel.Off,
                _ => LogLevel.Info
            };

            // Log to backend with [Frontend] prefix
            _logger.Log(logLevel, $"[Frontend] {message}", source);

            return new { success = true };
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to log frontend message: {ex.Message}", "SystemFacade", ex);
            return new { success = false, error = ex.Message };
        }
    }

    public Task<ScreenResolution> GetScreenResolutionAsync()
    {
        // Get primary screen resolution in logical (DPI-independent) pixels.
        // Screen.Bounds returns physical pixels; divide by DPI scale so the frontend
        // always works in logical pixel space (backend converts to physical when needed).
        var screen = global::System.Windows.Forms.Screen.PrimaryScreen;
        double dpiScale = DpiHelper.GetDpiScaleFactor();
        var resolution = new ScreenResolution
        {
            Width = (int)Math.Round(screen!.Bounds.Width / dpiScale),
            Height = (int)Math.Round(screen.Bounds.Height / dpiScale)
        };

        _logger.Debug($"[SystemFacade] Screen resolution: physical {screen.Bounds.Width}x{screen.Bounds.Height} -> logical {resolution.Width}x{resolution.Height} (DPI scale {dpiScale})");
        return Task.FromResult(resolution);
    }
}
