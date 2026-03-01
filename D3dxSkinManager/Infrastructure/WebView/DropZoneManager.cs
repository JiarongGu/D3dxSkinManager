using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Manages drop zone overlays using event-driven drag detection from FileDragDetector.
/// </summary>
public class DropZoneManager : IDisposable
{
    #region Fields

    private readonly WebView2 _webView;
    private readonly Form _parentForm;
    private readonly ILogHelper _logger;
    private readonly IpcHandler _ipcHandler;

    // Drag detection
    private readonly FileDragDetector _dragDetector;

    // Zone management
    private readonly Dictionary<string, DropZoneOverlay> _activeOverlays = new();
    private readonly Dictionary<string, ZoneMetadata> _registeredZones = new();

    // Cleanup synchronization
    private TaskCompletionSource<bool>? _dropCompletionSource;

    private class ZoneMetadata
    {
        public required string ZoneId { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    #endregion

    #region Initialization & Cleanup

    public DropZoneManager(WebView2 webView, Form parentForm, ILogHelper logger, IpcHandler ipcHandler)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ipcHandler = ipcHandler;

        // Create drag detector and subscribe to events
        _dragDetector = new FileDragDetector(parentForm, logger, bringToFrontOnDrag: false);
        _dragDetector.DragStarted += OnDragStarted;
        _dragDetector.DragMoved += OnDragMoved;
        _dragDetector.DragEnded += OnDragEnded;

        _logger.Info("DropZoneManager initialized with FileDragDetector", "DropZone");
    }

    public void Dispose()
    {
        // Unsubscribe from events
        if (_dragDetector != null)
        {
            _dragDetector.DragStarted -= OnDragStarted;
            _dragDetector.DragMoved -= OnDragMoved;
            _dragDetector.DragEnded -= OnDragEnded;
            _dragDetector.Dispose();
        }

        DestroyAllOverlays();
        _registeredZones.Clear();

        _logger.Info("DropZoneManager disposed", "DropZone");
    }

    #endregion

    #region Drag Event Handlers

    private void OnDragStarted(object? sender, FileDragDetector.DragEventArgs e)
    {
        _logger.Info($"Drag started at {e.ScreenPosition} ({e.DetectionMethod})", "DropZone");

        // Create new TaskCompletionSource for this drag operation
        _dropCompletionSource = new TaskCompletionSource<bool>();

        // Check if mouse is over any zones and create overlays
        CheckMouseOverZones(e.ScreenPosition);
    }

    private void OnDragMoved(object? sender, FileDragDetector.DragEventArgs e)
    {
        // Check if mouse is over any zones and create overlays on-demand
        CheckMouseOverZones(e.ScreenPosition);
    }

    private async void OnDragEnded(object? sender, EventArgs e)
    {
        var tcs = _dropCompletionSource;
        if (tcs == null)
            return;

        // Wait for drop event to complete
        await tcs.Task;

        // Always destroy overlays after drop completes
        _logger.Info("Drag ended, cleaning up overlays", "DropZone");

        if (_parentForm.InvokeRequired)
        {
            _parentForm.Invoke(() => DestroyAllOverlays());
        }
        else
        {
            DestroyAllOverlays();
        }
    }

    private void CheckMouseOverZones(Point screenPosition)
    {
        var visibleZones = _registeredZones.Values.Where(z => z.IsVisible).ToList();

        if (visibleZones.Count == 0)
        {
            // No zones - destroy any active overlays
            DestroyAllOverlays();
            return;
        }

        // Track which zones the mouse is currently over
        var zonesUnderMouse = new HashSet<string>();

        foreach (var metadata in visibleZones)
        {
            // Convert zone bounds to screen coordinates
            var zoneScreenPos = _webView.PointToScreen(new Point(metadata.X, metadata.Y));
            var zoneBounds = new Rectangle(zoneScreenPos.X, zoneScreenPos.Y, metadata.Width, metadata.Height);

            if (zoneBounds.Contains(screenPosition))
            {
                zonesUnderMouse.Add(metadata.ZoneId);

                // Mouse is over this zone - create overlay if not exists
                if (!_activeOverlays.ContainsKey(metadata.ZoneId))
                {
                    _logger.Info($"✓ Mouse entered zone {metadata.ZoneId}! Creating overlay...", "DropZone");
                    CreateOverlay(metadata);
                }
            }
        }

        // Destroy overlays for zones the mouse has left
        var zonesToRemove = _activeOverlays.Keys.Where(id => !zonesUnderMouse.Contains(id)).ToList();
        foreach (var zoneId in zonesToRemove)
        {
            _logger.Info($"Mouse left zone {zoneId}, destroying overlay", "DropZone");

            // Notify frontend that drag left the zone
            NotifyDragLeave(zoneId);

            DestroyOverlay(zoneId);
        }
    }

    #endregion

    #region Overlay Management

