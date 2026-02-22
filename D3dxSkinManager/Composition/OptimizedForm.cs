using System.Windows.Forms;

namespace D3dxSkinManager.Composition;

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
    }
}