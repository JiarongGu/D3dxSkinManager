using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Launch.Models;
using D3dxSkinManager.Modules.Launch.Services;
using D3dxSkinManager.Modules.Profiles.Services;
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
    Task<bool> LaunchGameAsync(string? customArgs = null);
    Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null);
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
    private readonly ISystemProcessService _processService;
    private readonly IProfileService _profileService;
    private readonly IPayloadHelper _payloadHelper;

    public LaunchFacade(
        I3DMigotoService d3dMigotoService,
        ISystemProcessService processService,
        IProfileService profileService,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _d3dMigotoService = d3dMigotoService ?? throw new ArgumentNullException(nameof(d3dMigotoService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
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
            "LAUNCH_GAME" => await LaunchGameAsync(request),
            "LAUNCH_CUSTOM" => await LaunchCustomProgramAsync(request),

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
    public async Task<bool> LaunchGameAsync(string? customArgs = null)
    {
        var profile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);
        if (profile == null)
        {
            throw new InvalidOperationException("No active profile found");
        }

        // TODO: Game launch configuration has been moved out of profile config
        // Need to implement new game launch logic or store game path separately
        throw new NotImplementedException("Game launch configuration is no longer stored in profile config. Feature needs to be reimplemented.");
    }

    public async Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null)
    {
        await _processService.LaunchProcessAsync(programPath, arguments, null).ConfigureAwait(false);
        return true;
    }

    // Private helper methods for message handling
    private async Task<DeploymentResult> DeployVersionAsync(IpcRequest request)
    {
        var versionName = _payloadHelper.GetRequiredValue<string>(request.Payload, "versionName");
        return await DeployVersionAsync(versionName).ConfigureAwait(false);
    }

    private async Task<bool> LaunchGameAsync(IpcRequest request)
    {
        var customArgs = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchGameAsync(customArgs).ConfigureAwait(false);
    }

    private async Task<bool> LaunchCustomProgramAsync(IpcRequest request)
    {
        var programPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "executablePath");
        var arguments = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchCustomProgramAsync(programPath, arguments).ConfigureAwait(false);
    }
}
