using Microsoft.Web.WebView2.WinForms;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Infrastructure.WebView;

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

/// <summary>
/// Overlay panel that captures drag-drop and forwards mouse events to WebView2
/// </summary>
public class DropZoneOverlay : Panel
{
    #region JavaScript Scripts

    private const string JS_HANDLE_HOVER_AND_CURSOR = @"
        (function(x, y) {
            const element = document.elementFromPoint(x, y);
            const elementPath = element ? element.tagName + '.' + element.className : null;

            // Get computed cursor style
            let cursorStyle = 'default';
            if (element) {
                cursorStyle = window.getComputedStyle(element).cursor || 'default';
            }

            // Initialize hover tracking
            if (!window.__lastHoveredElement) {
                window.__lastHoveredElement = null;
            }
            if (!window.__hoverStyleTag) {
                window.__hoverStyleTag = document.createElement('style');
                window.__hoverStyleTag.id = '__overlay_hover_styles';
                document.head.appendChild(window.__hoverStyleTag);
            }

            const lastElement = window.__lastHoveredElement;

            // If element changed, update hover classes and dispatch events
            if (lastElement !== element) {
                // Remove hover class from old element and its parents
                if (lastElement) {
                    let el = lastElement;
                    while (el && el !== document.body) {
                        el.classList.remove('__overlay_hover');
                        el = el.parentElement;
                    }

                    lastElement.dispatchEvent(new MouseEvent('mouseout', {
                        bubbles: true, cancelable: true, view: window,
                        clientX: x, clientY: y, relatedTarget: element
                    }));
                    lastElement.dispatchEvent(new MouseEvent('mouseleave', {
                        bubbles: false, cancelable: false, view: window,
                        clientX: x, clientY: y, relatedTarget: element
                    }));
                }

                // Add hover class to new element and its parents
                if (element) {
                    let el = element;
                    while (el && el !== document.body) {
                        el.classList.add('__overlay_hover');
                        el = el.parentElement;
                    }

                    element.dispatchEvent(new MouseEvent('mouseenter', {
                        bubbles: false, cancelable: false, view: window,
                        clientX: x, clientY: y, relatedTarget: lastElement
                    }));
                    element.dispatchEvent(new MouseEvent('mouseover', {
                        bubbles: true, cancelable: true, view: window,
                        clientX: x, clientY: y, relatedTarget: lastElement
                    }));
                }

                window.__lastHoveredElement = element;
            }

            // Always fire mousemove
            if (element) {
                element.dispatchEvent(new MouseEvent('mousemove', {
                    bubbles: true, cancelable: true, view: window,
                    clientX: x, clientY: y
                }));
            }

            return JSON.stringify({ element: elementPath, cursor: cursorStyle });
        })";

    private const string JS_CLEANUP_HOVER = @"
        (function() {
            if (window.__lastHoveredElement) {
                let el = window.__lastHoveredElement;
                while (el && el !== document.body) {
                    el.classList.remove('__overlay_hover');
                    el = el.parentElement;
                }
                window.__lastHoveredElement = null;
            }
            return 'Hover cleanup done';
        })();";

    private const string JS_FIND_SCROLLABLE_ELEMENTS = @"
        (function() {
            const scrollableElements = [];

            function checkScrollable(element) {
                const style = window.getComputedStyle(element);
                const hasVerticalScroll = element.scrollHeight > element.clientHeight &&
                                         (style.overflowY === 'auto' || style.overflowY === 'scroll');
                const hasHorizontalScroll = element.scrollWidth > element.clientWidth &&
                                           (style.overflowX === 'auto' || style.overflowX === 'scroll');

                if (hasVerticalScroll || hasHorizontalScroll) {
                    const rect = element.getBoundingClientRect();
                    scrollableElements.push({
                        x: Math.round(rect.left),
                        y: Math.round(rect.top),
                        width: Math.round(rect.width),
                        height: Math.round(rect.height),
                        clientWidth: element.clientWidth,
                        clientHeight: element.clientHeight,
                        hasVerticalScroll: hasVerticalScroll,
                        hasHorizontalScroll: hasHorizontalScroll
                    });
                }

                for (let child of element.children) {
                    checkScrollable(child);
                }
            }

            checkScrollable(document.body);
            return JSON.stringify(scrollableElements);
        })();";

