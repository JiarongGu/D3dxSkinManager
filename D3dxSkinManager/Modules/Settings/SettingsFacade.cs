using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Settings.Models;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Modules.Settings;

/// <summary>
/// Facade for settings and file system operations
/// Responsibility: App settings, file dialogs, file system operations
/// IPC Prefix: SETTINGS_*
/// </summary>
public class SettingsFacade : ISettingsFacade
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IProcessService _processService;
    private readonly IGlobalSettingsService _globalSettingsService;
    private readonly ISettingsFileService _settingsFileService;
    private readonly IPayloadHelper _payloadHelper;

    public SettingsFacade(
        IFileSystemService fileSystemService,
        IFileDialogService fileDialogService,
        IProcessService processService,
        IGlobalSettingsService globalSettingsService,
        ISettingsFileService settingsFileService,
        IPayloadHelper payloadHelper)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _globalSettingsService = globalSettingsService ?? throw new ArgumentNullException(nameof(globalSettingsService));
        _settingsFileService = settingsFileService ?? throw new ArgumentNullException(nameof(settingsFileService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[SettingsFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                // Global settings
                "GET_GLOBAL" => await GetGlobalSettingsHandlerAsync(request),
                "UPDATE_GLOBAL" => await UpdateGlobalSettingsHandlerAsync(request),
                "UPDATE_FIELD" => await UpdateGlobalSettingHandlerAsync(request),
                "RESET_GLOBAL" => await ResetGlobalSettingsHandlerAsync(request),

                // Settings files
                "GET_FILE" => await GetSettingsFileHandlerAsync(request),
                "SAVE_FILE" => await SaveSettingsFileHandlerAsync(request),
                "DELETE_FILE" => await DeleteSettingsFileHandlerAsync(request),
                "FILE_EXISTS" => await SettingsFileExistsHandlerAsync(request),
                "LIST_FILES" => await ListSettingsFilesHandlerAsync(request),

                // File system operations
                "OPEN_FILE" => await OpenFileAsync(request),
                "OPEN_DIRECTORY" => await OpenDirectoryAsync(request),
                "OPEN_FILE_IN_EXPLORER" => await OpenFileInExplorerAsync(request),
                "LAUNCH_PROCESS" => await LaunchProcessAsync(request),

                // File dialogs
                "OPEN_FILE_DIALOG" => await OpenFileDialogAsync(request),
                "OPEN_FOLDER_DIALOG" => await OpenFolderDialogAsync(request),
                "SAVE_FILE_DIALOG" => await SaveFileDialogAsync(request),

                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    public async Task<GlobalSettings> GetGlobalSettingsAsync()
    {
        return await _globalSettingsService.GetSettingsAsync();
    }

    public async Task UpdateGlobalSettingsAsync(GlobalSettings settings)
    {
        await _globalSettingsService.UpdateSettingsAsync(settings);
    }

    public async Task UpdateGlobalSettingAsync(string key, string value)
    {
        await _globalSettingsService.UpdateSettingAsync(key, value);
    }

    public async Task ResetGlobalSettingsAsync()
    {
        await _globalSettingsService.ResetSettingsAsync();
    }

    public async Task OpenFileInExplorerAsync(string filePath)
    {
        if (!_fileSystemService.FileExists(filePath))
        {
            throw new InvalidOperationException($"File not found: {filePath}");
        }

        await _fileSystemService.OpenFileInExplorerAsync(filePath);
    }

    public async Task OpenDirectoryAsync(string directoryPath)
    {
        if (!_fileSystemService.DirectoryExists(directoryPath))
        {
            throw new InvalidOperationException($"Directory not found: {directoryPath}");
        }

        await _fileSystemService.OpenDirectoryAsync(directoryPath);
    }

    public async Task OpenFileAsync(string filePath)
    {
        if (!_fileSystemService.FileExists(filePath))
        {
            throw new InvalidOperationException($"File not found: {filePath}");
        }

        await _fileSystemService.OpenFileAsync(filePath);
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
        var executablePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "executablePath");
        var arguments = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        var workingDirectory = _payloadHelper.GetOptionalValue<string>(request.Payload, "workingDirectory");

        await _processService.LaunchProcessAsync(executablePath, arguments, workingDirectory);

        return new { success = true, message = $"Launched process: {executablePath}" };
    }

    private async Task<FileDialogResult> OpenFileDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            Filters = null
        };

        return await OpenFileDialogAsync(options);
    }

    private async Task<FileDialogResult> OpenFolderDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath
        };

        return await OpenFolderDialogAsync(options);
    }

    private async Task<FileDialogResult> SaveFileDialogAsync(MessageRequest request)
    {
        var title = _payloadHelper.GetOptionalValue<string>(request.Payload, "title");
        var defaultPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultPath");

        var options = new FileDialogOptions
        {
            Title = title,
            DefaultPath = defaultPath,
            Filters = null
        };

        return await SaveFileDialogAsync(options);
    }

    private async Task<GlobalSettings> GetGlobalSettingsHandlerAsync(MessageRequest request)
    {
        Console.WriteLine($"[SettingsFacade] GetGlobalSettingsHandlerAsync called");
        var result = await GetGlobalSettingsAsync();
        Console.WriteLine($"[SettingsFacade] Settings retrieved: Theme={result.Theme}, LogLevel={result.LogLevel}");
        return result;
    }

    private async Task<object> UpdateGlobalSettingsHandlerAsync(MessageRequest request)
    {
        var theme = _payloadHelper.GetOptionalValue<string>(request.Payload, "theme");
        var annotationLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "annotationLevel");
        var logLevel = _payloadHelper.GetOptionalValue<string>(request.Payload, "logLevel");

        var settings = await GetGlobalSettingsAsync();

        if (theme != null) settings.Theme = theme;
        if (annotationLevel != null) settings.AnnotationLevel = annotationLevel;
        if (logLevel != null) settings.LogLevel = logLevel;

        await UpdateGlobalSettingsAsync(settings);

        return new { success = true, message = "Global settings updated", settings };
    }

    private async Task<object> UpdateGlobalSettingHandlerAsync(MessageRequest request)
    {
        var key = _payloadHelper.GetRequiredValue<string>(request.Payload, "key");
        var value = _payloadHelper.GetRequiredValue<string>(request.Payload, "value");

        await UpdateGlobalSettingAsync(key, value);

        return new { success = true, message = $"Setting '{key}' updated to '{value}'" };
    }

    private async Task<object> ResetGlobalSettingsHandlerAsync(MessageRequest request)
    {
        await ResetGlobalSettingsAsync();
        var settings = await GetGlobalSettingsAsync();

        return new { success = true, message = "Global settings reset to defaults", settings };
    }

    // Settings File Handlers

    private async Task<object> GetSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = await _settingsFileService.GetSettingsFileAsync(filename);

        if (content == null)
        {
            return new { success = false, message = $"Settings file not found: {filename}", content = (string?)null };
        }

        return new { success = true, content };
    }

    private async Task<object> SaveSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var content = _payloadHelper.GetRequiredValue<string>(request.Payload, "content");

        await _settingsFileService.SaveSettingsFileAsync(filename, content);

        return new { success = true, message = $"Settings file saved: {filename}" };
    }

    private async Task<object> DeleteSettingsFileHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");

        await _settingsFileService.DeleteSettingsFileAsync(filename);

        return new { success = true, message = $"Settings file deleted: {filename}" };
    }

    private async Task<object> SettingsFileExistsHandlerAsync(MessageRequest request)
    {
        var filename = _payloadHelper.GetRequiredValue<string>(request.Payload, "filename");
        var exists = await _settingsFileService.SettingsFileExistsAsync(filename);

        return new { exists };
    }

    private async Task<object> ListSettingsFilesHandlerAsync(MessageRequest request)
    {
        var files = await _settingsFileService.ListSettingsFilesAsync();

        return new { files };
    }
}
