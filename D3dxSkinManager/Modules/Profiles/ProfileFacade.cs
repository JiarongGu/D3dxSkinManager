using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Facade for profile management operations
/// Responsibility: Profile CRUD and switching
/// IPC Prefix: PROFILE_*
/// </summary>
public class ProfileFacade : IProfileFacade
{
    private readonly IProfileService _profileService;
    private readonly PluginEventBus? _eventBus;

    public ProfileFacade(
        IProfileService profileService,
        PluginEventBus? eventBus = null)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _eventBus = eventBus;
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[ProfileFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                "GET_ALL" => await GetAllProfilesAsync(),
                "GET_ACTIVE" => await GetActiveProfileAsync(),
                "GET_BY_ID" => await GetProfileByIdAsync(request),
                "CREATE" => await CreateProfileAsync(request),
                "UPDATE" => await UpdateProfileAsync(request),
                "DELETE" => await DeleteProfileAsync(request),
                "SWITCH" => await SwitchProfileAsync(request),
                "DUPLICATE" => await DuplicateProfileAsync(request),
                "EXPORT_CONFIG" => await ExportProfileConfigAsync(request),
                "GET_CONFIG" => await GetProfileConfigAsync(request),
                "UPDATE_CONFIG" => await UpdateProfileConfigAsync(request),
                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProfileFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    public async Task<ProfileListResponse> GetAllProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync();
        var activeProfile = await _profileService.GetActiveProfileAsync();

        return new ProfileListResponse
        {
            Profiles = profiles,
            ActiveProfileId = activeProfile?.Id ?? string.Empty
        };
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        return await _profileService.GetActiveProfileAsync();
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        return await _profileService.GetProfileByIdAsync(profileId);
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest createRequest)
    {
        var profile = await _profileService.CreateProfileAsync(createRequest);

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "profile.created",
                Data = profile
            });
        }

        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest updateRequest)
    {
        var success = await _profileService.UpdateProfileAsync(updateRequest);

        if (success && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "profile.updated",
                Data = updateRequest
            });
        }

        return success;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        var success = await _profileService.DeleteProfileAsync(profileId);

        if (success && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "profile.deleted",
                Data = new { ProfileId = profileId }
            });
        }

        return success;
    }

    public async Task<ProfileSwitchResult> SwitchProfileAsync(string profileId)
    {
        var result = await _profileService.SwitchProfileAsync(profileId);

        if (result.Success && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "profile.switched",
                Data = result
            });
        }

        return result;
    }

    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        var profile = await _profileService.DuplicateProfileAsync(sourceProfileId, newName);

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "profile.duplicated",
                Data = profile
            });
        }

        return profile;
    }

    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        return await _profileService.ExportProfileConfigAsync(profileId);
    }

    public async Task<ProfileConfiguration?> GetProfileConfigAsync(string profileId)
    {
        return await _profileService.GetProfileConfigurationAsync(profileId);
    }

    public async Task<bool> UpdateProfileConfigAsync(ProfileConfiguration config)
    {
        return await _profileService.UpdateProfileConfigurationAsync(config);
    }

    // Message request handlers

    private async Task<Profile?> GetProfileByIdAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await GetProfileByIdAsync(profileId);
    }

    private async Task<Profile> CreateProfileAsync(MessageRequest request)
    {
        var name = PayloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var description = PayloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var workDirectory = PayloadHelper.GetRequiredValue<string>(request.Payload, "workDirectory");
        var colorTag = PayloadHelper.GetOptionalValue<string>(request.Payload, "colorTag");
        var iconName = PayloadHelper.GetOptionalValue<string>(request.Payload, "iconName");
        var gameName = PayloadHelper.GetOptionalValue<string>(request.Payload, "gameName");
        var copyFromCurrent = PayloadHelper.GetOptionalValue<bool?>(request.Payload, "copyFromCurrent") ?? false;

        var createRequest = new CreateProfileRequest
        {
            Name = name,
            Description = description,
            WorkDirectory = workDirectory,
            ColorTag = colorTag,
            IconName = iconName,
            GameName = gameName,
            CopyFromCurrent = copyFromCurrent
        };

        return await CreateProfileAsync(createRequest);
    }

    private async Task<bool> UpdateProfileAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var name = PayloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var description = PayloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var workDirectory = PayloadHelper.GetOptionalValue<string>(request.Payload, "workDirectory");
        var colorTag = PayloadHelper.GetOptionalValue<string>(request.Payload, "colorTag");
        var iconName = PayloadHelper.GetOptionalValue<string>(request.Payload, "iconName");
        var gameName = PayloadHelper.GetOptionalValue<string>(request.Payload, "gameName");

        var updateRequest = new UpdateProfileRequest
        {
            ProfileId = profileId,
            Name = name,
            Description = description,
            WorkDirectory = workDirectory,
            ColorTag = colorTag,
            IconName = iconName,
            GameName = gameName
        };

        return await UpdateProfileAsync(updateRequest);
    }

    private async Task<bool> DeleteProfileAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await DeleteProfileAsync(profileId);
    }

    private async Task<ProfileSwitchResult> SwitchProfileAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await SwitchProfileAsync(profileId);
    }

    private async Task<Profile> DuplicateProfileAsync(MessageRequest request)
    {
        var sourceProfileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "sourceProfileId");
        var newName = PayloadHelper.GetRequiredValue<string>(request.Payload, "newName");
        return await DuplicateProfileAsync(sourceProfileId, newName);
    }

    private async Task<string> ExportProfileConfigAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await ExportProfileConfigAsync(profileId);
    }

    private async Task<ProfileConfiguration?> GetProfileConfigAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await GetProfileConfigAsync(profileId);
    }

    private async Task<bool> UpdateProfileConfigAsync(MessageRequest request)
    {
        var profileId = PayloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var archiveHandlingMode = PayloadHelper.GetOptionalValue<string>(request.Payload, "archiveHandlingMode");
        var defaultGrading = PayloadHelper.GetOptionalValue<string>(request.Payload, "defaultGrading");
        var autoGenerateThumbnails = PayloadHelper.GetOptionalValue<bool?>(request.Payload, "autoGenerateThumbnails");
        var autoClassifyMods = PayloadHelper.GetOptionalValue<bool?>(request.Payload, "autoClassifyMods");
        var thumbnailAlgorithm = PayloadHelper.GetOptionalValue<string>(request.Payload, "thumbnailAlgorithm");
        var migotoVersion = PayloadHelper.GetOptionalValue<string>(request.Payload, "migotoVersion");
        var gamePath = PayloadHelper.GetOptionalValue<string>(request.Payload, "gamePath");
        var gameLaunchArgs = PayloadHelper.GetOptionalValue<string>(request.Payload, "gameLaunchArgs");
        var customProgramPath = PayloadHelper.GetOptionalValue<string>(request.Payload, "customProgramPath");
        var customProgramArgs = PayloadHelper.GetOptionalValue<string>(request.Payload, "customProgramArgs");

        var config = new ProfileConfiguration
        {
            ProfileId = profileId
        };

        if (archiveHandlingMode != null) config.ArchiveHandlingMode = archiveHandlingMode;
        if (defaultGrading != null) config.DefaultGrading = defaultGrading;
        if (autoGenerateThumbnails.HasValue) config.AutoGenerateThumbnails = autoGenerateThumbnails.Value;
        if (autoClassifyMods.HasValue) config.AutoClassifyMods = autoClassifyMods.Value;
        if (thumbnailAlgorithm != null) config.ThumbnailAlgorithm = thumbnailAlgorithm;
        if (migotoVersion != null) config.MigotoVersion = migotoVersion;
        if (gamePath != null) config.GamePath = gamePath;
        if (gameLaunchArgs != null) config.GameLaunchArgs = gameLaunchArgs;
        if (customProgramPath != null) config.CustomProgramPath = customProgramPath;
        if (customProgramArgs != null) config.CustomProgramArgs = customProgramArgs;

        return await UpdateProfileConfigAsync(config);
    }
}
