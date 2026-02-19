using System;
using Photino.NET;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing window size and position persistence
/// Handles loading, saving, and validating window state across application restarts
/// </summary>
public class WindowStateService : IWindowStateService
{
    private readonly IGlobalSettingsService _settingsService;

    // Default window dimensions
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private const int MinWidth = 800;
    private const int MinHeight = 600;

    // Minimum visible area to consider position valid (title bar area)
    private const int MinVisibleWidth = 100;
    private const int MinVisibleHeight = 50;

    public WindowStateService(IGlobalSettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Loads saved window state from global settings
    /// </summary>
    public (int width, int height, int? x, int? y, bool maximized) LoadWindowState()
    {
        try
        {
            var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();

            var width = settings.Window.Width ?? DefaultWidth;
            var height = settings.Window.Height ?? DefaultHeight;
            var x = settings.Window.X;
            var y = settings.Window.Y;
            var maximized = settings.Window.Maximized;

            // Ensure minimum size
            width = Math.Max(width, MinWidth);
            height = Math.Max(height, MinHeight);

            Console.WriteLine($"[WindowState] Loaded: {width}x{height}, " +
                            $"Position: {(x.HasValue ? $"X={x},Y={y}" : "default")}, " +
                            $"Maximized: {maximized}");

            return (width, height, x, y, maximized);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowState] Error loading state: {ex.Message}");
            return (DefaultWidth, DefaultHeight, null, null, false);
        }
    }

    /// <summary>
    /// Saves current window state to global settings
    /// </summary>
    public void SaveWindowState(PhotinoWindow window)
    {
        if (window == null)
        {
            Console.WriteLine("[WindowState] Cannot save - window is null");
            return;
        }

        try
        {
            var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();

            // Get current window properties
            var currentLeft = window.Left;
            var currentTop = window.Top;
            var currentWidth = window.Width;
            var currentHeight = window.Height;
            var currentMaximized = window.Maximized;

            Console.WriteLine($"[WindowState] Reading current state: " +
                            $"Left={currentLeft}, Top={currentTop}, " +
                            $"Width={currentWidth}, Height={currentHeight}, " +
                            $"Maximized={currentMaximized}");

            // Save maximized state
            settings.Window.Maximized = currentMaximized;

            // Only save position/size if not maximized and values are valid
            if (!currentMaximized && currentWidth > 0 && currentHeight > 0)
            {
                settings.Window.X = currentLeft;
                settings.Window.Y = currentTop;
                settings.Window.Width = currentWidth;
                settings.Window.Height = currentHeight;

                Console.WriteLine($"[WindowState] Saved position and size");
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

            _settingsService.UpdateSettingsAsync(settings).GetAwaiter().GetResult();

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
    public bool IsPositionValid(int x, int y, int width, int height, PhotinoWindow window)
    {
        if (window == null)
        {
            return false;
        }

        try
        {
            // Get all monitors
            var monitors = window.Monitors;
            if (monitors == null || monitors.Count == 0)
            {
                Console.WriteLine("[WindowState] No monitors found, position invalid");
                return false;
            }

            // Check if window is at least partially visible on any monitor
            foreach (var monitor in monitors)
            {
                // Window rectangle
                var windowRight = x + width;
                var windowBottom = y + height;

                // Monitor rectangle (MonitorArea is a Rectangle with X, Y, Width, Height)
                var monitorArea = monitor.MonitorArea;
                var monitorRight = monitorArea.X + monitorArea.Width;
                var monitorBottom = monitorArea.Y + monitorArea.Height;

                // Check if there's any overlap
                bool hasOverlap = !(windowRight < monitorArea.X ||
                                   x > monitorRight ||
                                   windowBottom < monitorArea.Y ||
                                   y > monitorBottom);

                if (hasOverlap)
                {
                    // Ensure at least minimum visible area (title bar)
                    int visibleWidth = Math.Min(windowRight, monitorRight) - Math.Max(x, monitorArea.X);
                    int visibleHeight = Math.Min(windowBottom, monitorBottom) - Math.Max(y, monitorArea.Y);

                    if (visibleWidth >= MinVisibleWidth && visibleHeight >= MinVisibleHeight)
                    {
                        Console.WriteLine($"[WindowState] Position valid on monitor at " +
                                        $"({monitorArea.X},{monitorArea.Y})");
                        return true;
                    }
                }
            }

            Console.WriteLine($"[WindowState] Position ({x},{y}) not visible on any monitor");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WindowState] Error validating position: {ex.Message}");
            return false;
        }
    }
}
