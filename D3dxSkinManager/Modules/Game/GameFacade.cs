using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Game;

/// <summary>
/// Facade for game launch operations
/// Responsibility: Game launching and custom program execution
/// IPC Prefix: GAME_*
/// </summary>
public class GameFacade : IGameFacade
{
    private readonly IProcessService _processService;
    private readonly IProfileService _profileService;

    public GameFacade(
        IProcessService processService,
        IProfileService profileService)
    {
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[GameFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                "LAUNCH" or "LAUNCH_PROCESS" => await LaunchGameAsync(request),
                "GAME_LAUNCH_CUSTOM" => await LaunchCustomProgramAsync(request),
                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

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

    private async Task<bool> LaunchGameAsync(MessageRequest request)
    {
        var customArgs = PayloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchGameAsync(customArgs);
    }

    private async Task<bool> LaunchCustomProgramAsync(MessageRequest request)
    {
        var programPath = PayloadHelper.GetRequiredValue<string>(request.Payload, "executablePath");
        var arguments = PayloadHelper.GetOptionalValue<string>(request.Payload, "arguments");
        return await LaunchCustomProgramAsync(programPath, arguments);
    }
}
