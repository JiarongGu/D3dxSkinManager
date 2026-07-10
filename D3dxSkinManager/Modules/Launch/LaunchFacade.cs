using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Launch.Models;
using D3dxSkinManager.Modules.Launch.Services;
using D3dxSkinManager.Modules.System.Services;

namespace D3dxSkinManager.Modules.Launch;

/// <summary>
/// Interface for launch operations facade
/// </summary>
public interface ILaunchFacade : IModuleFacade
{

    // 3DMigoto methods
    Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync();
    Task<string?> GetCurrentVersionAsync();
    Task<DeploymentResult> DeployVersionAsync(string versionName);
    Task<bool> Launch3DMigotoAsync();

    // Game methods
    Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null);

    // XXMI methods
    Task<XxmiDetectResult> DetectXxmiAsync(string folderPath);
    Task<XxmiInstallerInfo> GetXxmiInstallerAsync();
}

/// <summary>
/// Facade for launch operations (3DMigoto and Game)
/// Responsibility: 3DMigoto version management and game launching
/// IPC Prefix: LAUNCH_*
/// </summary>
public class LaunchFacade : BaseFacade, ILaunchFacade
{
    protected override string ModuleName => "LaunchFacade";

    private readonly I3DMigotoService _d3dMigotoService;
    private readonly IXxmiService _xxmiService;
    private readonly ISystemProcessService _processService;
    private readonly IPayloadHelper _payloadHelper;

    public LaunchFacade(
        I3DMigotoService d3dMigotoService,
        IXxmiService xxmiService,
        ISystemProcessService processService,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _d3dMigotoService = d3dMigotoService ?? throw new ArgumentNullException(nameof(d3dMigotoService));
        _xxmiService = xxmiService ?? throw new ArgumentNullException(nameof(xxmiService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // 3DMigoto messages
            "LAUNCH_GET_VERSIONS" => await GetAvailableVersionsAsync(),
            "LAUNCH_GET_CURRENT" => await GetCurrentVersionAsync(),
            "LAUNCH_DEPLOY" => await DeployVersionAsync(request),
            "LAUNCH_3DMIGOTO" => await Launch3DMigotoAsync(),

            // Game messages
            "LAUNCH_CUSTOM" => await LaunchCustomProgramAsync(request),

            // XXMI messages
            "LAUNCH_XXMI_DETECT" => await DetectXxmiAsync(request),
            "LAUNCH_XXMI_INSTALLER_INFO" => await GetXxmiInstallerAsync(),
            // Fire-and-forget: acks immediately, progress via the Activity panel; the installer
            // opens itself when the download lands (background-task-tracking.md).
            "LAUNCH_XXMI_INSTALLER_DOWNLOAD" => StartXxmiInstallerDownload(request),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    // 3DMigoto methods
    public async Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync()
    {
        return await _d3dMigotoService.GetAvailableVersionsAsync().ConfigureAwait(false);
    }

    public async Task<string?> GetCurrentVersionAsync()
    {
        return await _d3dMigotoService.GetCurrentVersionAsync().ConfigureAwait(false);
    }

    public async Task<DeploymentResult> DeployVersionAsync(string versionName)
    {
        return await _d3dMigotoService.DeployVersionAsync(versionName).ConfigureAwait(false);
    }

    public async Task<bool> Launch3DMigotoAsync()
    {
        return await _d3dMigotoService.LaunchAsync().ConfigureAwait(false);
    }

    // Game methods
    public async Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null)
    {
        await _processService.LaunchProcessAsync(programPath, arguments, null).ConfigureAwait(false);
        return true;
    }

    // XXMI methods
    public async Task<XxmiDetectResult> DetectXxmiAsync(string folderPath)
    {
        return await _xxmiService.DetectAsync(folderPath).ConfigureAwait(false);
    }

    // Private helper methods for message handling
    private async Task<DeploymentResult> DeployVersionAsync(IpcRequest request)
    {
        var versionName = _payloadHelper.GetRequiredValue<string>(request.Payload, "versionName");
        return await DeployVersionAsync(versionName).ConfigureAwait(false);
    }

    private async Task<bool> LaunchCustomProgramAsync(IpcRequest request)
    {
        var programPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "executablePath");
        var arguments = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchCustomProgramAsync(programPath, arguments).ConfigureAwait(false);
    }

    private async Task<XxmiDetectResult> DetectXxmiAsync(IpcRequest request)
    {
        var folderPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "folderPath");
        return await DetectXxmiAsync(folderPath).ConfigureAwait(false);
    }

    public async Task<XxmiInstallerInfo> GetXxmiInstallerAsync()
    {
        return await _xxmiService.GetLatestInstallerAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// IPC handler for the XXMI installer download+open assist.
    /// IPC Message: LAUNCH_XXMI_INSTALLER_DOWNLOAD
    /// Payload: { version, fileName, sizeBytes, url } (the info LAUNCH_XXMI_INSTALLER_INFO returned;
    /// the service re-validates the url against the official release area before running anything).
    /// </summary>
    private object? StartXxmiInstallerDownload(IpcRequest request)
    {
        var info = new XxmiInstallerInfo
        {
            Version = _payloadHelper.GetRequiredValue<string>(request.Payload, "version"),
            FileName = _payloadHelper.GetRequiredValue<string>(request.Payload, "fileName"),
            SizeBytes = _payloadHelper.GetOptionalValue<long?>(request.Payload, "sizeBytes") ?? 0,
            Url = _payloadHelper.GetRequiredValue<string>(request.Payload, "url"),
        };
        _xxmiService.StartInstallerDownload(info);
        return new { started = true };
    }
}
