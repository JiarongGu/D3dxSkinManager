namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Utility for throttling action execution to a specified interval.
/// Ensures an action is not executed more frequently than the specified interval.
/// Thread-safe implementation.
/// </summary>
public class Throttle
{
    private readonly TimeSpan _interval;
    private DateTime _lastExecutionTime = DateTime.MinValue;
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a throttle with the specified interval
    /// </summary>
    /// <param name="interval">Minimum time between action executions</param>
    public Throttle(TimeSpan interval)
    {
        _interval = interval;
    }

    /// <summary>
    /// Creates a throttle with the specified interval in milliseconds
    /// </summary>
    /// <param name="intervalMs">Minimum time between action executions in milliseconds</param>
    public Throttle(int intervalMs) : this(TimeSpan.FromMilliseconds(intervalMs))
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
            var now = DateTime.UtcNow;
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
            var now = DateTime.UtcNow;
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
            _lastExecutionTime = DateTime.MinValue;
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
            var now = DateTime.UtcNow;
            return now - _lastExecutionTime >= _interval;
        }
    }
}

/// <summary>
/// Utility for debouncing action execution.
/// Delays action execution until a specified time has passed without new calls.
/// Useful for scenarios where you want to wait for user input to settle.
/// Thread-safe implementation.
/// </summary>
public class Debounce
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new object();

    /// <summary>
    /// Creates a debounce with the specified delay
    /// </summary>
    /// <param name="delay">Time to wait after last call before executing</param>
    public Debounce(TimeSpan delay)
    {
        _delay = delay;
    }

    /// <summary>
    /// Creates a debounce with the specified delay in milliseconds
    /// </summary>
    /// <param name="delayMs">Time to wait after last call before executing in milliseconds</param>
    public Debounce(int delayMs) : this(TimeSpan.FromMilliseconds(delayMs))
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
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        try
        {
            await Task.Delay(_delay, cts.Token);
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
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            cts = _cts;
        }

        try
        {
            await Task.Delay(_delay, cts.Token);
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
            _cts = null;
        }
    }
}
