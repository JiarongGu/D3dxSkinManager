using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Transparent overlay that captures OS file drag-drop events
///
/// Visibility logic:
/// - Mouse outside zone → always visible
/// - Mouse inside + dragging → always visible (drag takes precedence)
/// - Mouse inside + not dragging → check DOM occlusion via ExecuteScriptAsync
///
/// Mouse tracking is driven by frontend mouseenter/mouseleave events on HTML element
/// </summary>
public class DropZoneOverlay : Panel
{
    private readonly ILogHelper _logger;
    private readonly Action<string[], Point> _onFileDrop;
    private readonly Action<string> _onDragEnter;
    private readonly Action<string> _onDragLeave;
    private readonly WebView2? _webView;
    public string ZoneId { get; }

    private bool _mouseIsInside = false;
    private bool _isDragging = false;
    private bool _pendingOcclusionCheck = false;
    private bool _isDisposed = false;
    private bool _formIsActive = true; // Track if parent form is active

    // The overlay can be disposed (zone unregistered / navigation / form close) while an async occlusion
    // check or a queued SHOW/form-event is still pending. Touching Visible/Handle/Bounds after that throws
    // ObjectDisposedException — so every control-mutating path checks this first.
    private bool Dead => _isDisposed || IsDisposed;

    public DropZoneOverlay(
        string zoneId,
        ILogHelper logger,
        WebView2 webView,
        Action<string[], Point> onFileDrop,
        Action<string> onDragEnter,
        Action<string> onDragLeave)
    {
        ZoneId = zoneId;
        _logger = logger;
        _webView = webView;
        _onFileDrop = onFileDrop;
        _onDragEnter = onDragEnter;
        _onDragLeave = onDragLeave;

        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AllowDrop = true;

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        DragLeave += OnDragLeave;
        DragOver += OnDragOver;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        // Start visible by default (overlay shows until mouse enters)
        Visible = true;
        _logger.Info($"DropZoneOverlay created: {zoneId}", "DropZone");
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        _mouseIsInside = true;

        // Only process if overlay is visible (when mouse enters the visible overlay)
        if (!Visible) return;

        // Hide overlay when mouse enters (allows hover effects on webview elements)
        // This works even when form is inactive because element mouseenter fires through overlay
        if (!_isDragging)
        {
            HideOverlay("mouse entered overlay");
        }
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        _mouseIsInside = false;
        // Don't show overlay on leave - frontend will send SHOW when needed
    }

    /// <summary>
    /// Called by frontend when mouse leaves the HTML element.
    /// Fallback for when native MouseLeave doesn't fire.
    /// </summary>
    public void OnFrontendMouseLeave()
    {
        _mouseIsInside = false;
        ShowOverlay("frontend mouse left");
    }

