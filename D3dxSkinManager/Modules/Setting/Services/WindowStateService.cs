using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Setting.Services;

/// <summary>
/// Service for managing window size and position persistence
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Loads saved window state from settings
    /// </summary>
    /// <returns>Tuple containing (width, height, x, y, maximized)</returns>
    Task<(int width, int height, int? x, int? y, bool maximized)> LoadWindowStateAsync();

    /// <summary>
    /// Saves current window state to settings
    /// </summary>
    /// <param name="form">The WinForms Form to save state from</param>
    Task SaveWindowStateAsync(Form form);

    /// <summary>
    /// Validates that a window position is visible on at least one monitor
    /// </summary>
    /// <param name="x">Window X position</param>
    /// <param name="y">Window Y position</param>
    /// <param name="width">Window width</param>
    /// <param name="height">Window height</param>
    /// <param name="form">The WinForms Form (for monitor information)</param>
    /// <returns>True if position is valid, false otherwise</returns>
    bool IsPositionValid(int x, int y, int width, int height, Form form);
}

/// <summary>
/// Service for managing window size and position persistence
/// Handles loading, saving, and validating window state across application restarts
/// </summary>
public class WindowStateService : IWindowStateService
{
    private readonly IGlobalSettingService _settingsService;

    // Default window dimensions
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private const int MinWidth = 800;
    private const int MinHeight = 600;

    // Minimum visible area to consider position valid (title bar area)
    private const int MinVisibleWidth = 100;
    private const int MinVisibleHeight = 50;

