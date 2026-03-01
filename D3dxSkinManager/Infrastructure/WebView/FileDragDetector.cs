using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Detects file drag operations from external sources using Windows hooks and OLE clipboard.
/// Provides events for drag start, drag move, and drag end.
/// </summary>
public class FileDragDetector : IDisposable
{
    #region P/Invoke Declarations

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(out CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("ole32.dll")]
    private static extern int OleGetClipboard(out System.Runtime.InteropServices.ComTypes.IDataObject ppDataObj);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private const uint GA_ROOT = 2;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int VK_LBUTTON = 0x01;
    private const int IDC_ARROW = 32512;
    private const int IDC_NO = 32648;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when a file drag operation starts (enters the monitored window)
    /// </summary>
    public event EventHandler<DragEventArgs>? DragStarted;

    /// <summary>
    /// Fired when the mouse moves during a file drag operation
    /// </summary>
    public event EventHandler<DragEventArgs>? DragMoved;

    /// <summary>
    /// Fired when a file drag operation ends (mouse button released)
    /// </summary>
    public event EventHandler<EventArgs>? DragEnded;

    #endregion

    #region Fields

    private readonly Form _targetForm;
    private readonly ILogHelper _logger;
    private readonly bool _bringToFrontOnDrag;

    // Hook state
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private LowLevelMouseProc? _mouseHookCallback;

    // Drag state
    private bool _isDragging = false;
    private bool _leftButtonDown = false;
    private Point _dragStartPosition;
    private Point _lastCheckedPosition;

    // Configuration
    private const int DRAG_THRESHOLD = 5;
    private readonly int _movementThreshold = 10; // Minimum pixels to trigger move event

    // Performance optimization
    private Rectangle _cachedFormBounds;
    private DateTime _lastBoundsUpdate = DateTime.MinValue;
    private readonly TimeSpan _boundsUpdateInterval = TimeSpan.FromMilliseconds(100);

    // Cursor handles for detection
    private IntPtr _arrowCursor;
    private IntPtr _noCursor;

    #endregion

    #region Initialization & Cleanup

