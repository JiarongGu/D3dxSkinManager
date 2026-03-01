using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Drop zone event type constants.
/// Used with ModuleNames.DROP_ZONE as the module identifier.
/// Example: EmitAsync(ModuleNames.DROP_ZONE, DropZoneEvents.CLICK, payload)
/// </summary>
public static class DropZoneEvents
{
    /// <summary>
    /// Fired when drop zone is clicked.
    /// </summary>
    public const string CLICK = "CLICK";

    /// <summary>
    /// Fired when drag enters a drop zone.
    /// </summary>
    public const string DRAG_ENTER = "DRAG_ENTER";

    /// <summary>
    /// Fired when drag leaves a drop zone.
    /// </summary>
    public const string DRAG_LEAVE = "DRAG_LEAVE";

    /// <summary>
    /// Fired when files are dropped on a drop zone.
    /// </summary>
    public const string FILE_DROP = "FILE_DROP";

    /// <summary>
    /// Fired when mouse enters a drop zone (non-dragging hover).
    /// </summary>
    public const string MOUSE_ENTER = "MOUSE_ENTER";

    /// <summary>
    /// Fired when mouse leaves a drop zone (non-dragging hover).
    /// </summary>
    public const string MOUSE_LEAVE = "MOUSE_LEAVE";
}

/// <summary>
/// Overlay panel that captures drag-drop and forwards mouse events via IPC
/// </summary>
public class DropZoneOverlay : Panel
{
    private readonly ILogHelper _logger;
    private readonly Action<string[], Point> _onFileDrop;
    private readonly Action<string> _onDragEnter;
    private readonly Action<string> _onDragLeave;
    private readonly Action<Point> _onClick;
    private readonly Action<string>? _onMouseEnter;
    private readonly Action<string>? _onMouseLeave;
    public string ZoneId { get; }

    public DropZoneOverlay(
        string zoneId,
        ILogHelper logger,
        Action<string[], Point> onFileDrop,
        Action<string> onDragEnter,
        Action<string> onDragLeave,
        Action<Point> onClick,
        Action<string>? onMouseEnter = null,
        Action<string>? onMouseLeave = null)
    {
        ZoneId = zoneId;
        _logger = logger;
        _onFileDrop = onFileDrop;
        _onDragEnter = onDragEnter;
        _onDragLeave = onDragLeave;
        _onClick = onClick;
        _onMouseEnter = onMouseEnter;
        _onMouseLeave = onMouseLeave;

        // Make overlay transparent
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AllowDrop = true;
        Cursor = Cursors.Hand;

        // Wire up drag events
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        DragLeave += OnDragLeave;
        DragOver += OnDragOver;

        // Wire up mouse events
        Click += OnClick;
        MouseDown += OnMouseDown;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        _logger.Info($"DropZoneOverlay created: {zoneId}", "DropZone");
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
        // Don't paint - overlay is invisible
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

    private void OnClick(object? sender, EventArgs e)
    {
        var mouseEvent = e as MouseEventArgs;
        var clickPos = mouseEvent != null ? mouseEvent.Location : Point.Empty;
        _logger.Debug($"Click on zone {ZoneId} at {clickPos}", "DropZone");
        _onClick?.Invoke(clickPos);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        _logger.Verbose($"MouseDown on zone {ZoneId} at {e.Location}, button: {e.Button}", "DropZone");
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        _logger.Verbose($"Mouse enter zone: {ZoneId}", "DropZone");
        _onMouseEnter?.Invoke(ZoneId);
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        _logger.Verbose($"Mouse leave zone: {ZoneId}", "DropZone");
        _onMouseLeave?.Invoke(ZoneId);
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
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logger.Info($"DropZoneOverlay disposed: {ZoneId}", "DropZone");
        }
        base.Dispose(disposing);
    }
}
