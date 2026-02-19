using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Launch.Models;
using D3dxSkinManager.Modules.Launch.Services;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Launch;

/// <summary>
/// Facade for launch operations (3DMigoto and Game)
/// Responsibility: 3DMigoto version management and game launching
/// IPC Prefix: LAUNCH_*
/// </summary>
public class LaunchFacade : ILaunchFacade
{
    private readonly I3DMigotoService _d3dMigotoService;
    private readonly IProcessService _processService;
    private readonly IProfileService _profileService;
    private readonly IPayloadHelper _payloadHelper;

    public LaunchFacade(
        I3DMigotoService d3dMigotoService,
        IProcessService processService,
        IProfileService profileService,
        IPayloadHelper payloadHelper)
    {
        _d3dMigotoService = d3dMigotoService ?? throw new ArgumentNullException(nameof(d3dMigotoService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[LaunchFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
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

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LaunchFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    // 3DMigoto methods
    public async Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync()
    {
        return await _d3dMigotoService.GetAvailableVersionsAsync();
    }

    public async Task<string?> GetCurrentVersionAsync()
    {
        return await _d3dMigotoService.GetCurrentVersionAsync();
    }

    public async Task<DeploymentResult> DeployVersionAsync(string versionName)
    {
        return await _d3dMigotoService.DeployVersionAsync(versionName);
    }

    public async Task<bool> Launch3DMigotoAsync()
    {
        return await _d3dMigotoService.LaunchAsync();
    }

    // Game methods
    public async Task<bool> LaunchGameAsync(string? customArgs = null)
    {
        var profile = await _profileService.GetActiveProfileAsync();
        if (profile == null)
        {
            throw new InvalidOperationException("No active profile found");
        }

        var config = await _profileService.GetProfileConfigurationAsync(profile.Id);
        if (config == null || string.IsNullOrEmpty(config.GamePath))
        {
            throw new InvalidOperationException("Game path not configured in active profile");
        }

        var args = customArgs ?? config.GameLaunchArgs;
        await _processService.LaunchProcessAsync(config.GamePath, args, null);

        return true;
    }

    public async Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null)
    {
        await _processService.LaunchProcessAsync(programPath, arguments, null);
        return true;
    }

    // Private helper methods for message handling
    private async Task<DeploymentResult> DeployVersionAsync(MessageRequest request)
    {
        var versionName = _payloadHelper.GetRequiredValue<string>(request.Payload, "versionName");
        return await DeployVersionAsync(versionName);
    }

    private async Task<bool> LaunchGameAsync(MessageRequest request)
    {
        var customArgs = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchGameAsync(customArgs);
    }

    private async Task<bool> LaunchCustomProgramAsync(MessageRequest request)
    {
        var programPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "executablePath");
        var arguments = _payloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchCustomProgramAsync(programPath, arguments);
    }
}
