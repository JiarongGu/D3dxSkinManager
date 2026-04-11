using System.Runtime.InteropServices;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Helper for DPI scaling calculations
/// NOTE: With HighDpiMode.PerMonitorV2, Windows automatically scales window dimensions.
/// This helper is primarily for scaling internal UI elements like borders and hit areas.
/// </summary>
public static class DpiHelper
{
    private const double BaseDpi = 96.0; // 100% scaling

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    private const int LOGPIXELSX = 88; // DPI along X axis
    private const int LOGPIXELSY = 90; // DPI along Y axis

    /// <summary>
    /// Get the current DPI scaling factor (e.g., 1.0 for 100%, 1.5 for 150%, 2.0 for 200%)
    /// </summary>
    public static double GetDpiScaleFactor()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
            return dpiX / BaseDpi;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <summary>
    /// Scale a pixel value for internal UI elements (borders, hit areas, etc.)
    /// Use this ONLY for elements that need manual scaling with PerMonitorV2.
    /// Do NOT use for window dimensions - those are auto-scaled by Windows.
    /// </summary>
    /// <param name="basePixels">Pixel value designed for base resolution</param>
    /// <returns>Scaled pixel value for current DPI</returns>
    public static int ScalePixels(int basePixels)
    {
        double scaleFactor = GetDpiScaleFactor();
        return (int)Math.Round(basePixels * scaleFactor);
    }

    /// <summary>
    /// Scale a size - use for internal elements, NOT for window dimensions
    /// </summary>
    public static Size ScaleSize(int width, int height)
    {
        return new Size(ScalePixels(width), ScalePixels(height));
    }

    /// <summary>
    /// Scale a point - use for internal elements positioning
    /// </summary>
    public static Point ScalePoint(int x, int y)
    {
        return new Point(ScalePixels(x), ScalePixels(y));
    }
}
