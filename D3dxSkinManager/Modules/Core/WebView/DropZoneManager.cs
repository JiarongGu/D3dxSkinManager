using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Utilities;

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

        // Show all overlays when form loses/gains focus to ensure correct state
        _parentForm.Deactivate += OnFormDeactivate;
        _parentForm.Activated += OnFormActivated;

        _logger.Info("DropZoneManager initialized (overlays created on registration)", "DropZone");
    }

    public void Dispose()
    {
        if (_parentForm != null)
        {
            _parentForm.Deactivate -= OnFormDeactivate;
            _parentForm.Activated -= OnFormActivated;
        }
        DestroyAllOverlays();
        _logger.Info("DropZoneManager disposed", "DropZone");
    }

    private void OnFormDeactivate(object? sender, EventArgs e)
    {
        // Form lost focus - mark all overlays as inactive (keeps them visible for drag-drop from other apps)
        foreach (var overlay in _activeOverlays.Values)
        {
            overlay.SetFormActive(false);
        }
        _logger.Debug("Form deactivated - all overlays set to inactive mode", "DropZone");
    }

    private void OnFormActivated(object? sender, EventArgs e)
    {
        // Form regained focus - update overlay visibility based on current mouse position
        foreach (var overlay in _activeOverlays.Values)
        {
            overlay.SetFormActive(true);
        }
        _logger.Debug("Form activated - overlays updated based on mouse position", "DropZone");
    }

    #endregion

    #region Overlay Management

    /// <summary>
    /// Converts CSS pixel bounds (logical, from JavaScript getBoundingClientRect) to
    /// physical pixel bounds needed by Win32 PointToScreen/PointToClient and SetBounds.
    /// PointToScreen is a raw Win32 ClientToScreen call that works in physical pixels.
    /// CSS pixels are device-independent logical pixels; at 150% DPI each CSS pixel = 1.5 physical pixels.
    /// </summary>
    private static (int physX, int physY, int physWidth, int physHeight) ToPhysicalPixels(int cssX, int cssY, int cssWidth, int cssHeight)
    {
        double dpi = DpiHelper.GetDpiScaleFactor();
        return (
            (int)Math.Round(cssX * dpi),
            (int)Math.Round(cssY * dpi),
            (int)Math.Round(cssWidth * dpi),
            (int)Math.Round(cssHeight * dpi)
        );
    }

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
                (id) => NotifyDragLeave(id)
            );

            // Convert CSS logical pixels → physical pixels → Form coordinates.
            // PointToScreen/PointToClient are raw Win32 calls that work in physical pixels.
            var (physX, physY, physWidth, physHeight) = ToPhysicalPixels(x, y, width, height);
            var screenPos = _webView.PointToScreen(new Point(physX, physY));
            var formPos = _parentForm.PointToClient(screenPos);

            _logger.Info($"Creating overlay {zoneId}: CSS({x},{y} {width}x{height}) Phys({physX},{physY}) -> Screen({screenPos.X},{screenPos.Y}) -> Form({formPos.X},{formPos.Y}) PhysSize({physWidth}x{physHeight})", "DropZone");

            // Set bounds and add to parent form
            overlay.SetBounds(formPos.X, formPos.Y, physWidth, physHeight);
            _parentForm.Controls.Add(overlay);
            overlay.BringToFront();

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
            // Update existing overlay bounds — same CSS→physical conversion as CreateOverlay
            var (physX, physY, physWidth, physHeight) = ToPhysicalPixels(x, y, width, height);
            var screenPos = _webView.PointToScreen(new Point(physX, physY));
            var formPos = _parentForm.PointToClient(screenPos);
            _activeOverlays[zoneId].UpdateBounds(formPos.X, formPos.Y, physWidth, physHeight);
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

    public void ShowOverlay(string zoneId)
    {
        if (_activeOverlays.TryGetValue(zoneId, out var overlay))
        {
            overlay.OnFrontendMouseLeave();
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
