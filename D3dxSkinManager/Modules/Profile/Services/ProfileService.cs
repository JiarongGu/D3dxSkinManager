using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Profiles.Models;

namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Manages profiles with isolated work directories, databases, and configurations.
/// </summary>
public interface IProfileService
{
    Task<List<Profile>> GetAllProfilesAsync();
    Task<Profile?> GetActiveProfileAsync();
    Task<Profile?> GetProfileByIdAsync(string profileId);
    Task<Profile> CreateProfileAsync(CreateProfileRequest request);
    Task<bool> UpdateProfileAsync(UpdateProfileRequest request);
    Task<bool> DeleteProfileAsync(string profileId);
    Task<bool> SwitchProfileAsync(string profileId);
    Task<Profile?> ImportProfileConfigAsync(string configJson, string workDirectory);
    Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId);
    Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config);
    Task<bool> UpdateWindowConfigurationAsync(string profileId, string windowName, int x, int y, int width, int height);
    Task<bool> UpdateModPanelSizeAsync(string profileId, string panelSize);
    Task<bool> UpdateCategoryViewModeAsync(string profileId, string viewMode);
    Task<bool> UpdateLockedCategoriesAsync(string profileId, List<string> lockedCategories);
}

public class ProfileService : IProfileService
{
    private readonly IGlobalPathService _globalPaths;
    private readonly IPathHelper _pathHelper;
    private readonly IHashHelper _hashHelper;
    private readonly IImageHelper _imageHelper;
    private readonly IFileHelper _fileService;
    private readonly IProfileRepository _repository;
    private readonly ILogHelper _logger;
    private readonly Lazy<Task> _init;

