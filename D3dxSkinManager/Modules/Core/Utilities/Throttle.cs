namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Utility for throttling action execution to a specified interval.
/// Ensures an action is not executed more frequently than the specified interval.
/// Thread-safe implementation.
/// Supports TimeProvider for testability (use FakeTimeProvider in tests for instant time control).
/// </summary>
public class Throttle : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastExecutionTime = DateTimeOffset.MinValue;
    private readonly object _lock = new object();
    private bool _isDisposed;

    /// <summary>
    /// Creates a throttle with the specified interval
    /// </summary>
    /// <param name="interval">Minimum time between action executions</param>
    /// <param name="timeProvider">Time provider for testing (defaults to TimeProvider.System)</param>
    public Throttle(TimeSpan interval, TimeProvider? timeProvider = null)
    {
        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates a throttle with the specified interval in milliseconds
    /// </summary>
    /// <param name="intervalMs">Minimum time between action executions in milliseconds</param>
    /// <param name="timeProvider">Time provider for testing (defaults to TimeProvider.System)</param>
    public Throttle(int intervalMs, TimeProvider? timeProvider = null) : this(TimeSpan.FromMilliseconds(intervalMs), timeProvider)
    {
    }

    /// <summary>
    /// Executes the action if the throttle interval has elapsed, otherwise skips it
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <returns>True if action was executed, false if throttled</returns>
    public bool Execute(Action action)
    {
        lock (_lock)
        {
            if (_isDisposed) return false;

            var now = _timeProvider.GetUtcNow();
            if (now - _lastExecutionTime >= _interval)
            {
                _lastExecutionTime = now;
                action();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Executes the async action if the throttle interval has elapsed, otherwise skips it
    /// </summary>
    /// <param name="action">Async action to execute</param>
    /// <returns>True if action was executed, false if throttled</returns>
    public async Task<bool> ExecuteAsync(Func<Task> action)
    {
        bool shouldExecute = false;

        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            if (now - _lastExecutionTime >= _interval)
            {
                _lastExecutionTime = now;
                shouldExecute = true;
            }
        }

        if (shouldExecute)
        {
            await action();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resets the throttle, allowing immediate execution on next call
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _lastExecutionTime = DateTimeOffset.MinValue;
        }
    }

    /// <summary>
    /// Checks if enough time has elapsed to allow execution without actually executing
    /// </summary>
    /// <returns>True if execution would be allowed</returns>
    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_isDisposed) return false;

            var now = _timeProvider.GetUtcNow();
            return now - _lastExecutionTime >= _interval;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }
    }
}
