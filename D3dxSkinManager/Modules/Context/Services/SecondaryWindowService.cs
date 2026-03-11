using Microsoft.Web.WebView2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Core.WebView;
using D3dxSkinManager.Modules.Setting.Services;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Service for creating and managing secondary WebView2 windows
/// Note: This service is scoped to ProfileContext - profileId comes from IProfileContext
/// </summary>
public interface ISecondaryWindowService : IDisposable
{
    Task<Form?> CreateSecondaryWindowAsync(string windowName, string title, int defaultWidth, int defaultHeight, string htmlPage);
    void CloseAllWindows();
    bool HasWindow(string windowName);
    void CloseWindow(string windowName);
}

public class SecondaryWindowService : ISecondaryWindowService
{
    /// <summary>
    /// Represents an open secondary window entry
    /// </summary>
    private record WindowEntry(Form Form, WebViewSession Session, string WindowName);

    private readonly ILogHelper _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebViewSessionManager _sessionManager;
    private readonly ICustomSchemeHandler _schemeHandler;
    private readonly IProfileContext _profileContext;
    private readonly IProfileService _profileService;
    private readonly IAppEnvironment _appEnvironment;
    private readonly ConcurrentDictionary<string, WindowEntry> _openWindows = new();
    private int _windowCounter = 0;

