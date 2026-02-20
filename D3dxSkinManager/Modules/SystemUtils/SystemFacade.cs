using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.SystemUtils.Models;
using D3dxSkinManager.Modules.SystemUtils.Services;

namespace D3dxSkinManager.Modules.SystemUtils;

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
}

/// <summary>
/// Facade for system-level operations
/// Responsibility: File system operations, dialogs, path utilities, process launching, system settings
/// IPC Prefix: SYSTEM_*
/// </summary>
public class SystemFacade : BaseFacade, ISystemFacade
{
    protected override string ModuleName => "SystemFacade";

    private readonly IFileSystemService _fileSystemService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IProcessService _processService;
    private readonly IPathHelper _pathHelper;
    private readonly IPayloadHelper _payloadHelper;
    private readonly ISystemSettingsService _systemSettingsService;

    public SystemFacade(
        IFileSystemService fileSystemService,
        IFileDialogService fileDialogService,
        IProcessService processService,
        IPathHelper pathHelper,
        IPayloadHelper payloadHelper,
        ISystemSettingsService systemSettingsService,
        ILogHelper logger) : base(logger)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _systemSettingsService = systemSettingsService ?? throw new ArgumentNullException(nameof(systemSettingsService));
    }

    protected override async Task<object?> RouteMessageAsync(MessageRequest request)
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

        await _fileSystemService.OpenFileInExplorerAsync(absolutePath);
    }

    public async Task OpenDirectoryAsync(string directoryPath)
    {
        // Convert relative path to absolute for file system operations
        var absolutePath = _pathHelper.ToAbsolutePath(directoryPath) ?? directoryPath;

        if (!_fileSystemService.DirectoryExists(absolutePath))
        {
            throw new InvalidOperationException($"Directory not found: {directoryPath}");
        }

        await _fileSystemService.OpenDirectoryAsync(absolutePath);
    }

    public async Task OpenFileAsync(string filePath)
    {
        // Convert relative path to absolute for file system operations
        var absolutePath = _pathHelper.ToAbsolutePath(filePath) ?? filePath;

        if (!_fileSystemService.FileExists(absolutePath))
        {
            throw new InvalidOperationException($"File not found: {filePath}");
        }

        await _fileSystemService.OpenFileAsync(absolutePath);
    }

    public async Task<string> GetAbsolutePathAsync(string path)
    {
        // Convert relative path to absolute, or return as-is if already absolute
        return await Task.FromResult(_pathHelper.ToAbsolutePath(path) ?? path);
    }

    public async Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.OpenFileDialogAsync(options);
    }

    public async Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.OpenFolderDialogAsync(options);
    }

    public async Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null)
    {
        return await _fileDialogService.SaveFileDialogAsync(options);
    }

    public async Task LaunchProcessAsync(string path, string? args = null)
    {
        await _processService.LaunchProcessAsync(path, args);
    }

    public async Task<SystemSettings> GetSystemSettingsAsync()
    {
        return await _systemSettingsService.GetSettingsAsync();
    }

    public async Task UpdateSystemSettingsAsync(SystemSettings settings)
    {
        await _systemSettingsService.UpdateSettingsAsync(settings);
    }

    public async Task ResetSystemSettingsAsync()
    {
        await _systemSettingsService.ResetSettingsAsync();
    }

    // ============================================
    // Private IPC Handlers
    // ============================================

    private async Task<object> GetAbsolutePathAsync(MessageRequest request)
    {
        var path = _payloadHelper.GetRequiredValue<string>(request.Payload, "path");
        var absolutePath = await GetAbsolutePathAsync(path);
        return new { success = true, absolutePath };
    }

    private async Task<object> OpenFileInExplorerAsync(MessageRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        await OpenFileInExplorerAsync(filePath);
        return new { success = true, message = $"Opened file in explorer: {filePath}" };
    }

    private async Task<object> OpenDirectoryAsync(MessageRequest request)
    {
        var directoryPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "directoryPath");
        await OpenDirectoryAsync(directoryPath);
        return new { success = true, message = $"Opened directory: {directoryPath}" };
    }

    private async Task<object> OpenFileAsync(MessageRequest request)
    {
        var filePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "filePath");
        await OpenFileAsync(filePath);
        return new { success = true, message = $"Opened file: {filePath}" };
    }

    private async Task<object> LaunchProcessAsync(MessageRequest request)
    {
        var path = _payloadHelper.GetRequiredValue<string>(request.Payload, "path");
        var args = _payloadHelper.GetOptionalValue<string>(request.Payload, "args");
        await LaunchProcessAsync(path, args);
        return new { success = true, message = $"Launched process: {path}" };
    }

    private async Task<object> OpenFileDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            Filters = null,
            RememberPathKey = rememberPathKey
        };

        return await OpenFileDialogAsync(options);
    }

    private async Task<object> OpenFolderDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            RememberPathKey = rememberPathKey
        };

        return await OpenFolderDialogAsync(options);
    }

    private async Task<object> SaveFileDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");
        var rememberPathKey = _payloadHelper.GetOptionalValue<string>(request.Payload, "rememberPathKey");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            RememberPathKey = rememberPathKey
        };

        return await SaveFileDialogAsync(options);
    }

    private async Task<SystemSettings> GetSystemSettingsHandlerAsync(MessageRequest request)
    {
        _logger.Debug("GetSystemSettingsHandlerAsync called", "SystemFacade");
        var result = await GetSystemSettingsAsync();
        _logger.Debug($"System settings retrieved", "SystemFacade");
        return result;
    }

    private async Task<object> UpdateSystemSettingsHandlerAsync(MessageRequest request)
    {
        var settings = _payloadHelper.GetRequiredValue<SystemSettings>(request.Payload, "settings");
        await UpdateSystemSettingsAsync(settings);
        return new { success = true, message = "System settings updated" };
    }

    private async Task<object> ResetSystemSettingsHandlerAsync(MessageRequest request)
    {
        await ResetSystemSettingsAsync();
        var settings = await GetSystemSettingsAsync();
        return new { success = true, message = "System settings reset to defaults", settings };
    }
}
