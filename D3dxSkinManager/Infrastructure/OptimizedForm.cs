using System.Windows.Forms;

namespace D3dxSkinManager.Infrastructure;

/// <summary>
/// Optimized WinForms form with double buffering and performance improvements
/// </summary>
public class OptimizedForm : Form
{
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