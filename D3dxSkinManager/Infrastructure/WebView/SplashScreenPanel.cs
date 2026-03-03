using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace D3dxSkinManager.Infrastructure.WebView;

/// <summary>
/// Splash screen panel overlay shown while WebView2 compiles JavaScript
/// Theme-aware with progress indication
/// </summary>
public class SplashScreenPanel : Panel
{
    private readonly ProgressBar _progressBar;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Panel _contentPanel;
    private bool _isDarkTheme;

    public SplashScreenPanel(bool isDarkTheme = false)
    {
        _isDarkTheme = isDarkTheme;

        // Panel properties - fill entire form with exact theme colors
        Dock = DockStyle.Fill;
        BackColor = _isDarkTheme ? Color.FromArgb(31, 31, 31) : Color.FromArgb(230, 244, 255); // Dark: #1f1f1f (bg-container), Light: #e6f4ff (bg-container)
        DoubleBuffered = true;

        // Content panel (clean minimal progress bar)
        _contentPanel = new Panel
        {
            Size = new Size(400, 4),
            BackColor = Color.Transparent
        };
        Controls.Add(_contentPanel);

        // Hidden labels (keep for compatibility but hide them)
        _titleLabel = new Label { Visible = false };
        _statusLabel = new Label { Visible = false };

        // Progress bar - clean minimal indeterminate bar with primary color
        _progressBar = new ProgressBar
        {
            Location = new Point(0, 0),
            Size = new Size(400, 4),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 20,
            ForeColor = _isDarkTheme ? Color.FromArgb(23, 125, 220) : Color.FromArgb(24, 144, 255) // Dark: #177ddc, Light: #1890ff
        };
        _contentPanel.Controls.Add(_progressBar);

        // Handle resize to center the content panel
        Resize += (s, e) => CenterContentPanel();

        // Bring to front
        BringToFront();
    }

    private void CenterContentPanel()
    {
        _contentPanel.Location = new Point(
            (Width - _contentPanel.Width) / 2,
            (Height - _contentPanel.Height) / 2
        );
    }

    public void UpdateStatus(string status)
    {
        // Status text is hidden - this is a no-op for compatibility
    }

    public void UpdateProgress(int value)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => UpdateProgress(value)));
            return;
        }

        if (_progressBar.Style != ProgressBarStyle.Continuous)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
        }

        _progressBar.Value = Math.Min(value, 100);
    }

    public void SetTheme(bool isDark)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => SetTheme(isDark)));
            return;
        }

        _isDarkTheme = isDark;
        BackColor = _isDarkTheme ? Color.FromArgb(31, 31, 31) : Color.FromArgb(230, 244, 255); // Dark: #1f1f1f (bg-container), Light: #e6f4ff (bg-container)
        _progressBar.ForeColor = _isDarkTheme ? Color.FromArgb(23, 125, 220) : Color.FromArgb(24, 144, 255); // Dark: #177ddc, Light: #1890ff
        Invalidate();
    }
}
