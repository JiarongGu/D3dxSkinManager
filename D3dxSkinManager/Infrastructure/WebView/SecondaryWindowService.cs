using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Context.Services;
using System.Text.Json;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Service for creating and managing secondary WebView2 windows
/// </summary>
public interface ISecondaryWindowService : IDisposable
{
    Task<Form?> CreateCaptureWindowAsync(string profileId);
    void CloseAllWindows();
    bool HasWindowForProfile(string profileId);
    void CloseWindowForProfile(string profileId);
}

public class SecondaryWindowService : ISecondaryWindowService
{
    private readonly ILogHelper _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebViewSessionManager _sessionManager;
    private readonly ICustomSchemeHandler _schemeHandler;
    private readonly IProfilePathService _profilePathService;
    private readonly IAppEnvironment _appEnvironment;
    private readonly List<(Form Form, WebViewSession Session, string ProfileId)> _openWindows = new();
    private int _windowCounter = 0;

    public SecondaryWindowService(
        ILogHelper logger,
        IServiceProvider serviceProvider,
        IWebViewSessionManager sessionManager,
        ICustomSchemeHandler schemeHandler,
        IProfilePathService profilePathService,
        IAppEnvironment appEnvironment)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _sessionManager = sessionManager;
        _schemeHandler = schemeHandler;
        _profilePathService = profilePathService;
        _appEnvironment = appEnvironment;
    }

    public async Task<Form?> CreateCaptureWindowAsync(string profileId)
    {
        try
        {
            _logger.Info($"[SecondaryWindow] Creating capture window for profile {profileId}");

            var sessionId = $"capture_{++_windowCounter}";
            _logger.Info($"[SecondaryWindow] Session ID: {sessionId}");

            _logger.Info("[SecondaryWindow] Creating Form...");
            var form = new Form
            {
                Text = "Screen Capture",
                Size = new Size(300, 210), // Compact size
                StartPosition = FormStartPosition.Manual,
                FormBorderStyle = FormBorderStyle.FixedToolWindow, // Slim style with close only
                MaximizeBox = false, // No maximize button
                MinimizeBox = true,  // Keep minimize button
                TopMost = true, // Always on top of all windows
                ShowInTaskbar = true,
                Icon = null // TODO: Add icon if needed
            };
            _logger.Info("[SecondaryWindow] Form created");

            // Load saved position or default to right-bottom corner
            _logger.Info("[SecondaryWindow] Loading window position...");
            var screen = Screen.PrimaryScreen!.WorkingArea;
            Point position = await LoadWindowPositionAsync(profileId, form.Width, form.Height, screen);
            form.Location = position;
            _logger.Info($"[SecondaryWindow] Window position set to: ({position.X}, {position.Y})");

            _logger.Info("[SecondaryWindow] Creating WebView2 control...");
            var webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            _logger.Info("[SecondaryWindow] WebView2 control created");

            _logger.Info("[SecondaryWindow] Adding WebView2 to form...");
            form.Controls.Add(webView);
            _logger.Info("[SecondaryWindow] WebView2 added to form");

            // Create WebView session (this handles WebView2 initialization and IPC)
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
                    form
                );
                _logger.Info("[SecondaryWindow] WebViewSession instance created");
                return newSession;
            });
            _logger.Info("[SecondaryWindow] WebView session registered with SessionManager");

            // Handle window closing - save position and cleanup
            _logger.Info("[SecondaryWindow] Attaching FormClosing event handler...");
            form.FormClosing += (s, e) =>
            {
                _logger.Info($"[SecondaryWindow] FormClosing event fired for {sessionId}");
                var windowEntry = _openWindows.FirstOrDefault(w => w.Form == form);
                if (windowEntry != default)
                {
                    _openWindows.Remove(windowEntry);
                    _sessionManager.Remove(sessionId);

                    // Save window position synchronously
                    SaveWindowPositionAsync(windowEntry.ProfileId, form.Location).GetAwaiter().GetResult();

                    // Close the capture overlay when control panel closes
                    try
                    {
                        var captureService = _serviceProvider.GetService(typeof(D3dxSkinManager.Modules.Tool.ScreenCapture.Services.IScreenCaptureService))
                            as D3dxSkinManager.Modules.Tool.ScreenCapture.Services.IScreenCaptureService;
                        if (captureService != null && captureService.IsBorderOverlayVisible)
                        {
                            _logger.Info("[SecondaryWindow] Closing capture overlay");
                            captureService.HideBorderOverlayAsync().GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[SecondaryWindow] Failed to close capture overlay: {ex.Message}");
                    }
                }

                _logger.Info($"[SecondaryWindow] Capture window closed: {sessionId}");
            };

            // Handle form load event - initialize WebView2 after form is shown
            _logger.Info("[SecondaryWindow] Attaching Load event handler...");
            form.Load += (s, e) =>
            {
                _logger.Info("[SecondaryWindow] Form Load event fired, initializing WebView2...");

                // Use BeginInvoke to run asynchronously on the UI thread
                form.BeginInvoke(async () =>
                {
                    try
                    {
                        await session.StartAsync();
                        _logger.Info("[SecondaryWindow] Session started, navigating to capture.html...");

                        // Detect dev mode and navigate accordingly
                        var baseUrl = _appEnvironment.IsDevelopment
                            ? "http://localhost:3000/capture.html"
                            : "https://app.local/capture.html";

                        // Append profileId as query parameter
                        var url = $"{baseUrl}?profileId={Uri.EscapeDataString(profileId)}";

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

            _logger.Info("[SecondaryWindow] Adding window to tracking list...");
            _openWindows.Add((form, session, profileId));

            _logger.Info($"[SecondaryWindow] Capture window created successfully: {sessionId}");
            _logger.Info("[SecondaryWindow] Returning form to caller");
            return form;
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Failed to create capture window: {ex.Message}");
            return null;
        }
    }

    public void CloseAllWindows()
    {
        foreach (var (form, session, profileId) in _openWindows.ToList())
        {
            try
            {
                // Need to invoke on the form's thread to close it
                if (form.InvokeRequired)
                {
                    form.Invoke(() => form.Close());
                }
                else
                {
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SecondaryWindow] Error closing window: {ex.Message}");
            }
        }
        _openWindows.Clear();
    }

    public bool HasWindowForProfile(string profileId)
    {
        return _openWindows.Any(w => w.ProfileId == profileId);
    }

    public void CloseWindowForProfile(string profileId)
    {
        var windowEntry = _openWindows.FirstOrDefault(w => w.ProfileId == profileId);
        if (windowEntry != default)
        {
            try
            {
                var form = windowEntry.Form;
                // Need to invoke on the form's thread to close it
                if (form.InvokeRequired)
                {
                    form.Invoke(() => form.Close());
                }
                else
                {
                    form.Close();
                }
                _logger.Info($"[SecondaryWindow] Closed window for profile: {profileId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SecondaryWindow] Error closing window for profile {profileId}: {ex.Message}");
            }
        }
    }

    private Task<Point> LoadWindowPositionAsync(string profileId, int windowWidth, int windowHeight, Rectangle screen)
    {
        try
        {
            _logger.Info("[SecondaryWindow] Getting profile path from ProfilePathService...");
            var profileDir = _profilePathService.ProfilePath;
            _logger.Info($"[SecondaryWindow] Profile directory: {profileDir}");

            var configPath = Path.Combine(profileDir, "config.json");
            _logger.Info($"[SecondaryWindow] Config path: {configPath}");

            if (File.Exists(configPath))
            {
                _logger.Info("[SecondaryWindow] Config file exists, reading...");
                // Use synchronous read to avoid deadlock on STA thread
                var json = File.ReadAllText(configPath);
                _logger.Info($"[SecondaryWindow] Config JSON read, length: {json.Length}");

                var config = JsonSerializer.Deserialize<ProfileConfiguration>(json);
                _logger.Info($"[SecondaryWindow] Config deserialized, Capture null? {config?.Capture == null}");

                if (config?.Capture?.X != null && config.Capture.Y != null)
                {
                    int x = config.Capture.X.Value;
                    int y = config.Capture.Y.Value;
                    _logger.Info($"[SecondaryWindow] Saved position found: ({x}, {y})");

                    // Validate position is on screen
                    if (IsPositionValid(x, y, windowWidth, windowHeight, screen))
                    {
                        _logger.Info($"[SecondaryWindow] Position is valid, using saved position");
                        return Task.FromResult(new Point(x, y));
                    }
                    else
                    {
                        _logger.Info("[SecondaryWindow] Saved position is off-screen, using default");
                    }
                }
                else
                {
                    _logger.Info("[SecondaryWindow] No saved position in config");
                }
            }
            else
            {
                _logger.Info("[SecondaryWindow] Config file does not exist");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Exception loading window position: {ex.Message}");
            _logger.Error($"[SecondaryWindow] Stack trace: {ex.StackTrace}");
        }

        // Default to right-bottom corner
        _logger.Info($"[SecondaryWindow] Calculating default position for screen: {screen}");
        int defaultX = screen.Right - windowWidth - 20;
        int defaultY = screen.Bottom - windowHeight - 20;
        _logger.Info($"[SecondaryWindow] Using default position (right-bottom): ({defaultX}, {defaultY})");
        return Task.FromResult(new Point(defaultX, defaultY));
    }

    private Task SaveWindowPositionAsync(string profileId, Point location)
    {
        try
        {
            var profileDir = _profilePathService.ProfilePath;
            var configPath = Path.Combine(profileDir, "config.json");

            ProfileConfiguration config;
            if (File.Exists(configPath))
            {
                // Use synchronous I/O to avoid issues with event handlers
                var json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<ProfileConfiguration>(json) ?? new ProfileConfiguration { ProfileId = profileId };
            }
            else
            {
                config = new ProfileConfiguration { ProfileId = profileId };
            }

            config.Capture = new CaptureWindowConfiguration
            {
                X = location.X,
                Y = location.Y
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configPath, updatedJson);

            _logger.Info($"[SecondaryWindow] Saved window position: ({location.X}, {location.Y})");
        }
        catch (Exception ex)
        {
            _logger.Error($"[SecondaryWindow] Failed to save window position: {ex.Message}");
        }

        return Task.CompletedTask;
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
