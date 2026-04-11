using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Services;

public interface IScreenCaptureService
{
    Task<ScreenCaptureResult> CaptureAsync(ScreenCaptureConfig config);
    Task ShowBorderOverlayAsync(int x, int y, int width, int height, IProfileEventBus eventBus);
    Task HideBorderOverlayAsync();
    void ToggleCaptureControlPanel(string profileId);
    bool IsBorderOverlayVisible { get; }
}

public class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogHelper _logger;
    private readonly IScreenCaptureProfileRepository _profileRepository;
    private readonly ISecondaryWindowService _windowService;
    private readonly IFormInteractionService _formInteractionService;

    private ScreenCaptureOverlay? _overlayForm;
    private Thread? _overlayThread;
    private readonly object _overlayLock = new object();

    // Throttle for bounds changed events (100ms)
    private readonly Throttle _boundsChangeThrottle = new Throttle(100);

    // P/Invoke for better screen capture (handles GPU-rendered content better)
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    public bool IsBorderOverlayVisible
    {
        get
        {
            lock (_overlayLock)
            {
                return _overlayForm != null && !_overlayForm.IsDisposed;
            }
        }
    }

    public ScreenCaptureService(
        ILogHelper logger,
        IScreenCaptureProfileRepository profileRepository,
        ISecondaryWindowService windowService,
        IFormInteractionService formInteractionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _formInteractionService = formInteractionService ?? throw new ArgumentNullException(nameof(formInteractionService));
    }

    public async Task<ScreenCaptureResult> CaptureAsync(ScreenCaptureConfig config)
    {
        try
        {
            double dpiScale = DpiHelper.GetDpiScaleFactor();
            var x = (int)Math.Round((config.X ?? 0) * dpiScale);
            var y = (int)Math.Round((config.Y ?? 0) * dpiScale);
            var width = (int)Math.Round((config.Width ?? 800) * dpiScale);
            var height = (int)Math.Round((config.Height ?? 600) * dpiScale);

            _logger.Info($"[ScreenCaptureService] Capturing {width}x{height} at ({x}, {y})");

            // Temporarily hide border overlay before capture to prevent it from being included in the screenshot
            var borderWasVisible = IsBorderOverlayVisible;
            if (borderWasVisible)
            {
                _logger.Info("[ScreenCaptureService] Temporarily hiding border overlay for capture");
                TemporarilyHideOverlay();

                // Wait for the overlay to fully hide and screen to refresh (150ms is enough for most systems)
                await Task.Delay(150);
            }

            try
            {
                using var bitmap = CaptureScreen(x, y, width, height);

                var copiedToClipboard = false;
                if (config.CopyToClipboard)
                {
                    copiedToClipboard = CopyToClipboard(bitmap);
                }

                string? savedPath = null;
                if (config.SaveToFile && !string.IsNullOrEmpty(config.OutputPath))
                {
                    SaveToFile(bitmap, config.OutputPath, ImageFormat.Png);
                    savedPath = config.OutputPath;
                }

                return new ScreenCaptureResult
                {
                    Success = true,
                    CapturedArea = new ScreenCaptureArea
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    },
                    CopiedToClipboard = copiedToClipboard,
                    SavedPath = savedPath
                };
            }
            finally
            {
                // Restore border overlay if it was visible before capture
                if (borderWasVisible)
                {
                    _logger.Info("[ScreenCaptureService] Restoring border overlay after capture");
                    RestoreOverlay();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[ScreenCaptureService] Capture failed: {ex.Message}");
            return new ScreenCaptureResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task ShowBorderOverlayAsync(int x, int y, int width, int height, IProfileEventBus eventBus)
    {
        // Incoming values are logical (CSS) pixels from the frontend — convert to physical pixels
        // for ScreenCaptureOverlay, which sets Form.Bounds directly using Win32 physical screen coords.
        double dpiScale = DpiHelper.GetDpiScaleFactor();
        int physX = (int)Math.Round(x * dpiScale);
        int physY = (int)Math.Round(y * dpiScale);
        int physWidth = (int)Math.Round(width * dpiScale);
        int physHeight = (int)Math.Round(height * dpiScale);

        _logger.Info($"[ScreenCaptureService] ShowBorderOverlayAsync called: logical({x}, {y}) {width}x{height} -> physical({physX}, {physY}) {physWidth}x{physHeight}");

        lock (_overlayLock)
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                // Update existing overlay
                _logger.Info("[ScreenCaptureService] Updating existing overlay bounds");
                _overlayForm.Invoke(() => _overlayForm.UpdateBounds(physX, physY, physWidth, physHeight));
                return Task.CompletedTask;
            }

            _logger.Info("[ScreenCaptureService] Creating new overlay on STA thread");

            // Create new overlay on STA thread
            _overlayThread = new Thread(() =>
            {
                try
                {
                    _logger.Info($"[ScreenCaptureService] STA thread started, creating overlay at ({physX}, {physY}) size {physWidth}x{physHeight}");

                    var form = new ScreenCaptureOverlay(physX, physY, physWidth, physHeight);
                    _logger.Info("[ScreenCaptureService] ScreenCaptureOverlayForm created");

                    // Hook into main form's FormClosed event to close overlay when main form closes
                    var mainForm = _formInteractionService.GetMainForm();
                    if (mainForm != null)
                    {
                        FormClosedEventHandler? closeHandler = null;
                        closeHandler = (s, e) =>
                        {
                            _logger.Info("[ScreenCaptureService] Main form closed, closing overlay");
                            try
                            {
                                if (form != null && !form.IsDisposed && form.IsHandleCreated)
                                {
                                    // Invoke on the overlay form's thread to close it
                                    form.Invoke(() =>
                                    {
                                        _logger.Info("[ScreenCaptureService] Invoking form.Close() on overlay thread");
                                        form.Close();
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[ScreenCaptureService] Error closing overlay: {ex.Message}");
                            }
                            if (closeHandler != null && mainForm != null)
                            {
                                mainForm.FormClosed -= closeHandler;
                            }
                        };
                        mainForm.FormClosed += closeHandler;
                        _logger.Info("[ScreenCaptureService] Registered FormClosed handler on main form");
                    }

                    // Subscribe to bounds changes and emit to Profile EventBus (throttled to 100ms)
                    form.BoundsChanged += (newX, newY, newWidth, newHeight) =>
                    {
                        _boundsChangeThrottle.Execute(() =>
                        {
                            // ScreenCaptureOverlay fires physical pixel coords — convert to logical for frontend
                            double dpi = DpiHelper.GetDpiScaleFactor();
                            int logX = (int)Math.Round(newX / dpi);
                            int logY = (int)Math.Round(newY / dpi);
                            int logWidth = (int)Math.Round(newWidth / dpi);
                            int logHeight = (int)Math.Round(newHeight / dpi);

                            _logger.Info($"[ScreenCaptureService] Overlay bounds changed: physical({newX}, {newY}) {newWidth}x{newHeight} -> logical({logX}, {logY}) {logWidth}x{logHeight}");
                            _logger.Info($"[ScreenCaptureService] Emitting event: {ModuleNames.TOOL}/{ToolEvents.CAPTURE_BOUNDS_CHANGED}");
                            // Fire and forget - don't block the UI thread with .Wait()
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_BOUNDS_CHANGED, new
                                    {
                                        x = logX,
                                        y = logY,
                                        width = logWidth,
                                        height = logHeight
                                    }).ConfigureAwait(false);
                                    _logger.Info("[ScreenCaptureService] Event emitted successfully");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error($"[ScreenCaptureService] Failed to emit bounds changed event: {ex.Message}");
                                }
                            });
                        });
                    };

                    lock (_overlayLock)
                    {
                        _overlayForm = form;
                    }

                    _logger.Info("[ScreenCaptureService] Showing overlay and running message loop");
                    Application.Run(form);
                    _logger.Info("[ScreenCaptureService] Overlay message loop exited");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ScreenCaptureService] Overlay error: {ex.Message}\nStack: {ex.StackTrace}");
                }
            });

            _overlayThread.SetApartmentState(ApartmentState.STA);
            _overlayThread.IsBackground = true;
            _overlayThread.Start();
            _logger.Info("[ScreenCaptureService] Overlay STA thread started");
        }

        return Task.CompletedTask;
    }

    public Task HideBorderOverlayAsync()
    {
        lock (_overlayLock)
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                _logger.Info("[ScreenCaptureService] Closing overlay");
                _overlayForm.Invoke(_overlayForm.Close);
                _overlayForm = null;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Temporarily hides the overlay without closing it (for screen capture)
    /// </summary>
    private void TemporarilyHideOverlay()
    {
        lock (_overlayLock)
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed && _overlayForm.Visible)
            {
                _overlayForm.Invoke(() => _overlayForm.Hide());
            }
        }
    }

    /// <summary>
    /// Restores the overlay visibility after temporary hide
    /// </summary>
    private void RestoreOverlay()
    {
        lock (_overlayLock)
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed && !_overlayForm.Visible)
            {
                _overlayForm.Invoke(() => _overlayForm.Show());
            }
        }
    }

    public void ToggleCaptureControlPanel(string profileId)
    {
        const string captureWindowName = "capture";

        // Check if capture window already exists (service is scoped to current profile)
        if (_windowService.HasWindow(captureWindowName))
        {
            _logger.Info($"[ScreenCaptureService] Closing existing capture control panel for profile {profileId}");
            _windowService.CloseWindow(captureWindowName);
            return;
        }

        _logger.Info($"[ScreenCaptureService] Launching capture control panel for profile {profileId}");

        // Must run on STA thread for WinForms/WebView2
        var thread = new Thread(() =>
        {
            try
            {
                _logger.Info("[ScreenCaptureService] STA thread started");
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                _logger.Info("[ScreenCaptureService] Creating capture window...");

                // Call async method - safe because SecondaryWindowService uses ConfigureAwait(false) consistently
                var form = CreateCaptureWindowAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                if (form == null)
                {
                    _logger.Error("[ScreenCaptureService] CreateCaptureWindowAsync returned null");
                    return;
                }
                _logger.Info("[ScreenCaptureService] Showing form and starting message loop...");
                // Application.Run(form) shows the form AND runs the message loop
                Application.Run(form);
                _logger.Info("[ScreenCaptureService] Message loop exited (window closed)");
            }
            catch (Exception ex)
            {
                _logger.Error($"[ScreenCaptureService] Failed to show control panel: {ex.Message}");
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = false;
        thread.Start();
        _logger.Info("[ScreenCaptureService] STA thread launched");
    }

    /// <summary>
    /// Create capture-specific window with overlay cleanup behavior
    /// </summary>
    private async Task<Form?> CreateCaptureWindowAsync()
    {
        const string windowName = "capture";
        const string title = "Screen Capture";

        // Window dimensions in logical pixels
        // These are converted to physical pixels based on current DPI
        // Example: 300x210 logical -> 450x315 @ 150% DPI, 600x420 @ 200% DPI
        const int defaultWidth = 300;
        const int defaultHeight = 210;

        var form = await _windowService.CreateSecondaryWindowAsync(
            windowName,
            title,
            defaultWidth,
            defaultHeight,
            "capture.html"
        ).ConfigureAwait(false);

        if (form != null)
        {
            // Capture-specific: Close the capture overlay when control panel closes
            form.FormClosing += (s, e) =>
            {
                try
                {
                    if (IsBorderOverlayVisible)
                    {
                        _logger.Info("[ScreenCaptureService] Closing capture overlay with control panel");
                        // Fire and forget - FormClosing is synchronous, don't block with GetAwaiter().GetResult()
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await HideBorderOverlayAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[ScreenCaptureService] Failed to close capture overlay async: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ScreenCaptureService] Failed to close capture overlay: {ex.Message}");
                }
            };
        }

        return form;
    }

    private Bitmap CaptureScreen(int x, int y, int width, int height)
    {
        // Use Windows GDI BitBlt directly for better GPU-rendered content capture
        // This approach is more reliable than Graphics.CopyFromScreen for games and DirectX content

        IntPtr screenDC = IntPtr.Zero;
        IntPtr memoryDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            // Get the device context of the entire screen
            screenDC = GetDC(IntPtr.Zero);

            // Create a memory DC compatible with the screen DC
            memoryDC = CreateCompatibleDC(screenDC);

            // Create a bitmap compatible with the screen DC
            hBitmap = CreateCompatibleBitmap(screenDC, width, height);

            // Select the bitmap into the memory DC
            hOldBitmap = SelectObject(memoryDC, hBitmap);

            // Copy the screen content to the memory DC using SRCCOPY
            // SRCCOPY with BitBlt handles GPU content better than CopyFromScreen
            BitBlt(memoryDC, 0, 0, width, height, screenDC, x, y, CopyPixelOperation.SourceCopy);

            // Create a GDI+ Bitmap from the GDI bitmap
            var bitmap = Image.FromHbitmap(hBitmap);

            return bitmap;
        }
        finally
        {
            // Clean up resources
            if (hOldBitmap != IntPtr.Zero)
            {
                SelectObject(memoryDC, hOldBitmap);
            }
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (memoryDC != IntPtr.Zero)
            {
                DeleteDC(memoryDC);
            }
            if (screenDC != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
    }

    private bool CopyToClipboard(Bitmap bitmap)
    {
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetImage(bitmap);
                    _logger.Info("[ScreenCaptureService] Image copied to clipboard");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ScreenCaptureService] Failed to copy to clipboard: {ex.Message}");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[ScreenCaptureService] Clipboard operation failed: {ex.Message}");
            return false;
        }
    }

    private void SaveToFile(Bitmap bitmap, string filePath, ImageFormat format)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        bitmap.Save(filePath, format);
        _logger.Info($"[ScreenCaptureService] Image saved to: {filePath}");
    }
}
