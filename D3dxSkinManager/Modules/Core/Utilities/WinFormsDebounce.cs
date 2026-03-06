namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// WinForms-compatible debounce utility using System.Windows.Forms.Timer.
/// Delays action execution until a specified time has passed without new calls.
/// Safe for UI thread operations.
/// </summary>
public class WinFormsDebounce : IDisposable
{
    private readonly global::System.Windows.Forms.Timer _timer;
    private Action? _pendingAction;
    private readonly object _lock = new object();
    private bool _isDisposed;

    /// <summary>
    /// Creates a WinForms debounce with the specified delay
    /// </summary>
    /// <param name="delayMs">Time to wait after last call before executing in milliseconds</param>
    public WinFormsDebounce(int delayMs)
    {
        _timer = new global::System.Windows.Forms.Timer();
        _timer.Interval = delayMs;
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Executes the action after the debounce delay, cancelling any pending execution
    /// </summary>
    /// <param name="action">Action to execute on UI thread</param>
    public void Execute(Action action)
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _pendingAction = action;
            _timer.Stop();
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Action? actionToExecute = null;

        lock (_lock)
        {
            _timer.Stop();
            actionToExecute = _pendingAction;
            _pendingAction = null;
        }

        // Execute outside lock to avoid deadlocks
        actionToExecute?.Invoke();
    }

    /// <summary>
    /// Cancels any pending execution
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _timer.Stop();
            _pendingAction = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _timer.Stop();
            _timer.Dispose();
            _pendingAction = null;
        }
    }
}
