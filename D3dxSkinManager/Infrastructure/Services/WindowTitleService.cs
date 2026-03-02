using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Infrastructure.Services;

/// <summary>
/// Service that updates the window title and icon based on the active profile
/// </summary>
public interface IWindowTitleService
{
    void Initialize(Form mainForm);
    Task UpdateTitleAsync();
}

public class WindowTitleService : IWindowTitleService
{
    private readonly IProfileService _profileService;
    private readonly IGlobalPathService _globalPathService;
    private readonly IPathHelper _pathHelper;
    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;
    private Form? _mainForm;
    private Icon? _defaultIcon;
    private Icon? _currentCustomIcon;
    private const string APP_NAME = "D3dxSkinManager";

    public WindowTitleService(
        IProfileService profileService,
        IGlobalPathService globalPathService,
        IPathHelper pathHelper,
        IEventBus eventBus,
        ILogHelper logger)
    {
        _profileService = profileService;
        _globalPathService = globalPathService;
        _pathHelper = pathHelper;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void Initialize(Form mainForm)
    {
        _mainForm = mainForm;

        // Store the default icon so we can restore it later
        _defaultIcon = _mainForm.Icon;

        // Subscribe to profile switch events
        _eventBus.RegisterHandler(ModuleNames.PROFILE, ProfileEvents.SWITCHED, async (eventMessage) =>
        {
            await UpdateTitleAsync().ConfigureAwait(false);
        });

        // Subscribe to profile update events to refresh title/icon when profile changes
        _eventBus.RegisterHandler(ModuleNames.PROFILE, ProfileEvents.UPDATED, async (eventMessage) =>
        {
            await UpdateTitleAsync().ConfigureAwait(false);
        });

        _logger.Info("WindowTitleService initialized", "WindowTitleService");
    }

    public async Task UpdateTitleAsync()
    {
        if (_mainForm == null)
        {
            _logger.Warn("Cannot update title: MainForm not initialized", "WindowTitleService");
            return;
        }

        try
        {
            var activeProfile = await _profileService.GetActiveProfileAsync().ConfigureAwait(false);

            // Update title on UI thread
            if (_mainForm.InvokeRequired)
            {
                _mainForm.Invoke(() => SetTitle(activeProfile));
            }
            else
            {
                SetTitle(activeProfile);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update window title: {ex.Message}", "WindowTitleService");
        }
    }

    private void SetTitle(Profile? activeProfile)
    {
        if (_mainForm == null) return;

        if (activeProfile != null)
        {
            // Set title
            _mainForm.Text = $"{APP_NAME} - {activeProfile.Name}";

            // Set icon from thumbnail if available
            if (!string.IsNullOrEmpty(activeProfile.Thumbnail))
            {
                try
                {
                    // Convert relative path to absolute using PathHelper
                    var thumbnailPath = _pathHelper.ToAbsolutePath(activeProfile.Thumbnail);

                    if (string.IsNullOrEmpty(thumbnailPath))
                    {
                        _logger.Warn($"Failed to resolve thumbnail path: {activeProfile.Thumbnail}", "WindowTitleService");
                        RestoreDefaultIcon();
                        return;
                    }

                    _logger.Info($"Attempting to load icon from: {thumbnailPath}", "WindowTitleService");

                    if (File.Exists(thumbnailPath))
                    {
                        // Dispose previous custom icon if it exists
                        if (_currentCustomIcon != null)
                        {
                            _currentCustomIcon.Dispose();
                            _currentCustomIcon = null;
                        }

                        // Load image using FileStream to avoid file locking issues
                        using var fileStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var originalImage = Image.FromStream(fileStream);

                        // Create a new bitmap from the image to ensure we have a valid format
                        using var tempBitmap = new Bitmap(originalImage);

                        // Resize to icon size
                        var resizedBitmap = new Bitmap(tempBitmap, new Size(32, 32));
                        var iconHandle = resizedBitmap.GetHicon();

                        // Create icon and keep reference (don't dispose resizedBitmap yet)
                        _currentCustomIcon = Icon.FromHandle(iconHandle);
                        _mainForm.Icon = _currentCustomIcon;

                        _logger.Info($"Successfully updated window icon from thumbnail (size: {originalImage.Width}x{originalImage.Height})", "WindowTitleService");
                    }
                    else
                    {
                        _logger.Warn($"Thumbnail file not found: {thumbnailPath}", "WindowTitleService");
                        RestoreDefaultIcon();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to load thumbnail as icon from '{activeProfile.Thumbnail}': {ex.GetType().Name}: {ex.Message}", "WindowTitleService", ex);
                    _logger.Info($"Resolved path was: {_pathHelper.ToAbsolutePath(activeProfile.Thumbnail)}", "WindowTitleService");
                    RestoreDefaultIcon();
                }
            }
            else
            {
                _logger.Info("Profile has no thumbnail, restoring default icon", "WindowTitleService");
                RestoreDefaultIcon();
            }

            _logger.Info($"Updated window title: {_mainForm.Text}", "WindowTitleService");
        }
        else
        {
            _mainForm.Text = APP_NAME;
            RestoreDefaultIcon();
            _logger.Info($"Reset window title and icon to default", "WindowTitleService");
        }
    }

    private void RestoreDefaultIcon()
    {
        if (_mainForm != null && _defaultIcon != null)
        {
            // Dispose custom icon before restoring default
            if (_currentCustomIcon != null)
            {
                _currentCustomIcon.Dispose();
                _currentCustomIcon = null;
            }

            _mainForm.Icon = _defaultIcon;
            _logger.Info("Restored default window icon", "WindowTitleService");
        }
    }
}