    /// <summary>
    /// Set whether the parent form is active.
    /// Updates overlay visibility based on form state and mouse position.
    /// </summary>
    public void SetFormActive(bool isActive)
    {
        _formIsActive = isActive;

        if (!isActive)
        {
            // Form deactivated - ALWAYS show overlay to enable drag-drop from other apps
            // This is critical for background drag-drop functionality
            if (!_isDragging)
            {
                ShowOverlay("form inactive");
            }
        }
        else
        {
            // Form activated - check current mouse position
            // If mouse is inside, keep overlay hidden; otherwise show it
            if (_mouseIsInside && !_isDragging)
            {
                HideOverlay("form active, mouse inside");
            }
            else
            {
                ShowOverlay("form active, mouse outside");
            }
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TRANSPARENT = 0x20;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Don't paint - overlay is transparent
    }


    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            _isDragging = true;
            _onDragEnter?.Invoke(ZoneId);

            // File drag started: check if zone is occluded
            CheckOcclusion();
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragLeave(object? sender, EventArgs e)
    {
        _isDragging = false;
        _onDragLeave?.Invoke(ZoneId);

        // Drag ended: hide overlay if mouse still inside
        if (_mouseIsInside)
        {
            HideOverlay("drag ended, mouse inside");
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                _logger.Info($"Files dropped on zone {ZoneId}: {string.Join(", ", files)}", "DropZone");
                var clientPos = PointToClient(new Point(e.X, e.Y));
                _onFileDrop?.Invoke(files, clientPos);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling drop on zone {ZoneId}: {ex.Message}", "DropZone", ex);
        }
        finally
        {
            _isDragging = false;

            // Drag ended: hide overlay if mouse still inside
            if (_mouseIsInside)
            {
                HideOverlay("drag ended, mouse inside");
            }
        }
    }


    private async void CheckOcclusion()
    {
        if (_pendingOcclusionCheck || _webView == null) return;

        _pendingOcclusionCheck = true;

        try
        {
            var script = $@"
                (function() {{
                    const elem = document.querySelector('[data-drop-zone-id=""{ZoneId}""]');
                    if (!elem) return true;

                    const rect = elem.getBoundingClientRect();
                    if (rect.width === 0 || rect.height === 0) return true;

                    const testPoints = [
                        {{ x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }},
                        {{ x: rect.left + 10, y: rect.top + 10 }},
                        {{ x: rect.right - 10, y: rect.top + 10 }},
                        {{ x: rect.left + 10, y: rect.bottom - 10 }},
                        {{ x: rect.right - 10, y: rect.bottom - 10 }}
                    ];

                    for (const point of testPoints) {{
                        const topElement = document.elementFromPoint(point.x, point.y);
                        if (topElement && (topElement === elem || elem.contains(topElement))) {{
                            continue;
                        }}
                        if (topElement) {{
                            return true;
                        }}
                    }}

                    return false;
                }})();
            ";

            var result = await _webView.ExecuteScriptAsync(script);
            if (Dead) return; // overlay was torn down during the await — do not touch the control

            var isOccluded = result?.Trim().ToLower() == "true";

            if (isOccluded)
            {
                HideOverlay("occluded");
            }
            else
            {
                ShowOverlay("not occluded");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error checking occlusion for zone {ZoneId}: {ex.Message}", "DropZone");
            ShowOverlay("occlusion check error");
        }
        finally
        {
            _pendingOcclusionCheck = false;
        }
    }

    private void ShowOverlay(string reason)
    {
        if (Dead) return;
        if (!Visible)
        {
            Visible = true;
            BringToFront(); // Ensure overlay is on top
            _logger.Info($"Zone {ZoneId} shown: {reason} (Handle={Handle}, FormActive={_formIsActive})", "DropZone");
        }
        else
        {
            // Already visible but log anyway for debugging
            _logger.Debug($"Zone {ZoneId} already visible, skipped show: {reason} (FormActive={_formIsActive})", "DropZone");
        }
    }

    private void HideOverlay(string reason)
    {
        if (Dead) return;
        if (Visible)
        {
            Visible = false;
            _logger.Info($"Zone {ZoneId} hidden: {reason} (Handle={Handle}, FormActive={_formIsActive})", "DropZone");
        }
        else
        {
            // Already hidden but log anyway for debugging
            _logger.Debug($"Zone {ZoneId} already hidden, skipped hide: {reason} (FormActive={_formIsActive})", "DropZone");
        }
    }

    public new void UpdateBounds(int x, int y, int width, int height)
    {
        if (Dead) return;
        if (Left != x || Top != y || Width != width || Height != height)
        {
            SetBounds(x, y, width, height);
            _logger.Debug($"Zone {ZoneId} bounds updated: ({x}, {y}, {width}x{height})", "DropZone");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;

            DragEnter -= OnDragEnter;
            DragDrop -= OnDragDrop;
            DragLeave -= OnDragLeave;
            DragOver -= OnDragOver;
            MouseEnter -= OnMouseEnter;
            MouseLeave -= OnMouseLeave;

            _logger.Info($"DropZoneOverlay disposed: {ZoneId}", "DropZone");
        }
        base.Dispose(disposing);
    }
}

public static class DropZoneEvents
{
    public const string DRAG_ENTER = "DRAG_ENTER";
    public const string DRAG_LEAVE = "DRAG_LEAVE";
    public const string FILE_DROP = "FILE_DROP";
    public const string MOUSE_ENTER = "MOUSE_ENTER";
    public const string MOUSE_LEAVE = "MOUSE_LEAVE";
}
