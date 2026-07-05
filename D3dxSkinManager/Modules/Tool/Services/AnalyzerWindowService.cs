using System.Windows.Forms;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Pops the mod analyzer out into a SEPARATE WebView2 window (analyzer.html) so its results can sit
/// beside the main window / on another monitor while the user works the mod list — same mechanism as
/// the screen-capture control panel. Toggle: open if closed, close if open. The window is its own
/// React app sharing the backend (same profile DB), so it loads analysis results over IPC on its own.
/// </summary>
public interface IAnalyzerWindowService
{
    Task ToggleAsync(string profileId);
}

public class AnalyzerWindowService : IAnalyzerWindowService
{
    private const string WindowName = "analyzer";
    private const int DefaultWidth = 520;
    private const int DefaultHeight = 720;

    private readonly ISecondaryWindowService _windowService;
    private readonly ILogHelper _logger;

    public AnalyzerWindowService(ISecondaryWindowService windowService, ILogHelper logger)
    {
        _windowService = windowService;
        _logger = logger;
    }

    public async Task ToggleAsync(string profileId)
    {
        if (_windowService.HasWindow(WindowName))
        {
            _logger.Info($"[AnalyzerWindow] Closing existing analyzer window for profile {profileId}");
            _windowService.CloseWindow(WindowName);
            return;
        }

        _logger.Info($"[AnalyzerWindow] Launching analyzer window for profile {profileId}");

        // Preload config (async DB/settings) on THIS thread before the STA thread — same reason as the
        // capture control panel: avoids blocking the STA thread with a sync-over-async wait that can hang
        // while the thread pool is busy (e.g. during a mod analysis run).
        var windowConfig = await _windowService
            .PreloadWindowConfigAsync(WindowName, DefaultWidth, DefaultHeight).ConfigureAwait(false);

        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                var form = _windowService.CreateSecondaryWindow(
                    WindowName, "Mod Analyzer", DefaultWidth, DefaultHeight, "analyzer.html", windowConfig);
                if (form == null)
                {
                    _logger.Error("[AnalyzerWindow] CreateSecondaryWindow returned null");
                    return;
                }
                Application.Run(form);
            }
            catch (Exception ex)
            {
                _logger.Error($"[AnalyzerWindow] Failed to show analyzer window: {ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = false;
        thread.Start();
    }
}