    private const string JS_INJECT_HOVER_STYLES = @"
        (function() {
            // Create a style tag that copies all :hover rules to .__overlay_hover
            const style = document.createElement('style');
            style.id = '__overlay_hover_mirror';

            // This CSS will make elements with __overlay_hover class look like they're hovered
            style.textContent = `
                /* Apply hover-like styles to elements with __overlay_hover class */
                .__overlay_hover:hover,
                .__overlay_hover {
                    /* Inherit all hover behaviors */
                }
            `;

            document.head.appendChild(style);

            // Dynamically copy :hover rules
            setTimeout(() => {
                const sheets = Array.from(document.styleSheets);
                let hoverRules = [];

                sheets.forEach(sheet => {
                    try {
                        const rules = Array.from(sheet.cssRules || []);
                        rules.forEach(rule => {
                            if (rule.selectorText && rule.selectorText.includes(':hover')) {
                                const newSelector = rule.selectorText.replace(/:hover/g, '.__overlay_hover');
                                hoverRules.push(`${newSelector} { ${rule.style.cssText} }`);
                            }
                        });
                    } catch (e) {
                        // Skip cross-origin stylesheets
                    }
                });

                style.textContent += '\n' + hoverRules.join('\n');
            }, 500);

            return 'Hover styles injected';
        })();";

    private const string JS_DISPATCH_MOUSE_EVENT_TEMPLATE = @"
        (function() {{
            try {{
                const element = document.elementFromPoint({0}, {1});
                if (!element) {{
                    return JSON.stringify({{ error: 'No element at (' + {0} + ',' + {1} + ')', viewport: {{ width: window.innerWidth, height: window.innerHeight }} }});
                }}

                if ('{2}' === 'wheel') {{
                    // Find scrollable parent
                    let scrollable = element;
                    while (scrollable && scrollable !== document.documentElement) {{
                        const style = window.getComputedStyle(scrollable);
                        const overflowY = style.overflowY;
                        const overflowX = style.overflowX;

                        if ((overflowY === 'auto' || overflowY === 'scroll') && scrollable.scrollHeight > scrollable.clientHeight) {{
                            break;
                        }}
                        if ((overflowX === 'auto' || overflowX === 'scroll') && scrollable.scrollWidth > scrollable.clientWidth) {{
                            break;
                        }}
                        scrollable = scrollable.parentElement;
                    }}

                    if (!scrollable) scrollable = document.documentElement;

                    // Perform actual scroll
                    const scrollAmount = {3} / 3;
                    scrollable.scrollTop += scrollAmount;

                    // Also dispatch wheel event for React handlers
                    const wheelEvent = new WheelEvent('wheel', {{
                        bubbles: true,
                        cancelable: true,
                        view: window,
                        clientX: {0},
                        clientY: {1},
                        deltaY: scrollAmount,
                        deltaMode: 0
                    }});
                    element.dispatchEvent(wheelEvent);

                    return 'Scrolled ' + scrollable.tagName + ' by ' + scrollAmount;
                }} else {{
                    const event = new MouseEvent('{2}', {{
                        bubbles: true,
                        cancelable: true,
                        view: window,
                        clientX: {0},
                        clientY: {1},
                        button: {4}
                    }});
                    element.dispatchEvent(event);

                    // For click events, also dispatch a 'click' event
                    if ('{2}' === 'mouseup') {{
                        element.dispatchEvent(new MouseEvent('click', {{
                            bubbles: true,
                            cancelable: true,
                            view: window,
                            clientX: {0},
                            clientY: {1},
                            button: {4}
                        }}));
                    }}

                    return element.tagName + '.' + element.className;
                }}
            }} catch (e) {{
                return JSON.stringify({{ error: e.message, stack: e.stack }});
            }}
        }})();";

    #endregion

    #region Windows Messages

    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_MOUSEWHEEL = 0x020A;

    #endregion

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

        // Wire up overlay-level mouse events (not used for forwarding to WebView)
        // Click events are handled via JavaScript injection in WndProc
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;

        // Inject CSS to make __overlay_hover class mimic :hover styles
        InjectHoverStyles();

        // Detect scrollable areas initially
        _ = UpdateScrollableRects();

        // Setup timer to check if overlay should be shown again
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
        // Forward mouse messages to WebView2 (except during drag operations)
        uint msgId = (uint)m.Msg;
        if (_webView != null && !IsDraggingFiles(msgId))
        {
            switch (msgId)
            {
                case WM_MOUSEMOVE:
                    // Check if we're over scrollbar area
                    var movePt = new Point((int)m.LParam & 0xFFFF, (int)m.LParam >> 16);
                    if (IsOverScrollbar(movePt))
                    {
                        // Temporarily hide overlay to allow scrollbar interaction
                        if (Visible)
                        {
                            Visible = false;
                        }
                        return;
                    }
                    ForwardMouseMessageToWebView(m);
                    break;

                case WM_LBUTTONDOWN:
                case WM_LBUTTONUP:
                case WM_RBUTTONDOWN:
                case WM_RBUTTONUP:
                case WM_MBUTTONDOWN:
                case WM_MBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    // For click events, forward to WebView
                    ForwardMouseMessageToWebView(m);
                    break;

                case WM_MOUSEWHEEL:
                    // Always handle wheel scrolling via JavaScript (works anywhere in content)
                    ForwardMouseMessageToWebView(m);
                    break;
            }
        }