    public WindowStateService(IGlobalSettingService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Convert the stored LOGICAL window state to the PHYSICAL (device) px the WinForms form needs at the
    /// CURRENT monitor DPI. WinForms window coordinates are device px at the form's DPI and are NOT
    /// auto-scaled from a logical baseline, so a logical size must be × the current DPI (mirrors
    /// SecondaryWindowService's DPI restore). Nulls fall back to the 1280x800 logical default; the minimum
    /// is clamped in LOGICAL space. Pure + DPI-injected so it is fully unit-testable. At 100%
    /// (currentDpi == 1) it is the identity — no change on 96-DPI monitors. The DPI is never persisted;
    /// it's resolved fresh each start (each launch can be a different monitor DPI).
    /// </summary>
    public static (int width, int height, int? x, int? y, bool maximized) ToPhysicalState(
        int? logicalWidth, int? logicalHeight, int? logicalX, int? logicalY, bool maximized, double currentDpi)
    {
        double scale = currentDpi > 0 ? currentDpi : 1.0;
        double logW = Math.Max(logicalWidth ?? DefaultWidth, MinWidth);
        double logH = Math.Max(logicalHeight ?? DefaultHeight, MinHeight);
        int physW = (int)Math.Round(logW * scale);
        int physH = (int)Math.Round(logH * scale);

        // Position is both-or-neither (an incomplete pair can't place a window) — the caller centers when
        // there's no position.
        int? physX = null, physY = null;
        if (logicalX.HasValue && logicalY.HasValue)
        {
            physX = (int)Math.Round(logicalX.Value * scale);
            physY = (int)Math.Round(logicalY.Value * scale);
        }
        return (physW, physH, physX, physY, maximized);
    }

    /// <summary>
    /// Loads saved window state (LOGICAL px) and returns it as PHYSICAL px for the current monitor DPI.
    /// </summary>
    public async Task<(int width, int height, int? x, int? y, bool maximized)> LoadWindowStateAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
            // GetDpiScaleFactor returns the primary monitor's real DPI in this PerMonitorV2 process (2.0
            // at 200%) — verified 2026-07-12. The stored logical size × this = physical px for the form.
            var currentDpi = DpiHelper.GetDpiScaleFactor();
            var state = ToPhysicalState(
                settings.Window.Width, settings.Window.Height, settings.Window.X, settings.Window.Y,
                settings.Window.Maximized, currentDpi);

            Console.WriteLine($"[WindowState] Loaded (dpi {currentDpi:F2}): {state.width}x{state.height} physical, " +
                            $"Position: {(state.x.HasValue ? $"X={state.x},Y={state.y}" : "default")}, Maximized: {state.maximized}");
            return state;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowState] Error loading state: {ex.Message}");
            return ToPhysicalState(null, null, null, null, false, DpiHelper.GetDpiScaleFactor());
        }
    }

    /// <summary>
    /// Saves current window state to global settings
    /// </summary>
    public async Task SaveWindowStateAsync(Form form)
    {
        if (form == null)
        {
            Console.WriteLine("[WindowState] Cannot save - form is null");
            return;
        }

        try
        {
            var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);

            // Get current window properties
            var currentLeft = form.Left;
            var currentTop = form.Top;
            var currentWidth = form.Width;
            var currentHeight = form.Height;
            var currentMaximized = form.WindowState == FormWindowState.Maximized;

            Console.WriteLine($"[WindowState] Reading current state: " +
                            $"Left={currentLeft}, Top={currentTop}, " +
                            $"Width={currentWidth}, Height={currentHeight}, " +
                            $"Maximized={currentMaximized}");

            // Save maximized state
            settings.Window.Maximized = currentMaximized;

            // Only save position/size if not maximized and values are valid. Store LOGICAL px (÷ the
            // form's current monitor DPI) so the value is DPI-independent and restores correctly at any
            // DPI on the next start — the DPI itself is never persisted.
            if (!currentMaximized && currentWidth > 0 && currentHeight > 0)
            {
                // The form's ACTUAL current-monitor DPI (could be a secondary monitor), via the shared
                // base-DPI constant — no hardcoded 96.
                double scale = DpiHelper.ScaleFromDeviceDpi(form.DeviceDpi);
                settings.Window.X = (int)Math.Round(currentLeft / scale);
                settings.Window.Y = (int)Math.Round(currentTop / scale);
                settings.Window.Width = (int)Math.Round(currentWidth / scale);
                settings.Window.Height = (int)Math.Round(currentHeight / scale);

                Console.WriteLine($"[WindowState] Saved logical position and size (dpi {scale:F2})");
            }
            else if (currentMaximized)
            {
                Console.WriteLine("[WindowState] Window is maximized, keeping previous position/size");
            }
            else
            {
                Console.WriteLine($"[WindowState] Invalid dimensions (Width={currentWidth}, Height={currentHeight}), " +
                                "not saving position/size");
            }

            await _settingsService.UpdateSettingsAsync(settings).ConfigureAwait(false);

            Console.WriteLine($"[WindowState] Saved: {settings.Window.Width}x{settings.Window.Height}, " +
                            $"Position: X={settings.Window.X},Y={settings.Window.Y}, " +
                            $"Maximized: {settings.Window.Maximized}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowState] Error saving state: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that window position is within available screen bounds
    /// Ensures at least part of the window (title bar area) is visible on screen
    /// </summary>
    public bool IsPositionValid(int x, int y, int width, int height, Form form)
    {
        if (form == null)
        {
            return false;
        }

        try
        {
            // Get all screens (monitors)
            var screens = Screen.AllScreens;
            if (screens == null || screens.Length == 0)
            {
                Console.WriteLine("[WindowState] No screens found, position invalid");
                return false;
            }

            // Check if window is at least partially visible on any screen
            foreach (var screen in screens)
            {
                // Window rectangle
                var windowRight = x + width;
                var windowBottom = y + height;

                // Screen bounds
                var screenBounds = screen.Bounds;
                var screenRight = screenBounds.X + screenBounds.Width;
                var screenBottom = screenBounds.Y + screenBounds.Height;

                // Check if there's any overlap
                bool hasOverlap = !(windowRight < screenBounds.X ||
                                   x > screenRight ||
                                   windowBottom < screenBounds.Y ||
                                   y > screenBottom);

                if (hasOverlap)
                {
                    // Ensure at least minimum visible area (title bar)
                    int visibleWidth = Math.Min(windowRight, screenRight) - Math.Max(x, screenBounds.X);
                    int visibleHeight = Math.Min(windowBottom, screenBottom) - Math.Max(y, screenBounds.Y);

                    if (visibleWidth >= MinVisibleWidth && visibleHeight >= MinVisibleHeight)
                    {
                        Console.WriteLine($"[WindowState] Position valid on screen at " +
                                        $"({screenBounds.X},{screenBounds.Y})");
                        return true;
                    }
                }
            }

            Console.WriteLine($"[WindowState] Position ({x},{y}) not visible on any screen");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowState] Error validating position: {ex.Message}");
            return false;
        }
    }
}