    public ProfileService(
        IGlobalPathService globalPaths,
        IFileHelper fileService,
        IPathHelper pathHelper,
        IHashHelper hashHelper,
        IImageHelper imageHelper,
        IProfileRepository repository,
        ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _pathHelper = pathHelper;
        _hashHelper = hashHelper;
        _imageHelper = imageHelper;
        _fileService = fileService;
        _repository = repository;
        _logger = logger;

        // Lazy initialization to avoid blocking constructor
        _init = new Lazy<Task>(EnsureInitialProfileExistsAsync, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    private async Task EnsureInitialProfileExistsAsync()
    {
        try
        {
            var profiles = await _repository.GetAllProfilesAsync().ConfigureAwait(false);
            if (profiles.Count == 0)
            {
                // No profiles found - create initial profile
                _logger.Info("No profiles found. Creating initial profile.", "ProfileService");
                // Use internal method to avoid circular Lazy<T> initialization
                await CreateProfileInternalAsync(new CreateProfileRequest
                {
                    Name = "My Profile",
                    Description = "My first profile",
                    Color = "#1890ff",
                    GameName = null // No game name for initial profile
                }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to ensure initial profile exists: {ex.Message}", "ProfileService", ex);
        }
    }

    public async Task<List<Profile>> GetAllProfilesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetAllProfilesAsync().ConfigureAwait(false);
    }

    public async Task<Profile?> GetActiveProfileAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var activeId = await _repository.GetActiveProfileIdAsync().ConfigureAwait(false);
        return await _repository.GetProfileAsync(activeId).ConfigureAwait(false);
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetProfileAsync(profileId).ConfigureAwait(false);
    }

    public async Task<Profile> CreateProfileAsync(CreateProfileRequest request)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await CreateProfileInternalAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal profile creation that doesn't call EnsureInitializedAsync
    /// Used during initialization to avoid circular Lazy dependency
    /// </summary>
    private async Task<Profile> CreateProfileInternalAsync(CreateProfileRequest request)
    {
        var profileId = Guid.NewGuid().ToString();
        var profileDataDir = _globalPaths.GetProfileDirectoryPath(profileId);
        var thumbnailsDir = _globalPaths.GetProfileThumbnailsDirectory(profileId);

        _logger.Info($"Creating profile: {request.Name}, ProfileId: {profileId}, ThumbnailPath: {request.ThumbnailPath}", "ProfileService");

        // Create profile directory first (needed for thumbnail copy)
        await _fileService.CreateDirectoryAsync(profileDataDir).ConfigureAwait(false);
        _logger.Info($"Created profile directory: {profileDataDir}", "ProfileService");

        // Handle thumbnail if provided - store in profile thumbnails folder
        string? relativeThumbnailPath = null;
        if (!string.IsNullOrEmpty(request.ThumbnailPath))
        {
            _logger.Info($"Processing thumbnail: {request.ThumbnailPath}", "ProfileService");

            // Create thumbnails subdirectory
            await _fileService.CreateDirectoryAsync(thumbnailsDir).ConfigureAwait(false);

            // Convert and save thumbnail as PNG for compatibility with Windows icons
            relativeThumbnailPath = await ConvertAndSaveThumbnailAsync(
                request.ThumbnailPath,
                thumbnailsDir
            ).ConfigureAwait(false);

            if (relativeThumbnailPath == null)
            {
                _logger.Warn($"Failed to process thumbnail", "ProfileService");
            }
            else
            {
                _logger.Info($"Thumbnail processed successfully: {relativeThumbnailPath}", "ProfileService");
            }
        }
        else
        {
            _logger.Info("No thumbnail provided for profile", "ProfileService");
        }

        var profile = new Profile
        {
            Id = profileId,
            Name = request.Name,
            Description = request.Description,
            Color = request.Color ?? GenerateRandomColor(),
            GameName = request.GameName,
            Thumbnail = relativeThumbnailPath
        };

        // Create profile in repository (this will save profile metadata to disk)
        await _repository.CreateProfileAsync(profile).ConfigureAwait(false);

        // Create default profile configuration
        var config = new ProfileConfiguration
        {
            ProfileId = profile.Id
        };
        await _repository.SaveProfileConfigurationAsync(profile.Id, config).ConfigureAwait(false);

        _logger.Info($"Created profile: {profile.Name} ({profile.Id})", "ProfileService");
        return profile;
    }

    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        _logger.Info($"Updating profile: {request.ProfileId}, Name: {request.Name}, ThumbnailPath: {request.ThumbnailPath}", "ProfileService");

        var profile = await _repository.GetProfileAsync(request.ProfileId).ConfigureAwait(false);
        if (profile == null)
        {
            _logger.Warn($"Profile not found: {request.ProfileId}", "ProfileService");
            return false;
        }

        // Update profile properties
        if (!string.IsNullOrEmpty(request.Name)) profile.Name = request.Name;
        if (request.Description != null) profile.Description = request.Description;
        if (request.Color != null) profile.Color = request.Color;
        if (request.GameName != null) profile.GameName = request.GameName;

        // Handle thumbnail update if provided
        if (request.ThumbnailPath != null)
        {
            // Empty string means remove thumbnail
            if (string.IsNullOrEmpty(request.ThumbnailPath))
            {
                _logger.Info($"Thumbnail removal requested", "ProfileService");
                profile.Thumbnail = null;
            }
            else
            {
                // Non-empty string means update thumbnail - store in profile thumbnails folder
                _logger.Info($"Thumbnail update requested: {request.ThumbnailPath}", "ProfileService");
                var profileDataDir = _globalPaths.GetProfileDirectoryPath(request.ProfileId);
                var thumbnailsDir = _globalPaths.GetProfileThumbnailsDirectory(request.ProfileId);

                // Ensure profile directory exists (should already exist, but be safe)
                await _fileService.CreateDirectoryAsync(profileDataDir).ConfigureAwait(false);
                _logger.Info($"Profile directory ensured: {profileDataDir}", "ProfileService");

                // Create thumbnails subdirectory
                await _fileService.CreateDirectoryAsync(thumbnailsDir).ConfigureAwait(false);

                // Convert and save thumbnail as PNG for compatibility with Windows icons
                var relativeThumbnailPath = await ConvertAndSaveThumbnailAsync(
                    request.ThumbnailPath,
                    thumbnailsDir
                ).ConfigureAwait(false);

                if (relativeThumbnailPath == null)
                {
                    _logger.Warn($"Failed to process thumbnail", "ProfileService");
                }
                else
                {
                    _logger.Info($"Thumbnail processed successfully: {relativeThumbnailPath}", "ProfileService");
                    profile.Thumbnail = relativeThumbnailPath;
                }
            }
        }
        else
        {
            _logger.Info("No thumbnail update requested (ThumbnailPath is null)", "ProfileService");
        }

        await _repository.UpdateProfileAsync(profile).ConfigureAwait(false);
        _logger.Info($"Updated profile: {profile.Name} ({profile.Id})", "ProfileService");
        return true;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var activeId = await _repository.GetActiveProfileIdAsync().ConfigureAwait(false);
        if (profileId == activeId)
        {
            throw new InvalidOperationException("Cannot delete the active profile. Please switch to another profile first.");
        }

        var profile = await _repository.GetProfileAsync(profileId).ConfigureAwait(false);
        if (profile == null)
        {
            return false;
        }

        // Delete profile via repository (handles directory deletion)
        await _repository.DeleteProfileAsync(profileId).ConfigureAwait(false);

        _logger.Info($"Deleted profile: {profile.Name} ({profile.Id})", "ProfileService");

        // Note: ProfileServiceProvider cleanup is handled by ProfileFacade
        // to avoid circular dependency (ProfileService cannot depend on IProfileServiceProvider)

        return true;
    }

    public async Task<bool> SwitchProfileAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        // Set the new active profile
        await _repository.SetActiveProfileIdAsync(profileId).ConfigureAwait(false);

        _logger.Info($"Switched to profile: {profileId}", "ProfileService");
        return true;
    }

