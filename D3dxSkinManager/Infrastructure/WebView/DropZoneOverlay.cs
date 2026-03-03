using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Overlay panel that captures file drag-drop events and hides itself on mouse enter
/// </summary>
public class DropZoneOverlay : Panel
{
    private readonly ILogHelper _logger;
    private readonly Action<string[], Point> _onFileDrop;
    private readonly Action<string> _onDragEnter;
    private readonly Action<string> _onDragLeave;
    private readonly Action<string>? _onMouseEnter;
    private readonly Action<string>? _onMouseLeave;
    private readonly Action<string>? _onHide;
    private readonly WebView2? _webView;
    public string ZoneId { get; }

    public DropZoneOverlay(
        string zoneId,
        ILogHelper logger,
        WebView2 webView,
        Action<string[], Point> onFileDrop,
        Action<string> onDragEnter,
        Action<string> onDragLeave,
        Action<string>? onMouseEnter = null,
        Action<string>? onMouseLeave = null,
        Action<string>? onHide = null)
    {
        ZoneId = zoneId;
        _logger = logger;
        _webView = webView;
        _onFileDrop = onFileDrop;
        _onDragEnter = onDragEnter;
        _onDragLeave = onDragLeave;
        _onMouseEnter = onMouseEnter;
        _onMouseLeave = onMouseLeave;
        _onHide = onHide;

        // Make overlay transparent
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AllowDrop = true;

        // Don't override cursor - let it be determined by WebView content
        // Cursor will be updated dynamically based on what's underneath

        // Wire up drag events
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        DragLeave += OnDragLeave;
        DragOver += OnDragOver;

        // Wire up overlay-level mouse events
        // Overlay hides on enter, restores on leave
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        // Setup timer to restore overlay when cursor leaves area
        _visibilityCheckTimer = new System.Windows.Forms.Timer();
        _visibilityCheckTimer.Interval = 100; // Check every 100ms
        _visibilityCheckTimer.Tick += CheckOverlayVisibility;
        _visibilityCheckTimer.Start();

        _logger.Info($"DropZoneOverlay created: {zoneId}", "DropZone");
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TRANSPARENT = 0x20;

            var cp = base.CreateParams;
            // Use TRANSPARENT for visual transparency (mouse events don't pass through but we forward them)
            cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Don't paint - overlay is invisible
    }

    protected override void WndProc(ref Message m)
    {
        // No special handling needed - overlay hides on mouse enter
        // allowing WebView to receive all events directly
        base.WndProc(ref m);
    }

    private System.Windows.Forms.Timer? _visibilityCheckTimer;
    private bool _hiddenForMouseDown;

    private void CheckOverlayVisibility(object? sender, EventArgs e)
    {
        if (!Visible && _hiddenForMouseDown)
        {
            var cursorPos = Cursor.Position;
            var overlayPt = PointToClient(cursorPos);

            // Restore overlay when cursor leaves the overlay area
            if (!ClientRectangle.Contains(overlayPt))
            {
                _logger.Verbose($"Cursor left overlay area - restoring overlay", "DropZone");
                _hiddenForMouseDown = false;
                Visible = true;
            }
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            _logger.Debug($"Drag enter zone: {ZoneId}", "DropZone");
            _onDragEnter?.Invoke(ZoneId);
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
        _logger.Debug($"Drag leave zone: {ZoneId}", "DropZone");
        _onDragLeave?.Invoke(ZoneId);
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
    }


    private void OnMouseEnter(object? sender, EventArgs e)
    {
        _logger.Verbose($"Mouse enter zone: {ZoneId} - hiding overlay to allow WebView interaction", "DropZone");
        _onMouseEnter?.Invoke(ZoneId);

        // Hide overlay immediately when mouse enters (not during file drag)
        // This allows user to interact with WebView elements without wasting a click
        _hiddenForMouseDown = true;
        Visible = false;
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        _logger.Verbose($"Mouse leave zone: {ZoneId}", "DropZone");
        _onMouseLeave?.Invoke(ZoneId);

        // Note: Overlay will be restored by timer when cursor leaves overlay area
    }

    public new void UpdateBounds(int x, int y, int width, int height)
    {
        if (Left != x || Top != y || Width != width || Height != height)
        {
            SetBounds(x, y, width, height);
            _logger.Debug($"Zone {ZoneId} bounds updated: ({x}, {y}, {width}x{height})", "DropZone");
        }
    }

    public new void Show()
    {
        if (!Visible)
        {
            Visible = true;
            _logger.Debug($"Zone {ZoneId} shown", "DropZone");
        }
    }

    public new void Hide()
    {
        if (Visible)
        {
            Visible = false;
            _logger.Debug($"Zone {ZoneId} hidden", "DropZone");

            // Notify manager to unregister and dispose this zone
            _onHide?.Invoke(ZoneId);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _visibilityCheckTimer?.Stop();
            _visibilityCheckTimer?.Dispose();
            _logger.Info($"DropZoneOverlay disposed: {ZoneId}", "DropZone");
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Drop zone event type constants.
/// Used with ModuleNames.DROP_ZONE as the module identifier.
/// </summary>
public static class DropZoneEvents
{
    public const string DRAG_ENTER = "DRAG_ENTER";
    public const string DRAG_LEAVE = "DRAG_LEAVE";
    public const string FILE_DROP = "FILE_DROP";
    public const string MOUSE_ENTER = "MOUSE_ENTER";
    public const string MOUSE_LEAVE = "MOUSE_LEAVE";
}
