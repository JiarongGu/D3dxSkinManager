using System.Windows.Forms;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Optimized WinForms form with double buffering and performance improvements
/// </summary>
public class OptimizedForm : Form
{
    /// <summary>
    /// Optional raw WndProc hook. Return true to mark the message handled (swallow it). Used by
    /// <see cref="ApplicationHost"/> to catch the <see cref="SingleInstanceGuard"/> activation broadcast
    /// without coupling this form to single-instance logic.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, bool>? WndProcHook { get; set; }

    protected override void WndProc(ref Message m)
    {
        if (WndProcHook != null && WndProcHook(m.Msg))
        {
            return;
        }
        base.WndProc(ref m);
    }

    public OptimizedForm()
    {
        // Enable double buffering and optimized rendering
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.DoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        // Additional performance optimizations
        UpdateStyles();

        // Enable drag-and-drop support for DropZoneManager to detect system drag events
        AllowDrop = true;

        // Add DragOver handler to allow dragging over the form
        DragOver += (sender, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
        };
    }
}