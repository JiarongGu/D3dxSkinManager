using Microsoft.Web.WebView2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Core.WebView;
using D3dxSkinManager.Modules.Setting.Services;
using D3dxSkinManager.Modules.Core.Utilities;
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

                    // Save window position/size synchronously to avoid disposal issues
                    // During app shutdown or profile switch, async saves may fail due to disposed services
                    try
                    {
                        SaveWindowConfigurationAsync(
                            windowEntry.WindowName,
                            form.Location,
                            form.Size
                        ).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        // Services already disposed (app shutdown or profile switch) - this is OK
                        _logger.Info($"[SecondaryWindow] Services disposed during window close (expected during shutdown): {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[SecondaryWindow] Error saving window configuration on close: {ex.Message}");
                    }
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
    /// Config stores logical pixels (DPI-independent), we convert to physical pixels for Form
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
            double currentDpi = DpiHelper.GetDpiScaleFactor();
            _logger.Info($"[SecondaryWindow] Loading configuration for window '{windowName}', current DPI: {currentDpi:F2}");
            var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);

            if (config?.Windows != null && config.Windows.TryGetValue(windowName, out var windowConfig))
            {
                _logger.Info($"[SecondaryWindow] Found saved configuration for '{windowName}'");

                // Get saved values (in logical pixels or old physical pixels)
                int savedWidth = windowConfig.Width ?? defaultWidth;
                int savedHeight = windowConfig.Height ?? defaultHeight;

                // Check if values were saved from OLD system (physical pixels)
                // If SavedDpiScale exists, these are OLD physical pixels that need conversion
                int logicalWidth, logicalHeight;
                if (windowConfig.SavedDpiScale.HasValue)
                {
                    // Old config: physical pixels saved, convert to logical first
                    double oldDpi = windowConfig.SavedDpiScale.Value;
                    logicalWidth = (int)Math.Round(savedWidth / oldDpi);
                    logicalHeight = (int)Math.Round(savedHeight / oldDpi);
                    _logger.Info($"[SecondaryWindow] Migrated old config from DPI {oldDpi:F2}: {savedWidth}x{savedHeight} -> {logicalWidth}x{logicalHeight} logical");
                }
                else
                {
                    // New config: already logical pixels
                    logicalWidth = savedWidth;
                    logicalHeight = savedHeight;
                    _logger.Info($"[SecondaryWindow] Loaded logical pixels: {logicalWidth}x{logicalHeight}");
                }

                // Enforce minimum sizes in logical pixels
                logicalWidth = Math.Max(logicalWidth, 300);
                logicalHeight = Math.Max(logicalHeight, 200);

                // Convert FROM logical pixels TO physical pixels for the form
                int physicalWidth = (int)Math.Round(logicalWidth * currentDpi);
                int physicalHeight = (int)Math.Round(logicalHeight * currentDpi);
                _logger.Info($"[SecondaryWindow] Physical size for form: {physicalWidth}x{physicalHeight}");

                if (windowConfig.X != null && windowConfig.Y != null)
                {
                    int savedX = windowConfig.X.Value;
                    int savedY = windowConfig.Y.Value;

                    int logicalX, logicalY;
                    if (windowConfig.SavedDpiScale.HasValue)
                    {
                        // Old config: convert old physical to logical
                        double oldDpi = windowConfig.SavedDpiScale.Value;
                        logicalX = (int)Math.Round(savedX / oldDpi);
                        logicalY = (int)Math.Round(savedY / oldDpi);
                    }
                    else
                    {
                        // New config: already logical
                        logicalX = savedX;
                        logicalY = savedY;
                    }

                    // Convert FROM logical TO physical for the form
                    int physicalX = (int)Math.Round(logicalX * currentDpi);
                    int physicalY = (int)Math.Round(logicalY * currentDpi);

                    _logger.Info($"[SecondaryWindow] Logical position: ({logicalX}, {logicalY})");
                    _logger.Info($"[SecondaryWindow] Physical position: ({physicalX}, {physicalY})");

                    // Validate position is on screen (using physical pixels)
                    if (IsPositionValid(physicalX, physicalY, physicalWidth, physicalHeight, screen))
                    {
                        _logger.Info($"[SecondaryWindow] Configuration is valid, using loaded values");
                        return (new Point(physicalX, physicalY), new Size(physicalWidth, physicalHeight));
                    }
                    else
                    {
                        _logger.Info("[SecondaryWindow] Saved position is off-screen, using default");
                    }
                }
                else
                {
                    // No position saved, but we have valid size - use default position with loaded size
                    int posX = screen.Right - physicalWidth - 20;
                    int posY = screen.Bottom - physicalHeight - 20;
                    _logger.Info($"[SecondaryWindow] Using default position with loaded size: ({posX}, {posY}) {physicalWidth}x{physicalHeight}");
                    return (new Point(posX, posY), new Size(physicalWidth, physicalHeight));
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

        // Default to right-bottom corner with default size (convert default logical to physical)
        double dpi = DpiHelper.GetDpiScaleFactor();
        int physicalDefaultWidth = (int)Math.Round(defaultWidth * dpi);
        int physicalDefaultHeight = (int)Math.Round(defaultHeight * dpi);
        int defaultX = screen.Right - physicalDefaultWidth - 20;
        int defaultY = screen.Bottom - physicalDefaultHeight - 20;
        _logger.Info($"[SecondaryWindow] Using default: ({defaultX}, {defaultY}) {physicalDefaultWidth}x{physicalDefaultHeight} (physical pixels)");
        return (new Point(defaultX, defaultY), new Size(physicalDefaultWidth, physicalDefaultHeight));
    }

    /// <summary>
    /// Save window configuration (position and size) to profile config
    /// Form.Location and Form.Size are in PHYSICAL pixels - we convert to logical (DPI-independent)
    /// </summary>
    private async Task SaveWindowConfigurationAsync(
        string windowName,
        Point location,
        Size size)
    {
        try
        {
            var profileId = _profileContext.ProfileId;
            double currentDpi = DpiHelper.GetDpiScaleFactor();

            _logger.Info($"[SecondaryWindow] Saving configuration for '{windowName}'");
            _logger.Info($"[SecondaryWindow] Physical pixels: ({location.X}, {location.Y}) {size.Width}x{size.Height}, DPI: {currentDpi:F2}");

            // Convert FROM physical pixels TO logical pixels (DPI-independent)
            int logicalX = (int)Math.Round(location.X / currentDpi);
            int logicalY = (int)Math.Round(location.Y / currentDpi);
            int logicalWidth = (int)Math.Round(size.Width / currentDpi);
            int logicalHeight = (int)Math.Round(size.Height / currentDpi);

            _logger.Info($"[SecondaryWindow] Logical pixels: ({logicalX}, {logicalY}) {logicalWidth}x{logicalHeight}");

            await _profileService.UpdateWindowConfigurationAsync(
                profileId,
                windowName,
                logicalX,
                logicalY,
                logicalWidth,
                logicalHeight
            ).ConfigureAwait(false);

            _logger.Info($"[SecondaryWindow] Saved window '{windowName}' configuration as logical pixels");
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
