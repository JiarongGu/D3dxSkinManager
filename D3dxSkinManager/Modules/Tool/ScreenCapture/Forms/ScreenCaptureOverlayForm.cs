using System.Drawing;
using System.Runtime.InteropServices;

namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Forms;

/// <summary>
/// Draggable and resizable capture area overlay
/// Shows a border with resize handles that can be moved and resized
/// </summary>
public class ScreenCaptureOverlayForm : Form
{
    // Event fired when bounds change (move or resize)
    public event Action<int, int, int, int>? BoundsChanged;

    private const int BORDER_WIDTH = 3;
    private const int RESIZE_EDGE_SIZE = 10; // Hit area for edge detection

    private bool _isDragging = false;
    private bool _isResizing = false;
    private Point _dragStart;
    private Rectangle _startBounds; // Store initial bounds when resize starts
    private Size _lastSize; // Track size to minimize redraws
    private ResizeHandle _activeHandle = ResizeHandle.None;

    private enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Left,
        Right,
        Top,
        Bottom
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const uint LWA_ALPHA = 0x2;
    private const uint LWA_COLORKEY = 0x1;

    public ScreenCaptureOverlayForm(int x, int y, int width, int height)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // Make magenta transparent
        TopMost = true;
        ShowInTaskbar = false;

        Bounds = new Rectangle(x, y, width, height);
        _lastSize = new Size(width, height);

        // Make it a tool window
        int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
        SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    public void UpdateBounds(int x, int y, int width, int height)
    {
        Bounds = new Rectangle(x, y, width, height);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.Clear(TransparencyKey); // Clear with transparency key (magenta)

        // Draw border only - no handles
        using (var pen = new Pen(Color.FromArgb(255, 0, 150, 255), BORDER_WIDTH))
        {
            g.DrawRectangle(pen, BORDER_WIDTH / 2, BORDER_WIDTH / 2, Width - BORDER_WIDTH, Height - BORDER_WIDTH);
        }
    }

    private ResizeHandle GetHandleAtPoint(Point pt)
    {
        // Check corners first (priority over edges)
        if (pt.X < RESIZE_EDGE_SIZE && pt.Y < RESIZE_EDGE_SIZE) return ResizeHandle.TopLeft;
        if (pt.X >= Width - RESIZE_EDGE_SIZE && pt.Y < RESIZE_EDGE_SIZE) return ResizeHandle.TopRight;
        if (pt.X < RESIZE_EDGE_SIZE && pt.Y >= Height - RESIZE_EDGE_SIZE) return ResizeHandle.BottomLeft;
        if (pt.X >= Width - RESIZE_EDGE_SIZE && pt.Y >= Height - RESIZE_EDGE_SIZE) return ResizeHandle.BottomRight;

        // Check edges
        if (pt.X < RESIZE_EDGE_SIZE) return ResizeHandle.Left;
        if (pt.X >= Width - RESIZE_EDGE_SIZE) return ResizeHandle.Right;
        if (pt.Y < RESIZE_EDGE_SIZE) return ResizeHandle.Top;
        if (pt.Y >= Height - RESIZE_EDGE_SIZE) return ResizeHandle.Bottom;

        return ResizeHandle.None;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        _activeHandle = GetHandleAtPoint(e.Location);

        if (_activeHandle != ResizeHandle.None)
        {
            _isResizing = true;
            _dragStart = Cursor.Position;
            _startBounds = Bounds; // Store initial bounds
        }
        else if (e.X > RESIZE_EDGE_SIZE && e.X < Width - RESIZE_EDGE_SIZE && e.Y > RESIZE_EDGE_SIZE && e.Y < Height - RESIZE_EDGE_SIZE)
        {
            // Click in center - drag
            _isDragging = true;
            _dragStart = Cursor.Position;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging)
        {
            var delta = new Point(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
            Location = new Point(Location.X + delta.X, Location.Y + delta.Y);
            _dragStart = Cursor.Position;

            // Fire event asynchronously to avoid blocking UI thread
            FireBoundsChangedAsync(Left, Top, Width, Height);
        }
        else if (_isResizing)
        {
            // Use absolute cursor position relative to initial bounds (not delta accumulation)
            var currentCursor = Cursor.Position;
            var deltaX = currentCursor.X - _dragStart.X;
            var deltaY = currentCursor.Y - _dragStart.Y;
            var newBounds = _startBounds;

            switch (_activeHandle)
            {
                case ResizeHandle.TopLeft:
                    newBounds = new Rectangle(_startBounds.Left + deltaX, _startBounds.Top + deltaY,
                                             _startBounds.Width - deltaX, _startBounds.Height - deltaY);
                    break;
                case ResizeHandle.TopRight:
                    newBounds = new Rectangle(_startBounds.Left, _startBounds.Top + deltaY,
                                             _startBounds.Width + deltaX, _startBounds.Height - deltaY);
                    break;
                case ResizeHandle.BottomLeft:
                    newBounds = new Rectangle(_startBounds.Left + deltaX, _startBounds.Top,
                                             _startBounds.Width - deltaX, _startBounds.Height + deltaY);
                    break;
                case ResizeHandle.BottomRight:
                    newBounds = new Rectangle(_startBounds.Left, _startBounds.Top,
                                             _startBounds.Width + deltaX, _startBounds.Height + deltaY);
                    break;
                case ResizeHandle.Left:
                    newBounds = new Rectangle(_startBounds.Left + deltaX, _startBounds.Top,
                                             _startBounds.Width - deltaX, _startBounds.Height);
                    break;
                case ResizeHandle.Right:
                    newBounds = new Rectangle(_startBounds.Left, _startBounds.Top,
                                             _startBounds.Width + deltaX, _startBounds.Height);
                    break;
                case ResizeHandle.Top:
                    newBounds = new Rectangle(_startBounds.Left, _startBounds.Top + deltaY,
                                             _startBounds.Width, _startBounds.Height - deltaY);
                    break;
                case ResizeHandle.Bottom:
                    newBounds = new Rectangle(_startBounds.Left, _startBounds.Top,
                                             _startBounds.Width, _startBounds.Height + deltaY);
                    break;
            }

            if (newBounds.Width > 50 && newBounds.Height > 50)
            {
                var sizeChanged = newBounds.Width != Width || newBounds.Height != Height;
                Bounds = newBounds;

                // Only invalidate (redraw) if size changed, not just position
                if (sizeChanged)
                {
                    _lastSize = new Size(Width, Height);
                    Invalidate();
                }

                // Fire event asynchronously to avoid blocking UI thread
                FireBoundsChangedAsync(Left, Top, Width, Height);
            }
        }
        else
        {
            // Update cursor based on edge/corner
            var handle = GetHandleAtPoint(e.Location);
            Cursor = handle switch
            {
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                _ => Cursors.SizeAll
            };
        }
    }

    private void FireBoundsChangedAsync(int x, int y, int width, int height)
    {
        // Fire event on a background thread to avoid blocking UI
        if (BoundsChanged != null)
        {
            Task.Run(() =>
            {
                try
                {
                    BoundsChanged?.Invoke(x, y, width, height);
                }
                catch
                {
                    // Ignore errors from event handlers
                }
            });
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isDragging = false;
        _isResizing = false;
        _activeHandle = ResizeHandle.None;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
