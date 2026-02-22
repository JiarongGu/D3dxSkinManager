using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Interface for Profile Management facade
/// Handles: PROFILE_GET_ALL, PROFILE_SWITCH, PROFILE_CREATE, etc.
/// Prefix: PROFILE_*
/// </summary>
public interface IProfileFacade : IModuleFacade
{
    Task<ProfileListResponse> GetAllProfilesAsync();
    Task<Profile?> GetActiveProfileAsync();
    Task<Profile?> GetProfileByIdAsync(string profileId);
    Task<Profile> CreateProfileAsync(CreateProfileRequest createRequest);
    Task<bool> UpdateProfileAsync(UpdateProfileRequest updateRequest);
    Task<bool> DeleteProfileAsync(string profileId);
    Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName);
    Task<string> ExportProfileConfigAsync(string profileId);
    Task<ProfileConfiguration?> GetProfileConfigAsync(string profileId);
    Task<bool> UpdateProfileConfigAsync(ProfileConfiguration config);
}


/// <summary>
/// Facade for profile management operations
/// Responsibility: Profile CRUD and switching
/// IPC Prefix: PROFILE_*
/// </summary>
public class ProfileFacade : BaseFacade, IProfileFacade
{
    protected override string ModuleName => "ProfileFacade";

    private readonly IProfileService _profileService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitter _eventEmitter;

    public ProfileFacade(
        IProfileService profileService,
        IPayloadHelper payloadHelper,
        IEventEmitter eventEmitter,
        ILogHelper logger) : base(logger)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // Profile operations
            "GET_ALL" => await GetAllProfilesAsync(),
            "GET_ACTIVE" => await GetActiveProfileAsync(),
            "GET_BY_ID" => await GetProfileByIdAsync(request),
            "CREATE" => await CreateProfileAsync(request),
            "UPDATE" => await UpdateProfileAsync(request),
            "DELETE" => await DeleteProfileAsync(request),
            "DUPLICATE" => await DuplicateProfileAsync(request),
            "SWITCH" => await SwitchProfileAsync(request),

            // Config operations
            "EXPORT_CONFIG" => await ExportProfileConfigAsync(request),
            "GET_CONFIG" => await GetProfileConfigAsync(request),
            "UPDATE_CONFIG" => await UpdateProfileConfigAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<ProfileListResponse> GetAllProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync().ConfigureAwait(false);
        var activeProfile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);

        return new ProfileListResponse
        {
            Profiles = profiles,
            ActiveProfileId = activeProfile?.Id ?? string.Empty
        };
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        return await _profileService.GetActiveProfileAsync().ConfigureAwait(false);
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        return await _profileService.GetProfileByIdAsync(profileId).ConfigureAwait(false);
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest createRequest)
    {
        var profile = await _profileService.CreateProfileAsync(createRequest).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(Core.Event.EventType.CustomEvent, "profile.created", profile).ConfigureAwait(false);

        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest updateRequest)
    {
        var success = await _profileService.UpdateProfileAsync(updateRequest).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(Core.Event.EventType.CustomEvent, "profile.updated", updateRequest).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        var success = await _profileService.DeleteProfileAsync(profileId).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(Core.Event.EventType.CustomEvent, "profile.deleted", new { ProfileId = profileId }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        var profile = await _profileService.DuplicateProfileAsync(sourceProfileId, newName).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(Core.Event.EventType.CustomEvent, "profile.duplicated", profile).ConfigureAwait(false);

        return profile;
    }

    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        return await _profileService.ExportProfileConfigAsync(profileId).ConfigureAwait(false);
    }

    public async Task<ProfileConfiguration?> GetProfileConfigAsync(string profileId)
    {
        return await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
    }

    public async Task<bool> UpdateProfileConfigAsync(ProfileConfiguration config)
    {
        return await _profileService.UpdateProfileConfigurationAsync(config).ConfigureAwait(false);
    }

    // Message request handlers

    private async Task<Profile?> GetProfileByIdAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await GetProfileByIdAsync(profileId).ConfigureAwait(false);
    }

    private async Task<Profile> CreateProfileAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var workDirectory = _payloadHelper.GetRequiredValue<string>(request.Payload, "workDirectory");
        var colorTag = _payloadHelper.GetOptionalValue<string>(request.Payload, "colorTag");
        var iconName = _payloadHelper.GetOptionalValue<string>(request.Payload, "iconName");
        var gameName = _payloadHelper.GetOptionalValue<string>(request.Payload, "gameName");
        var copyFromCurrent = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "copyFromCurrent") ?? false;

        var createRequest = new CreateProfileRequest
        {
            Name = name,
            Description = description,
            WorkDirectory = workDirectory,
            ColorTag = colorTag,
            IconName = iconName,
            GameName = gameName
        };

        return await CreateProfileAsync(createRequest).ConfigureAwait(false);
    }

    private async Task<bool> UpdateProfileAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var workDirectory = _payloadHelper.GetOptionalValue<string>(request.Payload, "workDirectory");
        var colorTag = _payloadHelper.GetOptionalValue<string>(request.Payload, "colorTag");
        var iconName = _payloadHelper.GetOptionalValue<string>(request.Payload, "iconName");
        var gameName = _payloadHelper.GetOptionalValue<string>(request.Payload, "gameName");

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

        return await UpdateProfileAsync(updateRequest).ConfigureAwait(false);
    }

    private async Task<bool> DeleteProfileAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await DeleteProfileAsync(profileId).ConfigureAwait(false);
    }

    private async Task<Profile> DuplicateProfileAsync(IpcRequest request)
    {
        var sourceProfileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourceProfileId");
        var newName = _payloadHelper.GetRequiredValue<string>(request.Payload, "newName");
        return await DuplicateProfileAsync(sourceProfileId, newName).ConfigureAwait(false);
    }

    private async Task<string> ExportProfileConfigAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await ExportProfileConfigAsync(profileId).ConfigureAwait(false);
    }

    private async Task<ProfileConfiguration?> GetProfileConfigAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        return await GetProfileConfigAsync(profileId).ConfigureAwait(false);
    }

    private async Task<bool> UpdateProfileConfigAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var archiveHandlingMode = _payloadHelper.GetOptionalValue<string>(request.Payload, "archiveHandlingMode");
        var defaultGrading = _payloadHelper.GetOptionalValue<string>(request.Payload, "defaultGrading");
        var autoGenerateThumbnails = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "autoGenerateThumbnails");
        var autoClassifyMods = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "autoClassifyMods");
        var thumbnailAlgorithm = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnailAlgorithm");
        var migotoVersion = _payloadHelper.GetOptionalValue<string>(request.Payload, "migotoVersion");
        var gamePath = _payloadHelper.GetOptionalValue<string>(request.Payload, "gamePath");
        var gameLaunchArgs = _payloadHelper.GetOptionalValue<string>(request.Payload, "gameLaunchArgs");
        var customProgramPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "customProgramPath");
        var customProgramArgs = _payloadHelper.GetOptionalValue<string>(request.Payload, "customProgramArgs");

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

        return await UpdateProfileConfigAsync(config).ConfigureAwait(false);
    }

    private async Task<ProfileListResponse> SwitchProfileAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");

        await _profileService.SwitchProfileAsync(profileId).ConfigureAwait(false);

        var activeProfile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);
        await _eventEmitter.EmitAsync(Core.Event.EventType.CustomEvent, "profile.switched", activeProfile).ConfigureAwait(false);

        // Return the same response as GET_ALL
        return await GetAllProfilesAsync().ConfigureAwait(false);
    }
}
