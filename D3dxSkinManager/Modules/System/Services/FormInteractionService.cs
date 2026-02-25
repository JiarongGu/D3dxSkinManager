using System.Windows.Forms;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// Service for managing form interaction blocking
/// </summary>
public interface IFormInteractionService
{
    /// <summary>
    /// Set the main form reference
    /// </summary>
    void SetMainForm(Form form);

    /// <summary>
    /// Get the main form reference (for dialog ownership)
    /// </summary>
    Form? GetMainForm();

    /// <summary>
    /// Get the main form window handle (thread-safe)
    /// </summary>
    IntPtr GetMainFormHandle();

    /// <summary>
    /// Block user interaction with the form
    /// </summary>
    void BlockInteraction();

    /// <summary>
    /// Unblock user interaction with the form
    /// </summary>
    void UnblockInteraction();
}

/// <summary>
/// Implementation of form interaction blocking service
/// Uses WinForms native Enabled property to block/unblock the form
/// </summary>
public class FormInteractionService : IFormInteractionService
{
    private Form? _mainForm;
    private int _blockCount = 0; // Support nested blocking
    private readonly object _lock = new();

    /// <inheritdoc />
    public void SetMainForm(Form form)
    {
        _mainForm = form ?? throw new ArgumentNullException(nameof(form));
    }

    /// <inheritdoc />
    public Form? GetMainForm()
    {
        return _mainForm;
    }

    /// <inheritdoc />
    public IntPtr GetMainFormHandle()
    {
        if (_mainForm == null)
        {
            return IntPtr.Zero;
        }

        // Access Handle property on the UI thread to avoid cross-thread exceptions
        if (_mainForm.InvokeRequired)
        {
            return (IntPtr)_mainForm.Invoke(() => _mainForm.Handle);
        }
        else
        {
            return _mainForm.Handle;
        }
    }

    /// <inheritdoc />
    public void BlockInteraction()
    {
        if (_mainForm == null)
        {
            return;
        }

        lock (_lock)
        {
            _blockCount++;

            // Only disable on first block call (support nested blocking)
            if (_blockCount == 1)
            {
                if (_mainForm.InvokeRequired)
                {
                    _mainForm.Invoke(() => _mainForm.Enabled = false);
                }
                else
                {
                    _mainForm.Enabled = false;
                }
            }
        }
    }

    /// <inheritdoc />
    public void UnblockInteraction()
    {
        if (_mainForm == null)
        {
            return;
        }

        lock (_lock)
        {
            _blockCount = Math.Max(0, _blockCount - 1);

            // Only enable when all blocks are cleared
            if (_blockCount == 0)
            {
                if (_mainForm.InvokeRequired)
                {
                    _mainForm.Invoke(() => _mainForm.Enabled = true);
                }
                else
                {
                    _mainForm.Enabled = true;
                }
            }
        }
    }
}
