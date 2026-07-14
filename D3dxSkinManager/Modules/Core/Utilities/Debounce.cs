namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Utility for debouncing action execution.
/// Delays action execution until a specified time has passed without new calls.
/// Useful for scenarios where you want to wait for user input to settle.
/// Thread-safe implementation using async/await.
/// Supports TimeProvider for testability (use FakeTimeProvider in tests for instant time control).
/// </summary>
public class Debounce : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new object();
    private bool _isDisposed;

    /// <summary>
    /// Creates a debounce with the specified delay
    /// </summary>
    /// <param name="delay">Time to wait after last call before executing</param>
    /// <param name="timeProvider">Time provider for testing (defaults to TimeProvider.System)</param>
    public Debounce(TimeSpan delay, TimeProvider? timeProvider = null)
    {
        _delay = delay;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates a debounce with the specified delay in milliseconds
    /// </summary>
    /// <param name="delayMs">Time to wait after last call before executing in milliseconds</param>
    /// <param name="timeProvider">Time provider for testing (defaults to TimeProvider.System)</param>
    public Debounce(int delayMs, TimeProvider? timeProvider = null) : this(TimeSpan.FromMilliseconds(delayMs), timeProvider)
    {
    }

    /// <summary>
    /// Executes the action after the debounce delay, cancelling any pending execution
    /// </summary>
    /// <param name="action">Action to execute</param>
    public async Task ExecuteAsync(Action action)
    {
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (_isDisposed) return;

            _cts?.Cancel();
            _cts?.Dispose(); // dispose the superseded source — replacing it without disposing leaked one per call
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        try
        {
            await Task.Delay(_delay, _timeProvider, cts.Token);
            action();
        }
        catch (TaskCanceledException)
        {
            // Expected when debounce is called again before delay expires
        }
    }

    /// <summary>
    /// Executes the async action after the debounce delay, cancelling any pending execution
    /// </summary>
    /// <param name="action">Async action to execute</param>
    public async Task ExecuteAsync(Func<Task> action)
    {
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (_isDisposed) return;

            _cts?.Cancel();
            _cts?.Dispose(); // dispose the superseded source — replacing it without disposing leaked one per call
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        try
        {
            await Task.Delay(_delay, _timeProvider, cts.Token);
            await action();
        }
        catch (TaskCanceledException)
        {
            // Expected when debounce is called again before delay expires
        }
    }

    /// <summary>
    /// Cancels any pending execution
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