    public SecondaryWindowService(
        ILogHelper logger,
        IServiceProvider serviceProvider,
        IWebViewSessionManager sessionManager,
        ICustomSchemeHandler schemeHandler,
        IProfileContext profileContext,
        IProfileService profileService,
        IAppEnvironment appEnvironment)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _sessionManager = sessionManager;
        _schemeHandler = schemeHandler;
        _profileContext = profileContext;
        _profileService = profileService;
        _appEnvironment = appEnvironment;
    }

    /// <summary>
    /// Generic method to create any secondary window with saved position/size
    /// </summary>
    public async Task<Form?> CreateSecondaryWindowAsync(
        string windowName,
        string title,
        int defaultWidth,
        int defaultHeight,
        string htmlPage)
    {
        try
        {
            var profileId = _profileContext.ProfileId;
            _logger.Info($"[SecondaryWindow] Creating '{windowName}' window for profile {profileId}");

            var sessionId = $"{windowName}_{++_windowCounter}";
            _logger.Info($"[SecondaryWindow] Session ID: {sessionId}");

            _logger.Info("[SecondaryWindow] Creating Form...");
            var form = new Form
            {
                Text = title,
                Size = new Size(defaultWidth, defaultHeight),
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                MaximizeBox = false,
                MinimizeBox = true,
                TopMost = true,
                ShowInTaskbar = true,
                Icon = null
            };
            _logger.Info("[SecondaryWindow] Form created");

            // Load saved position/size or use defaults
            _logger.Info("[SecondaryWindow] Loading window configuration...");
            var screen = Screen.PrimaryScreen!.WorkingArea;
            var (position, size) = await LoadWindowConfigurationAsync(windowName, defaultWidth, defaultHeight, screen).ConfigureAwait(false);
            form.Location = position;
            form.Size = size;
            _logger.Info($"[SecondaryWindow] Window configuration set: ({position.X}, {position.Y}) {size.Width}x{size.Height}");

            _logger.Info("[SecondaryWindow] Creating WebView2 control...");
            var webView = new WebView2
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 26, 26) // Prevent white flash
            };
            _logger.Info("[SecondaryWindow] WebView2 control created");

            _logger.Info("[SecondaryWindow] Adding WebView2 to form...");
            form.Controls.Add(webView);
            _logger.Info("[SecondaryWindow] WebView2 added to form");

            // Load theme from global settings for splash screen
            _logger.Info("[SecondaryWindow] Loading theme from settings...");
            bool isDarkTheme = true; // Default to dark
            try
            {
                var settingService = _serviceProvider.GetRequiredService<IGlobalSettingService>();
                var settings = await settingService.GetSettingsAsync().ConfigureAwait(false);
                isDarkTheme = settings.Theme == "dark";
                _logger.Info($"[SecondaryWindow] Using theme: {settings.Theme}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[SecondaryWindow] Failed to load theme from settings, using dark default: {ex.Message}");
            }

            // Create splash screen panel with theme from settings
            _logger.Info("[SecondaryWindow] Creating splash screen panel...");
            var splashScreenPanel = new SplashScreenPanel(isDarkTheme);
            splashScreenPanel.UpdateStatus("Initializing...");
            form.Controls.Add(splashScreenPanel);
            splashScreenPanel.BringToFront();
            _logger.Info($"[SecondaryWindow] Splash screen panel added (theme: {(isDarkTheme ? "dark" : "light")})");

            // Create WebView session with splash screen
            _logger.Info("[SecondaryWindow] Creating WebView session...");
            var session = _sessionManager.Create(sessionId, () =>
            {
                _logger.Info("[SecondaryWindow] WebView session factory called");
                var newSession = new WebViewSession(
                    sessionId,
                    webView,
                    _logger,
                    _serviceProvider,
                    _schemeHandler,
                    form,
                    splashScreenPanel  // Pass splash screen to session
                );
                _logger.Info("[SecondaryWindow] WebViewSession instance created");
                return newSession;
            });
            _logger.Info("[SecondaryWindow] WebView session registered with SessionManager");

            // Handle window closing - save position/size and cleanup
            _logger.Info("[SecondaryWindow] Attaching FormClosing event handler...");
            form.FormClosing += (s, e) =>
            {
                _logger.Info($"[SecondaryWindow] FormClosing event fired for {sessionId}");

                // Remove from dictionary
                if (_openWindows.TryRemove(windowName, out var windowEntry))
                {
                    _sessionManager.Remove(sessionId);

                    // Save window position/size asynchronously (fire and forget with proper error handling)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveWindowConfigurationAsync(
                                windowEntry.WindowName,
                                form.Location,
                                form.Size
                            ).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[SecondaryWindow] Error saving window configuration on close: {ex.Message}");
                        }
                    });
                }

                _logger.Info($"[SecondaryWindow] Window '{windowName}' closed: {sessionId}");
            };

            // Handle form load event - initialize WebView2 after form is shown
            _logger.Info("[SecondaryWindow] Attaching Load event handler...");
            form.Load += (s, e) =>
            {
                _logger.Info("[SecondaryWindow] Form Load event fired, initializing WebView2...");

                form.BeginInvoke(async () =>
                {
                    try
                    {
                        await session.StartAsync();
                        _logger.Info($"[SecondaryWindow] Session started, navigating to {htmlPage}...");

                        var url = _appEnvironment.IsDevelopment
                            ? $"http://localhost:3000/{htmlPage}"
                            : $"https://app.local/{htmlPage}";

                        _logger.Info($"[SecondaryWindow] Navigation URL: {url} (dev mode: {_appEnvironment.IsDevelopment})");
                        webView.CoreWebView2.Navigate(url);
                        _logger.Info("[SecondaryWindow] Navigation initiated");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[SecondaryWindow] Failed to start session: {ex.Message}");
                    }
                });
            };

            _logger.Info("[SecondaryWindow] Adding window to tracking dictionary...");
            var entry = new WindowEntry(form, session, windowName);
            if (!_openWindows.TryAdd(windowName, entry))
            {
                _logger.Warn($"[SecondaryWindow] Window '{windowName}' already exists, closing old one");
                CloseWindow(windowName);
                _openWindows.TryAdd(windowName, entry);
            }

            _logger.Info($"[SecondaryWindow] Window '{windowName}' created successfully: {sessionId}");
            return form;
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Failed to create '{windowName}' window: {ex.Message}");
            return null;
        }
    }

    public void CloseAllWindows()
    {
        foreach (var kvp in _openWindows.ToArray())
        {
            CloseWindow(kvp.Key);
        }
    }

    public bool HasWindow(string windowName)
    {
        return _openWindows.ContainsKey(windowName);
    }

    public void CloseWindow(string windowName)
    {
        if (_openWindows.TryGetValue(windowName, out var entry))
        {
            try
            {
                var form = entry.Form;
                // Need to invoke on the form's thread to close it
                // Use BeginInvoke (non-blocking) instead of Invoke (blocking) to prevent deadlock
                // during profile switching when called from IPC thread
                if (form.InvokeRequired)
                {
                    form.BeginInvoke(() => form.Close());
                }
                else
                {
                    form.Close();
                }
                _logger.Info($"[SecondaryWindow] Closed window: {windowName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SecondaryWindow] Error closing window '{windowName}': {ex.Message}");
            }
        }
    }


    /// <summary>
    /// Load window configuration (position and size) from profile config
    /// </summary>
    private async Task<(Point Position, Size Size)> LoadWindowConfigurationAsync(
        string windowName,
        int defaultWidth,
        int defaultHeight,
        Rectangle screen)
    {
        try
        {
            var profileId = _profileContext.ProfileId;
            _logger.Info($"[SecondaryWindow] Loading configuration for window '{windowName}'...");
            var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);

            if (config?.Windows != null && config.Windows.TryGetValue(windowName, out var windowConfig))
            {
                _logger.Info($"[SecondaryWindow] Found saved configuration for '{windowName}'");

                int width = windowConfig.Width ?? defaultWidth;
                int height = windowConfig.Height ?? defaultHeight;

                if (windowConfig.X != null && windowConfig.Y != null)
                {
                    int x = windowConfig.X.Value;
                    int y = windowConfig.Y.Value;
                    _logger.Info($"[SecondaryWindow] Saved config: ({x}, {y}) {width}x{height}");

                    // Validate position is on screen
                    if (IsPositionValid(x, y, width, height, screen))
                    {
                        _logger.Info($"[SecondaryWindow] Configuration is valid, using saved values");
                        return (new Point(x, y), new Size(width, height));
                    }
                    else
                    {
                        _logger.Info("[SecondaryWindow] Saved position is off-screen, using default");
                    }
                }
            }
            else
            {
                _logger.Info($"[SecondaryWindow] No saved configuration for '{windowName}'");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Exception loading window configuration: {ex.Message}");
            _logger.Error($"[SecondaryWindow] Stack trace: {ex.StackTrace}");
        }

        // Default to right-bottom corner with default size
        _logger.Info($"[SecondaryWindow] Using default configuration for screen: {screen}");
        int defaultX = screen.Right - defaultWidth - 20;
        int defaultY = screen.Bottom - defaultHeight - 20;
        _logger.Info($"[SecondaryWindow] Default: ({defaultX}, {defaultY}) {defaultWidth}x{defaultHeight}");
        return (new Point(defaultX, defaultY), new Size(defaultWidth, defaultHeight));
    }

    /// <summary>
    /// Save window configuration (position and size) to profile config
    /// </summary>
    private async Task SaveWindowConfigurationAsync(
        string windowName,
        Point location,
        Size size)
    {
        try
        {
            var profileId = _profileContext.ProfileId;
            _logger.Info($"[SecondaryWindow] Saving configuration for '{windowName}': ({location.X}, {location.Y}) {size.Width}x{size.Height}");

            await _profileService.UpdateWindowConfigurationAsync(
                profileId,
                windowName,
                location.X,
                location.Y,
                size.Width,
                size.Height
            ).ConfigureAwait(false);

            _logger.Info($"[SecondaryWindow] Saved window '{windowName}' configuration");
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Failed to save window configuration: {ex.Message}");
        }
    }

    private bool IsPositionValid(int x, int y, int width, int height, Rectangle screen)
    {
        // Check if at least part of the window is visible on screen
        Rectangle windowRect = new Rectangle(x, y, width, height);
        Rectangle intersection = Rectangle.Intersect(windowRect, screen);
        return intersection.Width > 100 && intersection.Height > 50; // At least 100x50 visible
    }

    public void Dispose()
    {
        _logger.Info("[SecondaryWindow] Disposing SecondaryWindowService, closing all windows...");
        CloseAllWindows();
    }
}