        base.WndProc(ref m);
    }

    private async Task UpdateScrollableRects()
    {
        if (_webView?.CoreWebView2 == null)
            return;

        try
        {
            var result = await _webView.CoreWebView2.ExecuteScriptAsync(JS_FIND_SCROLLABLE_ELEMENTS);
            if (!string.IsNullOrEmpty(result) && result != "null")
            {
                var json = System.Text.Json.JsonDocument.Parse(result.Trim('"').Replace("\\\"", "\""));
                _scrollableRects.Clear();

                foreach (var element in json.RootElement.EnumerateArray())
                {
                    var x = element.GetProperty("x").GetInt32();
                    var y = element.GetProperty("y").GetInt32();
                    var width = element.GetProperty("width").GetInt32();
                    var height = element.GetProperty("height").GetInt32();
                    var clientWidth = element.GetProperty("clientWidth").GetInt32();
                    var clientHeight = element.GetProperty("clientHeight").GetInt32();
                    var hasVertical = element.GetProperty("hasVerticalScroll").GetBoolean();
                    var hasHorizontal = element.GetProperty("hasHorizontalScroll").GetBoolean();

                    // Add scrollbar area (right edge for vertical, bottom edge for horizontal)
                    if (hasVertical)
                    {
                        var scrollbarX = x + clientWidth;
                        var scrollbarWidth = width - clientWidth;
                        _scrollableRects.Add(new Rectangle(scrollbarX, y, scrollbarWidth, height));
                    }
                    if (hasHorizontal)
                    {
                        var scrollbarY = y + clientHeight;
                        var scrollbarHeight = height - clientHeight;
                        _scrollableRects.Add(new Rectangle(x, scrollbarY, width, scrollbarHeight));
                    }
                }

                _lastScrollableRectsUpdate = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating scrollable rects: {ex.Message}", "DropZone", ex);
        }
    }

    private void CheckOverlayVisibility(object? sender, EventArgs e)
    {
        if (!Visible)
        {
            // Overlay is hidden, check if mouse is still over scrollbar
            var cursorPos = Cursor.Position;
            var overlayPt = PointToClient(cursorPos);

            // Check if cursor is still within overlay bounds
            if (ClientRectangle.Contains(overlayPt))
            {
                // Cursor is within overlay, check if it's over a scrollbar
                if (!IsOverScrollbar(overlayPt))
                {
                    // Not over scrollbar anymore, show overlay
                    Visible = true;
                }
            }
            else
            {
                // Cursor left overlay area entirely, show overlay
                Visible = true;
            }
        }
    }

    private bool IsOverScrollbar(Point overlayPoint)
    {
        if (_webView == null)
            return false;

        // Update scrollable rects cache every 2 seconds
        if ((DateTime.Now - _lastScrollableRectsUpdate).TotalSeconds > 2)
        {
            _ = UpdateScrollableRects();
        }

        // Convert to WebView coordinates
        var screenPt = PointToScreen(overlayPoint);
        var webViewPt = _webView.PointToClient(screenPt);

        // Check if point is in any scrollbar rect
        foreach (var rect in _scrollableRects)
        {
            if (rect.Contains(webViewPt))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDraggingFiles(uint msg)
    {
        // Check if we're in the middle of a drag operation
        // During drag, don't forward mouse events
        return msg >= 0x0233 && msg <= 0x0238; // WM_DROPFILES through WM_QUERYDROPOBJECT
    }

    private readonly List<Rectangle> _scrollableRects = new();
    private DateTime _lastScrollableRectsUpdate = DateTime.MinValue;
    private System.Windows.Forms.Timer? _visibilityCheckTimer;

    private async void ForwardMouseMessageToWebView(Message m)
    {
        try
        {
            if (_webView?.CoreWebView2 == null)
                return;

            uint msgId = (uint)m.Msg;

            // For wheel events, coordinates are in screen coordinates, not client coordinates
            Point webViewPt;
            if (msgId == WM_MOUSEWHEEL)
            {
                // Wheel events have screen coordinates in lParam
                var screenX = (short)((int)m.LParam & 0xFFFF);
                var screenY = (short)((int)m.LParam >> 16);
                var screenPt = new Point(screenX, screenY);
                webViewPt = _webView.PointToClient(screenPt);
            }
            else
            {
                // Other events have overlay-relative coordinates
                var overlayPt = new Point((int)m.LParam & 0xFFFF, (int)m.LParam >> 16);
                var screenPt = PointToScreen(overlayPt);
                webViewPt = _webView.PointToClient(screenPt);
            }

            // For mousemove, also trigger hover state changes
            if (msgId == WM_MOUSEMOVE)
            {
                await HandleMouseMove(webViewPt.X, webViewPt.Y);
            }
            else
            {
                // For other events (click, scroll, etc.), dispatch them
                await DispatchMouseEvent(msgId, webViewPt.X, webViewPt.Y, m.WParam);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error forwarding mouse event: {ex.Message}", "DropZone", ex);
        }
    }

    private async Task HandleMouseMove(int x, int y)
    {
        var script = $"{JS_HANDLE_HOVER_AND_CURSOR}({x}, {y});";
        var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);

        // Parse result and update cursor
        if (!string.IsNullOrEmpty(result) && result != "null")
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(result.Trim('"').Replace("\\\"", "\""));
                if (json.RootElement.TryGetProperty("cursor", out var cursorProp))
                {
                    var cursorStyle = cursorProp.GetString();
                    UpdateCursorFromStyle(cursorStyle);
                }
            }
            catch { }
        }

        // Only log occasionally to reduce spam
        if (DateTime.Now.Millisecond % 100 < 20)
        {
            _logger.Verbose($"Mouse move at ({x},{y}) -> {result}", "DropZone");
        }
    }

    private async void InjectHoverStyles()
    {
        if (_webView?.CoreWebView2 == null)
            return;

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(JS_INJECT_HOVER_STYLES);
            _logger.Debug("Hover styles injected into WebView", "DropZone");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to inject hover styles: {ex.Message}", "DropZone", ex);
        }
    }

    private void UpdateCursorFromStyle(string? cursorStyle)
    {
        Cursor = cursorStyle switch
        {
            "pointer" => Cursors.Hand,
            "text" => Cursors.IBeam,
            "move" => Cursors.SizeAll,
            "crosshair" => Cursors.Cross,
            "wait" => Cursors.WaitCursor,
            "help" => Cursors.Help,
            "not-allowed" => Cursors.No,
            "grab" => Cursors.Hand,
            "grabbing" => Cursors.Hand,
            _ => Cursors.Default
        };
    }

    private async Task DispatchMouseEvent(uint msgId, int x, int y, IntPtr wParam = default)
    {
        var eventType = msgId switch
        {
            WM_LBUTTONDOWN => "mousedown",
            WM_LBUTTONUP => "mouseup",
            WM_LBUTTONDBLCLK => "dblclick",
            WM_RBUTTONDOWN => "contextmenu",
            WM_MOUSEWHEEL => "wheel",
            _ => null
        };

        if (eventType == null)
            return;

        // Extract wheel delta for scroll events
        int wheelDelta = 0;
        if (msgId == WM_MOUSEWHEEL)
        {
            wheelDelta = (short)((long)wParam >> 16);
        }

        var script = string.Format(JS_DISPATCH_MOUSE_EVENT_TEMPLATE, x, y, eventType, -wheelDelta, GetMouseButton(msgId));
        var result = await _webView?.CoreWebView2.ExecuteScriptAsync(script)!;
        _logger.Verbose($"Dispatched {eventType} at ({x},{y}) to {result}", "DropZone");
    }

    private int GetMouseButton(uint msg)
    {
        return msg switch
        {
            WM_LBUTTONDOWN or WM_LBUTTONUP or WM_LBUTTONDBLCLK => 0, // Left button
            WM_MBUTTONDOWN or WM_MBUTTONUP => 1, // Middle button
            WM_RBUTTONDOWN or WM_RBUTTONUP => 2, // Right button
            _ => 0
        };
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
        _logger.Verbose($"Mouse enter zone: {ZoneId}", "DropZone");
        _onMouseEnter?.Invoke(ZoneId);

        // Apply initial hover effect when mouse enters
        var mousePos = PointToClient(Cursor.Position);
        var screenPt = PointToScreen(mousePos);
        var webViewPt = _webView?.PointToClient(screenPt);
        if (webViewPt != null)
        {
            _ = HandleMouseMove(webViewPt.Value.X, webViewPt.Value.Y);
        }
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        _logger.Verbose($"Mouse leave zone: {ZoneId}", "DropZone");
        _onMouseLeave?.Invoke(ZoneId);

        // Clean up hover styles when mouse leaves the overlay
        CleanupHoverStyles();
    }

    private async void CleanupHoverStyles()
    {
        if (_webView?.CoreWebView2 == null)
            return;

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(JS_CLEANUP_HOVER);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to cleanup hover styles: {ex.Message}", "DropZone", ex);
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
