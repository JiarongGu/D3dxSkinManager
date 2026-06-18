using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Services;

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
    private readonly IPathHelper _pathHelper;
    private readonly IGlobalPathService _globalPathService;

    public ProfileFacade(
        IProfileService profileService,
        IPayloadHelper payloadHelper,
        IEventEmitter eventEmitter,
        IPathHelper pathHelper,
        IGlobalPathService globalPathService,
        ILogHelper logger) : base(logger)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
        _globalPathService = globalPathService ?? throw new ArgumentNullException(nameof(globalPathService));
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

            // Tab settings (per-profile)
            "UPDATE_MOD_PANEL_SIZE" => await UpdateModPanelSizeAsync(request),
            "UPDATE_CATEGORY_VIEW_MODE" => await UpdateCategoryViewModeAsync(request),
            "UPDATE_LOCKED_CATEGORIES" => await UpdateLockedCategoriesAsync(request),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<ProfileListResponse> GetAllProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync().ConfigureAwait(false);
        var activeProfile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);

        // Convert data directories to absolute paths for frontend display
        var profilesWithAbsolutePaths = profiles.Select(ConvertToAbsolutePaths).ToList();

        return new ProfileListResponse
        {
            Profiles = profilesWithAbsolutePaths,
            ActiveProfileId = activeProfile?.Id ?? string.Empty
        };
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        var profile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);
        return profile != null ? ConvertToAbsolutePaths(profile) : null;
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        var profile = await _profileService.GetProfileByIdAsync(profileId).ConfigureAwait(false);
        return profile != null ? ConvertToAbsolutePaths(profile) : null;
    }

    /// <summary>
    /// Convert profile paths from relative to absolute for frontend display
    /// </summary>
    private Profile ConvertToAbsolutePaths(Profile profile)
    {
        return new Profile
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            Color = profile.Color,
            GameName = profile.GameName,
            Thumbnail = profile.Thumbnail != null ? _pathHelper.ToAbsolutePath(profile.Thumbnail) ?? profile.Thumbnail : null
        };
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest createRequest)
    {
        var profile = await _profileService.CreateProfileAsync(createRequest).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CREATED, profile).ConfigureAwait(false);

        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest updateRequest)
    {
        var success = await _profileService.UpdateProfileAsync(updateRequest).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.UPDATED, updateRequest).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        var success = await _profileService.DeleteProfileAsync(profileId).ConfigureAwait(false);

        if (success)
        {
            await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.DELETED, new { ProfileId = profileId }).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<Profile> DuplicateProfileAsync(string sourceProfileId, string newName)
    {
        var profile = await _profileService.DuplicateProfileAsync(sourceProfileId, newName).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.DUPLICATED, profile).ConfigureAwait(false);

        return profile;
    }

    public async Task<string> ExportProfileConfigAsync(string profileId)
    {
        return await _profileService.ExportProfileConfigAsync(profileId).ConfigureAwait(false);
    }

    public async Task<ProfileConfiguration?> GetProfileConfigAsync(string profileId)
    {
        var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);

        // Add computed internal work directory path for display in UI
        if (config != null)
        {
            // Get the profile's data directory path from global path service
            var profileDataPath = _globalPathService.GetProfileDirectoryPath(profileId);
            var internalWorkPath = Path.Combine(profileDataPath, "work");

            // Add as a computed property that won't be persisted (JsonIgnore on property)
            // Frontend will use this to display internal path when mode is "internal"
            config.ModWork.InternalDirectory = internalWorkPath;
        }

        return config;
    }

    public async Task<bool> UpdateProfileConfigAsync(ProfileConfiguration config)
    {
        var result = await _profileService.UpdateProfileConfigurationAsync(config).ConfigureAwait(false);

        if (result)
        {
            // Emit event so profile-scoped services can react to config changes
            await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CONFIG_UPDATED, config).ConfigureAwait(false);
        }

        return result;
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
        var color = _payloadHelper.GetOptionalValue<string>(request.Payload, "color");
        var gameName = _payloadHelper.GetOptionalValue<string>(request.Payload, "gameName");
        var thumbnailPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnailPath");

        var createRequest = new CreateProfileRequest
        {
            Name = name,
            Description = description,
            Color = color,
            GameName = gameName,
            ThumbnailPath = thumbnailPath
        };

        return await CreateProfileAsync(createRequest).ConfigureAwait(false);
    }

    private async Task<bool> UpdateProfileAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var name = _payloadHelper.GetOptionalValue<string>(request.Payload, "name");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var color = _payloadHelper.GetOptionalValue<string>(request.Payload, "color");
        var gameName = _payloadHelper.GetOptionalValue<string>(request.Payload, "gameName");
        var thumbnailPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnailPath");

        var updateRequest = new UpdateProfileRequest
        {
            ProfileId = profileId,
            Name = name,
            Description = description,
            Color = color,
            GameName = gameName,
            ThumbnailPath = thumbnailPath
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

        // Mod work directory and cleanup configuration
        var workMode = _payloadHelper.GetOptionalValue<string>(request.Payload, "workMode");
        var workDirectory = _payloadHelper.GetOptionalValue<string>(request.Payload, "workDirectory");
        var cleanupEnabled = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "cleanupEnabled");
        var cleanupMaxCaches = _payloadHelper.GetOptionalValue<int?>(request.Payload, "cleanupMaxCaches");

        // Mod import configuration
        var compressionType = _payloadHelper.GetOptionalValue<string>(request.Payload, "compressionType");
        var compressionMode = _payloadHelper.GetOptionalValue<string>(request.Payload, "compressionMode");

        // Game launch configuration
        var launchPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "launchPath");
        var launchArgs = _payloadHelper.GetOptionalValue<string>(request.Payload, "launchArgs");

        // Load existing configuration to preserve fields like Windows and Tabs
        var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        if (config == null)
        {
            config = new ProfileConfiguration
            {
                ProfileId = profileId
            };
        }

        // Handle ModWork directory and cleanup configuration
        if (workMode != null || workDirectory != null || cleanupEnabled.HasValue || cleanupMaxCaches.HasValue)
        {
            var normalizedMode = (workMode ?? config.ModWork.Mode ?? "internal").ToLowerInvariant();
            var usesCustomDir = normalizedMode == "external" || normalizedMode == "xxmi";

            config.ModWork = new ModWorkConfiguration
            {
                // Normalize mode to lowercase for storage
                Mode = normalizedMode,
                // Store directory for external + xxmi modes (both use a custom work dir)
                Directory = usesCustomDir ? (workDirectory ?? config.ModWork.Directory) : null,
                // Update or preserve cleanup settings
                CleanupEnabled = cleanupEnabled ?? config.ModWork.CleanupEnabled,
                CleanupMaxCaches = cleanupMaxCaches.HasValue
                    ? Math.Max(1, Math.Min(100, cleanupMaxCaches.Value))  // Validate range: 1-100
                    : config.ModWork.CleanupMaxCaches
            };
        }

        // Handle Mod import configuration
        if (compressionType != null || compressionMode != null)
        {
            if (compressionType != null)
            {
                config.ModImport.CompressionType = compressionType;
            }
            if (compressionMode != null)
            {
                config.ModImport.CompressionMode = compressionMode;
            }
        }

        // Handle game launch configuration
        if (launchPath != null || launchArgs != null)
        {
            config.Launch.Path = launchPath ?? config.Launch.Path;
            config.Launch.Args = launchArgs ?? config.Launch.Args;
        }

        return await UpdateProfileConfigAsync(config).ConfigureAwait(false);
    }

    private async Task<ProfileListResponse> SwitchProfileAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");

        await _profileService.SwitchProfileAsync(profileId).ConfigureAwait(false);

        var activeProfile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.SWITCHED, activeProfile).ConfigureAwait(false);

        // Return the same response as GET_ALL
        return await GetAllProfilesAsync().ConfigureAwait(false);
    }

    private async Task<object> UpdateModPanelSizeAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var panelSize = _payloadHelper.GetRequiredValue<string>(request.Payload, "panelSize");

        // Delegate to service (which handles all business logic)
        await _profileService.UpdateModPanelSizeAsync(profileId, panelSize).ConfigureAwait(false);

        // Emit event to notify of config change
        var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CONFIG_UPDATED, config).ConfigureAwait(false);

        return new { success = true, message = "Mod panel size updated", config };
    }

    private async Task<object> UpdateCategoryViewModeAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var viewMode = _payloadHelper.GetRequiredValue<string>(request.Payload, "viewMode");

        await _profileService.UpdateCategoryViewModeAsync(profileId, viewMode).ConfigureAwait(false);

        var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        await _eventEmitter.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CONFIG_UPDATED, config).ConfigureAwait(false);

        return new { success = true, message = "Category view mode updated", config };
    }

    private async Task<object> UpdateLockedCategoriesAsync(IpcRequest request)
    {
        var profileId = _payloadHelper.GetRequiredValue<string>(request.Payload, "profileId");
        var lockedCategories = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "lockedCategories");

        await _profileService.UpdateLockedCategoriesAsync(profileId, lockedCategories).ConfigureAwait(false);

        return new { success = true, message = "Locked categories updated" };
    }
}
