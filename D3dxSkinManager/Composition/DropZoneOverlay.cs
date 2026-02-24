using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Composition;

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

    public void UpdateBounds(int x, int y, int width, int height)
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

/// <summary>
/// Manages multiple drop zone overlays
/// </summary>
public class DropZoneManager
{
    private readonly WebView2 _webView;
    private readonly Form _parentForm;
    private readonly ILogHelper _logger;
    private readonly IpcCommunicationHandler _ipcHandler;
    private readonly Dictionary<string, DropZoneOverlay> _zones = new();

    public DropZoneManager(WebView2 webView, Form parentForm, ILogHelper logger, IpcCommunicationHandler ipcHandler)
    {
        _webView = webView;
        _parentForm = parentForm;
        _logger = logger;
        _ipcHandler = ipcHandler;
    }

    public void RegisterZone(string zoneId, int x, int y, int width, int height)
    {
        if (_zones.ContainsKey(zoneId))
        {
            _logger.Warn($"Zone {zoneId} already registered, updating bounds", "DropZoneManager");
            UpdateZoneBounds(zoneId, x, y, width, height);
            return;
        }

        var overlay = new DropZoneOverlay(
            zoneId,
            _logger,
            (files, pos) => OnFilesDropped(zoneId, files, pos),
            (id) => OnDragEnter(id),
            (id) => OnDragLeave(id),
            (pos) => OnClick(zoneId, pos),
            (id) => OnMouseEnter(id),
            (id) => OnMouseLeave(id)
        );

        // Convert WebView2 coordinates to Form coordinates
        var webViewLocation = _webView.PointToScreen(new Point(x, y));
        var formLocation = _parentForm.PointToClient(webViewLocation);

        overlay.UpdateBounds(formLocation.X, formLocation.Y, width, height);
        _parentForm.Controls.Add(overlay);
        overlay.BringToFront();
        overlay.Show();

        _zones[zoneId] = overlay;
        _logger.Info($"Zone registered: {zoneId} at WebView({x}, {y}) -> Form({formLocation.X}, {formLocation.Y}), {width}x{height}", "DropZoneManager");
    }

    public void UpdateZoneBounds(string zoneId, int x, int y, int width, int height)
    {
        if (_zones.TryGetValue(zoneId, out var overlay))
        {
            // Convert WebView2 coordinates to Form coordinates
            var webViewLocation = _webView.PointToScreen(new Point(x, y));
            var formLocation = _parentForm.PointToClient(webViewLocation);

            overlay.UpdateBounds(formLocation.X, formLocation.Y, width, height);
        }
        else
        {
            _logger.Warn($"Attempted to update non-existent zone: {zoneId}", "DropZoneManager");
        }
    }

    public void ShowZone(string zoneId)
    {
        if (_zones.TryGetValue(zoneId, out var overlay))
        {
            overlay.Show();
        }
    }

    public void HideZone(string zoneId)
    {
        if (_zones.TryGetValue(zoneId, out var overlay))
        {
            overlay.Hide();
        }
    }

    public void UnregisterZone(string zoneId)
    {
        if (_zones.TryGetValue(zoneId, out var overlay))
        {
            _parentForm.Controls.Remove(overlay);
            overlay.Dispose();
            _zones.Remove(zoneId);
            _logger.Info($"Zone unregistered: {zoneId}", "DropZoneManager");
        }
    }

    private void OnDragEnter(string zoneId)
    {
        _logger.Debug($"Drag enter zone {zoneId}", "DropZoneManager");
        _ipcHandler?.SendNotification(DropZoneEvents.DRAG_ENTER, new { zoneId });
    }

    private void OnDragLeave(string zoneId)
    {
        _logger.Debug($"Drag leave zone {zoneId}", "DropZoneManager");
        _ipcHandler?.SendNotification(DropZoneEvents.DRAG_LEAVE, new { zoneId });
    }

    private void OnClick(string zoneId, Point position)
    {
        _logger.Debug($"Click on zone {zoneId} at {position}", "DropZoneManager");

        // Send click event to frontend via IPC
        _ipcHandler?.SendNotification(DropZoneEvents.CLICK, new
        {
            zoneId,
            position = new { x = position.X, y = position.Y }
        });
    }

    private void OnMouseEnter(string zoneId)
    {
        _logger.Verbose($"Mouse enter zone {zoneId}", "DropZoneManager");
        _ipcHandler?.SendNotification(DropZoneEvents.MOUSE_ENTER, new { zoneId });
    }

    private void OnMouseLeave(string zoneId)
    {
        _logger.Verbose($"Mouse leave zone {zoneId}", "DropZoneManager");
        _ipcHandler?.SendNotification(DropZoneEvents.MOUSE_LEAVE, new { zoneId });
    }

    private void OnFilesDropped(string zoneId, string[] files, Point position)
    {
        _logger.Info($"Files dropped on zone {zoneId}: {string.Join(", ", files)}", "DropZoneManager");

        // Send notification to frontend with zone ID
        _ipcHandler?.SendNotification(DropZoneEvents.FILE_DROP, new
        {
            zoneId,
            files,
            position = new { x = position.X, y = position.Y }
        });
    }

    public void ClearAll()
    {
        foreach (var zoneId in _zones.Keys.ToList())
        {
            UnregisterZone(zoneId);
        }
    }
}