    public async Task<Profile?> ImportProfileConfigAsync(string configJson, string workDirectory)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // TODO: Implement import logic
        // For now, return null to indicate the operation is not supported
        _logger.Warn("Profile import requested but this feature is not yet implemented", "Profiles");

        // Parse JSON, create profile with imported settings
        await Task.CompletedTask;
        return null;
    }

    public async Task<ProfileConfiguration?> GetProfileConfigurationAsync(string profileId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
    }

    public async Task<bool> UpdateProfileConfigurationAsync(ProfileConfiguration config)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _repository.SaveProfileConfigurationAsync(config.ProfileId, config).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> UpdateWindowConfigurationAsync(string profileId, string windowName, int x, int y, int width, int height)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // Get current configuration
        var config = await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        if (config == null)
        {
            _logger.Warn($"Profile configuration not found for {profileId}, creating new", "ProfileService");
            config = new ProfileConfiguration { ProfileId = profileId };
        }

        // Update the window configuration in the Windows dictionary
        config.Windows[windowName] = new WindowConfiguration
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        // Save via repository (handles locking and cache)
        await _repository.SaveProfileConfigurationAsync(profileId, config).ConfigureAwait(false);

        _logger.Verbose($"Updated window '{windowName}' configuration for profile {profileId}: ({x}, {y}, {width}x{height})", "ProfileService");
        return true;
    }

    public async Task<bool> UpdateModPanelSizeAsync(string profileId, string panelSize)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // Get current configuration
        var config = await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        if (config == null)
        {
            _logger.Warn($"Profile configuration not found for {profileId}, creating new", "ProfileService");
            config = new ProfileConfiguration { ProfileId = profileId };
        }

        // Update the mod panel size in the Tabs configuration
        config.Tabs.Mod.PanelSize = panelSize;

        // Save via repository (handles locking and cache)
        await _repository.SaveProfileConfigurationAsync(profileId, config).ConfigureAwait(false);

        _logger.Verbose($"Updated mod panel size for profile {profileId}: {panelSize}", "ProfileService");
        return true;
    }

    public async Task<bool> UpdateCategoryViewModeAsync(string profileId, string viewMode)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var config = await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        if (config == null)
        {
            _logger.Warn($"Profile configuration not found for {profileId}, creating new", "ProfileService");
            config = new ProfileConfiguration { ProfileId = profileId };
        }

        config.Tabs.Mod.CategoryViewMode = viewMode;

        await _repository.SaveProfileConfigurationAsync(profileId, config).ConfigureAwait(false);

        _logger.Verbose($"Updated category view mode for profile {profileId}: {viewMode}", "ProfileService");
        return true;
    }

    public async Task<bool> UpdateLockedCategoriesAsync(string profileId, List<string> lockedCategories)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var config = await _repository.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
        if (config == null)
        {
            _logger.Warn($"Profile configuration not found for {profileId}, creating new", "ProfileService");
            config = new ProfileConfiguration { ProfileId = profileId };
        }

        config.Tabs.Mod.LockedExpandedCategories = lockedCategories;

        await _repository.SaveProfileConfigurationAsync(profileId, config).ConfigureAwait(false);

        _logger.Verbose($"Updated locked categories for profile {profileId}: {lockedCategories.Count} categories", "ProfileService");
        return true;
    }

    /// <summary>
    /// Convert thumbnail image to PNG format and save it to the thumbnails directory.
    /// This ensures compatibility with Windows icon conversion.
    /// </summary>
    private async Task<string?> ConvertAndSaveThumbnailAsync(string sourcePath, string thumbnailsDir)
    {
        try
        {
            // Generate hash-based filename
            var hash = await _hashHelper.CalculateFileSHA256Async(sourcePath);

            // Use ImageHelper to convert to PNG
            var targetPath = await _imageHelper.ConvertToPngAsync(sourcePath, thumbnailsDir, hash);

            if (targetPath == null)
            {
                _logger.Error($"Failed to convert thumbnail using ImageHelper", "ProfileService");
                return null;
            }

            // Return relative path
            var relativePath = _pathHelper.ToRelativePath(targetPath);
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to convert thumbnail: {ex.Message}", "ProfileService", ex);
            return null;
        }
    }

    private string GenerateRandomColor()
    {
        var colors = new[] { "#1890ff", "#52c41a", "#faad14", "#f5222d", "#722ed1", "#13c2c2", "#eb2f96", "#fa8c16" };
        var random = new Random();
        return colors[random.Next(colors.Length)];
    }
}
