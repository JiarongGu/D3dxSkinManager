using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Overlay panel that captures file drag-drop events
///
/// SIMPLIFIED LOGIC:
/// - Frontend sends bounds and occlusion state
/// - Backend tracks mouse position (inside/outside zone)
/// - Backend tracks file drag state
/// - Visibility: Show when (file dragging OR (mouse outside AND not occluded))
/// - Debounced visibility updates (50-100ms) prevent excessive UI changes
/// </summary>
public class DropZoneOverlay : Panel
{
    private readonly ILogHelper _logger;
    private readonly Action<string[], Point> _onFileDrop;
    private readonly Action<string> _onDragEnter;
    private readonly Action<string> _onDragLeave;
    private readonly Action<string>? _onMouseEnter;
    private readonly Action<string>? _onMouseLeave;
    private readonly WebView2? _webView;
    public string ZoneId { get; }

    // State tracking
    private bool _mouseIsInside = false;  // Is mouse currently inside zone bounds?
    private bool _isOccluded = false;     // Is zone covered by other HTML elements? (from frontend)
    private bool _isDragging = false;      // Is a file drag operation in progress?

    // Visibility management
    private readonly WinFormsDebounce _visibilityDebounce;
    private global::System.Windows.Forms.Timer? _mouseTrackTimer;
    private bool _isDisposed = false;

    public DropZoneOverlay(
        string zoneId,
        ILogHelper logger,
        WebView2 webView,
        Action<string[], Point> onFileDrop,
        Action<string> onDragEnter,
        Action<string> onDragLeave,
        Action<string>? onMouseEnter = null,
        Action<string>? onMouseLeave = null)
    {
        ZoneId = zoneId;
        _logger = logger;
        _webView = webView;
        _onFileDrop = onFileDrop;
        _onDragEnter = onDragEnter;
        _onDragLeave = onDragLeave;
        _onMouseEnter = onMouseEnter;
        _onMouseLeave = onMouseLeave;

        // Make overlay transparent
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AllowDrop = true;

        // Wire up drag events
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        DragLeave += OnDragLeave;
        DragOver += OnDragOver;

        // Wire up mouse events
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        // Setup debounced visibility updates (75ms)
        _visibilityDebounce = new WinFormsDebounce(75);

        // Setup mouse position tracking timer (check every 100ms)
        _mouseTrackTimer = new global::System.Windows.Forms.Timer();
        _mouseTrackTimer.Interval = 100;
        _mouseTrackTimer.Tick += CheckMousePosition;
        _mouseTrackTimer.Start();

        _logger.Info($"DropZoneOverlay created: {zoneId}", "DropZone");
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TRANSPARENT = 0x20;

            var cp = base.CreateParams;
            // Use TRANSPARENT for visual transparency
            cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Don't paint - overlay is invisible
    }

    /// <summary>
    /// Check mouse position periodically to detect if it left the zone without triggering events
    /// </summary>
    private void CheckMousePosition(object? sender, EventArgs e)
    {
        // Safety check: don't process if disposed
        if (_isDisposed || IsDisposed)
        {
            return;
        }

        try
        {
            var cursorPos = Cursor.Position;
            var overlayPt = PointToClient(cursorPos);
            bool mouseCurrentlyInside = ClientRectangle.Contains(overlayPt);

            // If mouse state changed, update it
            if (mouseCurrentlyInside != _mouseIsInside)
            {
                _mouseIsInside = mouseCurrentlyInside;

                // Notify frontend about mouse state change
                if (_mouseIsInside)
                {
                    _onMouseEnter?.Invoke(ZoneId);
                }
                else
                {
                    _onMouseLeave?.Invoke(ZoneId);
                }

                // Recalculate visibility
                UpdateVisibility();
            }
        }
        catch (ObjectDisposedException)
        {
            // Control was disposed, stop the timer
            _mouseTrackTimer?.Stop();
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            _isDragging = true;
            _logger.Debug($"Drag enter zone: {ZoneId}", "DropZone");
            _onDragEnter?.Invoke(ZoneId);

            // Recalculate visibility (drag started, should be visible)
            UpdateVisibility();
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
        _logger.Debug($"Drag leave zone: {ZoneId}", "DropZone");
        _onDragLeave?.Invoke(ZoneId);

        // Recalculate visibility (drag ended)
        UpdateVisibility();
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
            // Recalculate visibility (drag ended)
            UpdateVisibility();
        }
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        _mouseIsInside = true;
        _onMouseEnter?.Invoke(ZoneId);

        // Recalculate visibility (mouse entered)
        UpdateVisibility();
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        _mouseIsInside = false;
        _onMouseLeave?.Invoke(ZoneId);

        // Recalculate visibility (mouse left)
        UpdateVisibility();
    }

    /// <summary>
    /// Set occlusion state from frontend
    /// Frontend checks if zone is covered by other HTML elements
    /// </summary>
    public void SetOccluded(bool isOccluded)
    {
        if (_isOccluded != isOccluded)
        {
            _isOccluded = isOccluded;
            _logger.Verbose($"Zone {ZoneId} occlusion changed: {isOccluded}", "DropZone");

            // Recalculate visibility
            UpdateVisibility();
        }
    }

    /// <summary>
    /// Calculate and schedule visibility update with debouncing
    ///
    /// Logic: Show zone when:
    /// - File is being dragged over zone, OR
    /// - Mouse is outside zone AND zone is not occluded
    /// </summary>
    private void UpdateVisibility()
    {
        // Calculate desired visibility
        // Show if: dragging OR (mouse outside AND not occluded)
        bool shouldBeVisible = _isDragging || (!_mouseIsInside && !_isOccluded);

        // Only update if state actually changed
        if (shouldBeVisible == Visible)
        {
            // Already in correct state, cancel any pending updates
            _visibilityDebounce.Cancel();
            return;
        }

        // Schedule visibility update with debouncing (75ms)
        _visibilityDebounce.Execute(() => ApplyVisibilityUpdate(shouldBeVisible));
    }

    /// <summary>
    /// Apply visibility update after debounce period
    /// </summary>
    private void ApplyVisibilityUpdate(bool targetVisibility)
    {
        // Double-check current state matches target (may have changed during debounce)
        // Recalculate: Show if dragging OR (mouse outside AND not occluded)
        bool currentShouldBeVisible = _isDragging || (!_mouseIsInside && !_isOccluded);

        if (currentShouldBeVisible != targetVisibility)
        {
            // State changed during debounce, schedule another update
            UpdateVisibility();
            return;
        }

        // Apply visibility change
        if (currentShouldBeVisible && !Visible)
        {
            Visible = true;
            _logger.Verbose($"Zone {ZoneId} shown (dragging={_isDragging}, mouseInside={_mouseIsInside}, occluded={_isOccluded})", "DropZone");
        }
        else if (!currentShouldBeVisible && Visible)
        {
            Visible = false;
            _logger.Verbose($"Zone {ZoneId} hidden (dragging={_isDragging}, mouseInside={_mouseIsInside}, occluded={_isOccluded})", "DropZone");
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

            // Stop and dispose debounce and timers
            _visibilityDebounce?.Dispose();

            _mouseTrackTimer?.Stop();
            _mouseTrackTimer?.Dispose();
            _mouseTrackTimer = null;

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
