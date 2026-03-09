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
    private global::System.Windows.Forms.Timer? _mouseTrackTimer; // Only used when form is inactive

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

        // Start visible by default (overlay shows until mouse enters)
        Visible = true;
        _logger.Info($"DropZoneOverlay created: {zoneId}", "DropZone");
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        // Always hide overlay on mouse enter (for CSS hover effects)
        if (!_isDragging)
        {
            _mouseIsInside = true;
            HideOverlay("mouse entered overlay");
        }
    }

    /// <summary>
    /// Called by frontend when mouse leaves the HTML element.
    /// Only works when form is active (frontend events fire).
    /// </summary>
    public void OnFrontendMouseLeave()
    {
        _mouseIsInside = false;
        ShowOverlay("frontend mouse left");
    }

    /// <summary>
    /// Set whether the parent form is active.
    /// When inactive, uses timer polling since frontend events don't fire reliably.
    /// </summary>
    public void SetFormActive(bool isActive)
    {
        _formIsActive = isActive;

        if (!isActive)
        {
            // Form deactivated - start timer polling (20ms)
            if (_mouseTrackTimer == null)
            {
                _mouseTrackTimer = new global::System.Windows.Forms.Timer { Interval = 20 };
                _mouseTrackTimer.Tick += CheckMousePosition;
            }
            _mouseTrackTimer.Start();
            ShowOverlay("form inactive - timer started");
        }
        else
        {
            // Form activated - stop timer polling, resume frontend-driven mode
            _mouseTrackTimer?.Stop();
            ShowOverlay("form active - timer stopped");
        }
    }

    private void CheckMousePosition(object? sender, EventArgs e)
    {
        if (_isDisposed || IsDisposed) return;

        try
        {
            var cursorPos = Cursor.Position;
            var overlayPt = PointToClient(cursorPos);
            bool mouseCurrentlyInside = ClientRectangle.Contains(overlayPt);

            if (mouseCurrentlyInside != _mouseIsInside)
            {
                _mouseIsInside = mouseCurrentlyInside;

                if (_mouseIsInside)
                {
                    // Mouse entered: hide overlay unless dragging files
                    if (!_isDragging)
                    {
                        HideOverlay("timer: mouse entered");
                    }
                }
                else
                {
                    // Mouse left: show overlay
                    ShowOverlay("timer: mouse left");
                }
            }
        }
        catch (ObjectDisposedException)
        {
            _mouseTrackTimer?.Stop();
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
        if (!Visible)
        {
            Visible = true;
            _logger.Verbose($"Zone {ZoneId} shown: {reason}", "DropZone");
        }
    }

    private void HideOverlay(string reason)
    {
        if (Visible)
        {
            Visible = false;
            _logger.Verbose($"Zone {ZoneId} hidden: {reason}", "DropZone");
        }
    }

    public new void UpdateBounds(int x, int y, int width, int height)
    {
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

            _mouseTrackTimer?.Stop();
            _mouseTrackTimer?.Dispose();
            _mouseTrackTimer = null;

            DragEnter -= OnDragEnter;
            DragDrop -= OnDragDrop;
            DragLeave -= OnDragLeave;
            DragOver -= OnDragOver;
            MouseEnter -= OnMouseEnter;

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
}