    private void CreateOverlay(ZoneMetadata metadata)
    {
        if (_activeOverlays.ContainsKey(metadata.ZoneId))
            return;

        // Ensure we're on the UI thread
        if (_parentForm.InvokeRequired)
        {
            _parentForm.Invoke(() => CreateOverlay(metadata));
            return;
        }

        try
        {
            var overlay = new DropZoneOverlay(
                metadata.ZoneId,
                _logger,
                (files, pos) => NotifyFileDrop(metadata.ZoneId, files, pos),
                (id) => NotifyDragEnter(id),
                (id) => NotifyDragLeave(id),
                (pos) => NotifyClick(metadata.ZoneId, pos),
                (id) => NotifyMouseEnter(id),
                (id) => NotifyMouseLeave(id)
            );

            // Convert WebView coordinates to Form coordinates
            var screenPos = _webView.PointToScreen(new Point(metadata.X, metadata.Y));
            var formPos = _parentForm.PointToClient(screenPos);

            _logger.Info($"Creating overlay {metadata.ZoneId}: WebView({metadata.X},{metadata.Y}) -> Screen({screenPos.X},{screenPos.Y}) -> Form({formPos.X},{formPos.Y}) Size({metadata.Width}x{metadata.Height})", "DropZone");

            // Set bounds and add to parent form
            overlay.SetBounds(formPos.X, formPos.Y, metadata.Width, metadata.Height);
            _parentForm.Controls.Add(overlay);
            overlay.BringToFront();
            overlay.Visible = true;

            _activeOverlays[metadata.ZoneId] = overlay;
            _logger.Info($"✓ Overlay created successfully: {metadata.ZoneId}, Visible={overlay.Visible}, Bounds=({overlay.Left},{overlay.Top},{overlay.Width}x{overlay.Height})", "DropZone");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create overlay {metadata.ZoneId}: {ex.Message}", "DropZone", ex);
        }
    }

    private void DestroyAllOverlays()
    {
        foreach (var (_, overlay) in _activeOverlays.ToList())
        {
            _parentForm.Controls.Remove(overlay);
            overlay.Dispose();
        }
        _activeOverlays.Clear();
    }

    private void DestroyOverlay(string zoneId)
    {
        if (_activeOverlays.TryGetValue(zoneId, out var overlay))
        {
            _parentForm.Controls.Remove(overlay);
            overlay.Dispose();
            _activeOverlays.Remove(zoneId);
        }
    }

    #endregion

    #region Public API - Zone Registration

    public void RegisterZone(string zoneId, int x, int y, int width, int height)
    {
        if (_registeredZones.TryGetValue(zoneId, out var existing))
        {
            // Update existing zone bounds
            existing.X = x;
            existing.Y = y;
            existing.Width = width;
            existing.Height = height;

            // Update active overlay if it exists (overlays use form coordinates)
            if (_activeOverlays.TryGetValue(zoneId, out var overlay))
            {
                var screenPos = _webView.PointToScreen(new Point(x, y));
                var formPos = _parentForm.PointToClient(screenPos);
                overlay.UpdateBounds(formPos.X, formPos.Y, width, height);
            }

            _logger.Debug($"Zone updated: {zoneId}", "DropZone");
            return;
        }

        // Register new zone
        var metadata = new ZoneMetadata
        {
            ZoneId = zoneId,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsVisible = true
        };

        _registeredZones[zoneId] = metadata;
        _logger.Info($"Zone registered: {zoneId} ({_registeredZones.Count} total)", "DropZone");
    }

    public void UnregisterZone(string zoneId)
    {
        if (_registeredZones.Remove(zoneId))
        {
            DestroyOverlay(zoneId);
            _logger.Info($"Zone unregistered: {zoneId}", "DropZone");
        }
    }

    public void UpdateZoneBounds(string zoneId, int x, int y, int width, int height)
    {
        RegisterZone(zoneId, x, y, width, height);
    }

    public void ShowZone(string zoneId)
    {
        if (_registeredZones.TryGetValue(zoneId, out var metadata))
        {
            metadata.IsVisible = true;

            if (_activeOverlays.TryGetValue(zoneId, out var overlay))
            {
                overlay.Show();
            }
        }
    }

    public void HideZone(string zoneId)
    {
        if (_registeredZones.TryGetValue(zoneId, out var metadata))
        {
            metadata.IsVisible = false;

            if (_activeOverlays.TryGetValue(zoneId, out var overlay))
            {
                overlay.Hide();
            }
        }
    }

    public void ClearAll()
    {
        DestroyAllOverlays();
        _registeredZones.Clear();
        _logger.Info("All zones cleared", "DropZone");
    }

    #endregion

    #region Event Notifications

    private void NotifyDragEnter(string zoneId)
    {
        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.DRAG_ENTER, new { zoneId });
    }

    private void NotifyDragLeave(string zoneId)
    {
        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.DRAG_LEAVE, new { zoneId });
    }

    private void NotifyClick(string zoneId, Point position)
    {
        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.CLICK, new
        {
            zoneId,
            position = new { x = position.X, y = position.Y }
        });
    }

    private void NotifyMouseEnter(string zoneId)
    {
        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.MOUSE_ENTER, new { zoneId });
    }

    private void NotifyMouseLeave(string zoneId)
    {
        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.MOUSE_LEAVE, new { zoneId });
    }

    private void NotifyFileDrop(string zoneId, string[] files, Point position)
    {
        _logger.Info($"Files dropped on {zoneId}: {string.Join(", ", files)}", "DropZone");

        _ipcHandler?.SendNotification(ModuleNames.DROP_ZONE, DropZoneEvents.FILE_DROP, new
        {
            zoneId,
            files,
            position = new { x = position.X, y = position.Y }
        });

        // Signal that drop event has completed
        // OnDragEnded will destroy overlays after this
        _dropCompletionSource?.TrySetResult(true);
    }

    #endregion
}
