using Photino.NET;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing window size and position persistence
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Loads saved window state from settings
    /// </summary>
    /// <returns>Tuple containing (width, height, x, y, maximized)</returns>
    (int width, int height, int? x, int? y, bool maximized) LoadWindowState();

    /// <summary>
    /// Saves current window state to settings
    /// </summary>
    /// <param name="window">The Photino window to save state from</param>
    void SaveWindowState(PhotinoWindow window);

    /// <summary>
    /// Validates that a window position is visible on at least one monitor
    /// </summary>
    /// <param name="x">Window X position</param>
    /// <param name="y">Window Y position</param>
    /// <param name="width">Window width</param>
    /// <param name="height">Window height</param>
    /// <param name="window">The Photino window (for monitor information)</param>
    /// <returns>True if position is valid, false otherwise</returns>
    bool IsPositionValid(int x, int y, int width, int height, PhotinoWindow window);
}
