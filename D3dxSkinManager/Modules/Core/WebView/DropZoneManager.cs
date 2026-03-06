using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;

namespace D3dxSkinManager.Modules.Core.WebView;

/// <summary>
/// Manages drop zone overlays that capture all mouse events and forward them to frontend.
/// Overlays are created immediately when zones are registered and act as transparent pass-through layers.
/// </summary>
public class DropZoneManager : IDisposable
{
    #region Fields

    private readonly WebView2 _webView;
    private readonly Form _parentForm;
    private readonly ILogHelper _logger;
    private readonly IpcHandler _ipcHandler;

    // Zone management - overlays created immediately on registration
    private readonly Dictionary<string, DropZoneOverlay> _activeOverlays = new();

    #endregion

    #region Initialization & Cleanup

    public DropZoneManager(WebView2 webView, Form parentForm, ILogHelper logger, IpcHandler ipcHandler)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ipcHandler = ipcHandler;

        _logger.Info("DropZoneManager initialized (overlays created on registration)", "DropZone");
    }

    public void Dispose()
    {
        DestroyAllOverlays();
        _logger.Info("DropZoneManager disposed", "DropZone");
    }

    #endregion

    #region Overlay Management

    private void CreateOverlay(string zoneId, int x, int y, int width, int height)
    {
        if (_activeOverlays.ContainsKey(zoneId))
            return;

        // Ensure we're on the UI thread
        if (_parentForm.InvokeRequired)
        {
            _parentForm.Invoke(() => CreateOverlay(zoneId, x, y, width, height));
            return;
        }

        try
        {
            var overlay = new DropZoneOverlay(
                zoneId,
                _logger,
                _webView,
                (files, pos) => NotifyFileDrop(zoneId, files, pos),
                (id) => NotifyDragEnter(id),
                (id) => NotifyDragLeave(id),
                (id) => NotifyMouseEnter(id),
                (id) => NotifyMouseLeave(id),
                (id) => UnregisterZone(id)
            );

            // Convert WebView coordinates to Form coordinates
            var screenPos = _webView.PointToScreen(new Point(x, y));
            var formPos = _parentForm.PointToClient(screenPos);

            _logger.Info($"Creating overlay {zoneId}: WebView({x},{y}) -> Screen({screenPos.X},{screenPos.Y}) -> Form({formPos.X},{formPos.Y}) Size({width}x{height})", "DropZone");

            // Set bounds and add to parent form
            overlay.SetBounds(formPos.X, formPos.Y, width, height);
            _parentForm.Controls.Add(overlay);
            overlay.BringToFront();
            overlay.Visible = true;

            _activeOverlays[zoneId] = overlay;
            _logger.Info($"✓ Overlay created successfully: {zoneId}, Visible={overlay.Visible}, Bounds=({overlay.Left},{overlay.Top},{overlay.Width}x{overlay.Height})", "DropZone");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create overlay {zoneId}: {ex.Message}", "DropZone", ex);
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
        if (_activeOverlays.ContainsKey(zoneId))
        {
            // Update existing overlay bounds
            var screenPos = _webView.PointToScreen(new Point(x, y));
            var formPos = _parentForm.PointToClient(screenPos);
            _activeOverlays[zoneId].UpdateBounds(formPos.X, formPos.Y, width, height);
            _logger.Debug($"Zone updated: {zoneId}", "DropZone");
            return;
        }

        // Create overlay immediately for new zone
        CreateOverlay(zoneId, x, y, width, height);
        _logger.Info($"Zone registered and overlay created: {zoneId} ({_activeOverlays.Count} total)", "DropZone");
    }

    public void UnregisterZone(string zoneId)
    {
        if (_activeOverlays.ContainsKey(zoneId))
        {
            DestroyOverlay(zoneId);
            _logger.Info($"Zone unregistered and overlay destroyed: {zoneId}", "DropZone");
        }
    }

    public void UpdateZoneBounds(string zoneId, int x, int y, int width, int height)
    {
        RegisterZone(zoneId, x, y, width, height);
    }

    public void ShowZone(string zoneId)
    {
        if (_activeOverlays.TryGetValue(zoneId, out var overlay))
        {
            // Trust frontend's decision about visibility
            // Frontend handles element visibility and occlusion checks
            overlay.Show();
        }
    }

    public void HideZone(string zoneId)
    {
        if (_activeOverlays.TryGetValue(zoneId, out var overlay))
        {
            overlay.Hide();
        }
    }

    public void ClearAll()
    {
        DestroyAllOverlays();
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
    }

    #endregion
}
