using System.Drawing;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;

namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Forms;

/// <summary>
/// Simple, compact capture control panel
/// Shows only X, Y, Width, Height with capture and border toggle
/// </summary>
public class ScreenCaptureControlPanelForm : Form
{
    private readonly IScreenCaptureService _captureService;

    private NumericUpDown _xNumeric = null!;
    private NumericUpDown _yNumeric = null!;
    private NumericUpDown _widthNumeric = null!;
    private NumericUpDown _heightNumeric = null!;
    private Button _showBorderButton = null!;
    private Button _captureButton = null!;
    private ScreenCaptureOverlayForm? _overlayForm;

    private bool _borderShowing = false;

    public ScreenCaptureControlPanelForm(IScreenCaptureService captureService)
    {
        _captureService = captureService;

        InitializeForm();
        InitializeControls();
    }

    private void InitializeForm()
    {
        Text = "Screen Capture";
        Size = new Size(280, 200);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(45, 45, 48); // Dark theme
        ForeColor = Color.White;

        // Position in top-right corner
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - Width - 20, screen.Top + 20);
    }

    private void InitializeControls()
    {
        var font = new Font("Segoe UI", 9F);
        int labelWidth = 60;
        int controlWidth = 80;
        int margin = 15;
        int rowHeight = 35;
        int currentY = margin;

        // X Position
        AddRow("X:", ref _xNumeric, margin, currentY, labelWidth, controlWidth, font);
        currentY += rowHeight;

        // Y Position
        AddRow("Y:", ref _yNumeric, margin, currentY, labelWidth, controlWidth, font);
        currentY += rowHeight;

        // Width
        AddRow("Width:", ref _widthNumeric, margin, currentY, labelWidth, controlWidth, font);
        _widthNumeric.Value = 800;
        currentY += rowHeight;

        // Height
        AddRow("Height:", ref _heightNumeric, margin, currentY, labelWidth, controlWidth, font);
        _heightNumeric.Value = 600;
        currentY += rowHeight;

        // Buttons row
        _showBorderButton = new Button
        {
            Text = "Show Area",
            Location = new Point(margin, currentY),
            Size = new Size(120, 30),
            Font = font,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _showBorderButton.FlatAppearance.BorderSize = 0;
        _showBorderButton.Click += ShowBorderButton_Click;
        Controls.Add(_showBorderButton);

        _captureButton = new Button
        {
            Text = "Capture",
            Location = new Point(margin + 130, currentY),
            Size = new Size(110, 30),
            Font = font,
            BackColor = Color.FromArgb(16, 124, 16),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _captureButton.FlatAppearance.BorderSize = 0;
        _captureButton.Click += CaptureButton_Click;
        Controls.Add(_captureButton);
    }

    private void AddRow(string labelText, ref NumericUpDown numeric, int x, int y, int labelWidth, int controlWidth, Font font)
    {
        var label = new Label
        {
            Text = labelText,
            Location = new Point(x, y + 5),
            Size = new Size(labelWidth, 20),
            Font = font,
            ForeColor = Color.White
        };
        Controls.Add(label);

        numeric = new NumericUpDown
        {
            Location = new Point(x + labelWidth, y),
            Size = new Size(controlWidth, 25),
            Font = font,
            Maximum = 10000,
            Minimum = -10000,
            BackColor = Color.FromArgb(60, 60, 65),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        numeric.ValueChanged += NumericValueChanged;
        Controls.Add(numeric);
    }

    private void NumericValueChanged(object? sender, EventArgs e)
    {
        // Update overlay if showing
        if (_borderShowing && _overlayForm != null && !_overlayForm.IsDisposed)
        {
            UpdateOverlayPosition();
        }
    }

    private void UpdateOverlayPosition()
    {
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.UpdateBounds(
                (int)_xNumeric.Value,
                (int)_yNumeric.Value,
                (int)_widthNumeric.Value,
                (int)_heightNumeric.Value
            );
        }
    }

    private void ShowBorderButton_Click(object? sender, EventArgs e)
    {
        if (_borderShowing)
        {
            HideBorder();
        }
        else
        {
            ShowBorder();
        }
    }

    private void ShowBorder()
    {
        if (_overlayForm == null || _overlayForm.IsDisposed)
        {
            _overlayForm = new ScreenCaptureOverlayForm((int)_xNumeric.Value, (int)_yNumeric.Value, (int)_widthNumeric.Value, (int)_heightNumeric.Value);
            _overlayForm.BoundsChanged += Overlay_BoundsChanged;
        }

        _overlayForm.Show();
        _borderShowing = true;
        _showBorderButton.Text = "Hide Area";
        _showBorderButton.BackColor = Color.FromArgb(180, 60, 0);
    }

    private void HideBorder()
    {
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.Hide();
        }
        _borderShowing = false;
        _showBorderButton.Text = "Show Area";
        _showBorderButton.BackColor = Color.FromArgb(0, 120, 212);
    }

    private void Overlay_BoundsChanged(int x, int y, int width, int height)
    {
        // Update numeric controls when overlay is moved/resized
        _xNumeric.Value = x;
        _yNumeric.Value = y;
        _widthNumeric.Value = width;
        _heightNumeric.Value = height;
    }

    private async void CaptureButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var config = new Models.ScreenCaptureConfig
            {
                X = (int)_xNumeric.Value,
                Y = (int)_yNumeric.Value,
                Width = (int)_widthNumeric.Value,
                Height = (int)_heightNumeric.Value,
                CopyToClipboard = true
            };

            await _captureService.CaptureAsync(config);
            FlashSuccess();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capture failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void FlashSuccess()
    {
        var originalColor = BackColor;
        BackColor = Color.FromArgb(16, 124, 16);
        await Task.Delay(150);
        BackColor = originalColor;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_overlayForm != null && !_overlayForm.IsDisposed)
        {
            _overlayForm.Close();
        }
        base.OnFormClosing(e);
    }
}
