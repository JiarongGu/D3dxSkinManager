using System.Runtime.InteropServices;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Helper for DPI scaling calculations. With PerMonitorV2 the React UI + control LAYOUT auto-scale, but a
/// WinForms FORM's outer size set in code is device px and is NOT auto-scaled from a logical baseline — so
/// window size/position DO need an explicit logical↔physical conversion (see WindowStateService /
/// SecondaryWindowService, which use these factors). This helper's ScalePixels/ScaleSize are for internal
/// physical-pixel elements (capture overlay borders/hit areas), not for laying out React/managed controls.
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
    /// Scale factor for a known device DPI (e.g. a WinForms <c>Control.DeviceDpi</c>): 96→1.0, 120→1.25,
    /// 144→1.5, 192→2.0. Uses the shared <see cref="BaseDpi"/> (Windows' 100% reference DPI) so callers
    /// never hardcode 96. Falls back to 1.0 for a non-positive/unknown DPI.
    /// </summary>
    public static double ScaleFromDeviceDpi(int deviceDpi) => deviceDpi > 0 ? deviceDpi / BaseDpi : 1.0;

    /// <summary>
    /// Scale a pixel value for internal physical-pixel elements (capture overlay borders, hit areas).
    /// Use this ONLY for those. For WINDOW size/position, convert logical↔physical explicitly (see
    /// WindowStateService.ToPhysicalState) — NOT this method.
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