    public FileDragDetector(Form targetForm, ILogHelper logger, bool bringToFrontOnDrag = false)
    {
        _targetForm = targetForm ?? throw new ArgumentNullException(nameof(targetForm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bringToFrontOnDrag = bringToFrontOnDrag;

        // Load standard cursor handles
        _arrowCursor = LoadCursor(IntPtr.Zero, IDC_ARROW);
        _noCursor = LoadCursor(IntPtr.Zero, IDC_NO);

        InstallMouseHook();

        _logger.Info($"FileDragDetector initialized (bringToFrontOnDrag={bringToFrontOnDrag})", "DragDetector");
    }

    public void Dispose()
    {
        UninstallMouseHook();
        _logger.Info("FileDragDetector disposed", "DragDetector");
    }

    #endregion

    #region Mouse Hook

    private void InstallMouseHook()
    {
        _mouseHookCallback = MouseHookProc;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        if (curModule != null)
        {
            _mouseHookHandle = SetWindowsHookEx(
                WH_MOUSE_LL,
                _mouseHookCallback,
                GetModuleHandle(curModule.ModuleName),
                0);

            if (_mouseHookHandle == IntPtr.Zero)
            {
                _logger.Error("Failed to install mouse hook", "DragDetector");
            }
            else
            {
                _logger.Debug("Mouse hook installed", "DragDetector");
            }
        }
    }

    private void UninstallMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
            _logger.Debug("Mouse hook uninstalled", "DragDetector");
        }
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var message = wParam.ToInt32();

                switch (message)
                {
                    case WM_LBUTTONDOWN:
                        _leftButtonDown = true;
                        _dragStartPosition = new Point(hookStruct.pt.x, hookStruct.pt.y);
                        break;

                    case WM_MOUSEMOVE when _leftButtonDown:
                        var currentPos = new Point(hookStruct.pt.x, hookStruct.pt.y);

                        if (!_isDragging)
                        {
                            CheckForDragStart(currentPos);
                        }
                        else
                        {
                            NotifyDragMoved(currentPos);
                        }
                        break;

                    case WM_LBUTTONUP:
                        _leftButtonDown = false;
                        if (_isDragging)
                        {
                            EndDrag();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse hook error: {ex.Message}", "DragDetector", ex);
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    #endregion

    #region Drag Detection

    private void CheckForDragStart(Point currentPosition)
    {
        // Check if movement exceeds threshold
        int dx = Math.Abs(currentPosition.X - _dragStartPosition.X);
        int dy = Math.Abs(currentPosition.Y - _dragStartPosition.Y);

        if (dx <= DRAG_THRESHOLD && dy <= DRAG_THRESHOLD)
            return;

        // Check if cursor is over our window bounds
        var formBounds = GetFormBounds();
        if (!formBounds.Contains(currentPosition))
            return;

        // Detect file drag using multiple methods (in order of reliability):
        // 1. OLE clipboard check - most reliable for file drops
        // 2. Drag started OUTSIDE window and entered = likely file drag from external source
        // 3. Cursor changed (not arrow) = drag operation indicator
        bool isFileDrag = IsFileDragOperation();
        bool dragStartedOutside = !formBounds.Contains(_dragStartPosition);
        bool isDragCursor = IsLikelyDragCursor();

        // Activate drag mode if any condition is true
        if (isFileDrag || dragStartedOutside || isDragCursor)
        {
            _isDragging = true;
            _lastCheckedPosition = currentPosition;

            string reason = isFileDrag
                ? "OLE clipboard contains files"
                : dragStartedOutside
                    ? "drag started outside window"
                    : "cursor indicates drag operation";

            _logger.Info($"File drag detected at {currentPosition} ({reason})", "DragDetector");

            // Optionally bring window to front to ensure it receives drop events
            if (_bringToFrontOnDrag && !IsMouseOverOurWindow(currentPosition))
            {
                try
                {
                    _targetForm.Invoke(() =>
                    {
                        _targetForm.BringToFront();
                        _targetForm.Activate();
                        _logger.Info("Brought window to front for file drag", "DragDetector");
                    });
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Failed to bring window to front: {ex.Message}", "DragDetector");
                }
            }

            // Notify subscribers
            DragStarted?.Invoke(this, new DragEventArgs(currentPosition, reason));
        }
    }

    private void NotifyDragMoved(Point currentPosition)
    {
        // Performance optimization: skip check if mouse hasn't moved much
        int dx = Math.Abs(currentPosition.X - _lastCheckedPosition.X);
        int dy = Math.Abs(currentPosition.Y - _lastCheckedPosition.Y);

        if (dx < _movementThreshold && dy < _movementThreshold)
            return;

        _lastCheckedPosition = currentPosition;

        // Notify subscribers
        DragMoved?.Invoke(this, new DragEventArgs(currentPosition, "drag moved"));
    }

    private void EndDrag()
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _logger.Info("Drag ended", "DragDetector");

        // Notify subscribers
        DragEnded?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Detection Methods

    /// <summary>
    /// Check if files are being dragged using OLE clipboard
    /// </summary>
    private bool IsFileDragOperation()
    {
        try
        {
            // Check if left mouse button is down
            short lButtonState = GetAsyncKeyState(VK_LBUTTON);
            bool isPressed = (lButtonState & 0x8000) != 0;

            if (!isPressed)
                return false;

            // Try to get OLE clipboard data
            if (OleGetClipboard(out System.Runtime.InteropServices.ComTypes.IDataObject dataObj) == 0 && dataObj != null)
            {
                try
                {
                    // Check for CF_HDROP format (file drop format)
                    var formatEtc = new FORMATETC
                    {
                        cfFormat = (short)DataFormats.GetFormat(DataFormats.FileDrop).Id,
                        ptd = IntPtr.Zero,
                        dwAspect = DVASPECT.DVASPECT_CONTENT,
                        lindex = -1,
                        tymed = TYMED.TYMED_HGLOBAL
                    };

                    int result = dataObj.QueryGetData(ref formatEtc);
                    return result == 0;
                }
                finally
                {
                    Marshal.ReleaseComObject(dataObj);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"OLE clipboard check failed: {ex.Message}", "DragDetector");
        }
        return false;
    }

    /// <summary>
    /// Check if the current cursor indicates a drag operation
    /// </summary>
    private bool IsLikelyDragCursor()
    {
        try
        {
            var cursorInfo = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (GetCursorInfo(out cursorInfo))
            {
                return cursorInfo.hCursor != _arrowCursor && cursorInfo.hCursor != IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Cursor check failed: {ex.Message}", "DragDetector");
        }
        return false;
    }

    /// <summary>
    /// Get cached form bounds, updating if needed
    /// </summary>
    private Rectangle GetFormBounds()
    {
        var now = DateTime.UtcNow;
        if (now - _lastBoundsUpdate > _boundsUpdateInterval)
        {
            _cachedFormBounds = _targetForm.RectangleToScreen(_targetForm.ClientRectangle);
            _lastBoundsUpdate = now;
        }
        return _cachedFormBounds;
    }

    /// <summary>
    /// Check if the mouse position is actually over our window (topmost at that position)
    /// </summary>
    private bool IsMouseOverOurWindow(Point screenPosition)
    {
        try
        {
            // Get the window at this screen position
            var point = new POINT { x = screenPosition.X, y = screenPosition.Y };
            IntPtr hwndAtPoint = WindowFromPoint(point);

            if (hwndAtPoint == IntPtr.Zero)
                return false;

            // Get the root window (top-level parent) of the window at this point
            IntPtr rootWindow = GetAncestor(hwndAtPoint, GA_ROOT);

            // Check if it's our form or any of our form's child windows
            return rootWindow == _targetForm.Handle || hwndAtPoint == _targetForm.Handle;
        }
        catch (Exception ex)
        {
            _logger.Debug($"Failed to check window at point: {ex.Message}", "DragDetector");
            return false;
        }
    }

    #endregion

    #region Event Args

    /// <summary>
    /// Event arguments for drag events
    /// </summary>
    public class DragEventArgs : EventArgs
    {
        public Point ScreenPosition { get; }
        public string DetectionMethod { get; }

        public DragEventArgs(Point screenPosition, string detectionMethod)
        {
            ScreenPosition = screenPosition;
            DetectionMethod = detectionMethod;
        }
    }

    #endregion
}
