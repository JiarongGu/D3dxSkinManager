using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Helpers;
using Timer = System.Threading.Timer;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Monitors application performance metrics
/// </summary>
public interface IPerformanceMonitor
{
    void StartOperation(string operationName);
    TimeSpan StopOperation(string operationName);
    void LogPerformanceMetrics();
    PerformanceMetrics GetCurrentMetrics();
}

/// <summary>
/// Performance metrics data
/// </summary>
public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public long WorkingSetMemoryMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UpTime { get; set; }
}

/// <summary>
/// Implementation of performance monitoring
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogHelper _logger;
    private readonly Dictionary<string, Stopwatch> _operations;
    private readonly Process _currentProcess;
    private DateTime _lastCpuCheck;
    private TimeSpan _lastTotalProcessorTime;
    private readonly Timer _metricsTimer;
    private readonly object _lock = new object();

    public PerformanceMonitor(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operations = new Dictionary<string, Stopwatch>();
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuCheck = DateTime.UtcNow;
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;

        // Start periodic metrics logging (every 30 seconds)
        _metricsTimer = new Timer(
            _ => LogPerformanceMetrics(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    public void StartOperation(string operationName)
    {
        lock (_lock)
        {
            if (_operations.ContainsKey(operationName))
            {
                _operations[operationName].Restart();
            }
            else
            {
                _operations[operationName] = Stopwatch.StartNew();
            }

            _logger.Debug($"Performance tracking started: {operationName}", "Performance");
        }
    }

    public TimeSpan StopOperation(string operationName)
    {
        lock (_lock)
        {
            if (_operations.TryGetValue(operationName, out var stopwatch))
            {
                stopwatch.Stop();
                var elapsed = stopwatch.Elapsed;
                _operations.Remove(operationName);

                // Log if operation took longer than 100ms
                if (elapsed.TotalMilliseconds > 100)
                {
                    _logger.Warn($"Slow operation detected: {operationName} took {elapsed.TotalMilliseconds:F0}ms", "Performance");
                }
                else
                {
                    _logger.Debug($"Operation completed: {operationName} in {elapsed.TotalMilliseconds:F0}ms", "Performance");
                }

                return elapsed;
            }

            _logger.Warn($"No operation found to stop: {operationName}", "Performance");
            return TimeSpan.Zero;
        }
    }

    public PerformanceMetrics GetCurrentMetrics()
    {
        _currentProcess.Refresh();

        // Calculate CPU usage
        var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
        var currentTime = DateTime.UtcNow;

        var timeDiff = currentTime.Subtract(_lastCpuCheck).TotalMilliseconds;
        var cpuTimeDiff = currentTotalProcessorTime.Subtract(_lastTotalProcessorTime).TotalMilliseconds;

        var cpuUsage = (cpuTimeDiff / timeDiff) * 100.0 / Environment.ProcessorCount;

        _lastCpuCheck = currentTime;
        _lastTotalProcessorTime = currentTotalProcessorTime;

        return new PerformanceMetrics
        {
            CpuUsage = Math.Min(100, Math.Max(0, cpuUsage)),
            WorkingSetMemoryMB = _currentProcess.WorkingSet64 / (1024 * 1024),
            PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
            ThreadCount = _currentProcess.Threads.Count,
            HandleCount = _currentProcess.HandleCount,
            TotalProcessorTime = _currentProcess.TotalProcessorTime,
            UpTime = DateTime.UtcNow - _currentProcess.StartTime.ToUniversalTime()
        };
    }

    public void LogPerformanceMetrics()
    {
        try
        {
            var metrics = GetCurrentMetrics();

            _logger.Info($"Performance Metrics - " +
                $"CPU: {metrics.CpuUsage:F1}%, " +
                $"Memory: {metrics.WorkingSetMemoryMB}MB, " +
                $"Threads: {metrics.ThreadCount}, " +
                $"Handles: {metrics.HandleCount}, " +
                $"Uptime: {metrics.UpTime:hh\\:mm\\:ss}",
                "Performance");

            // Warn if memory usage is high
            if (metrics.WorkingSetMemoryMB > 500)
            {
                _logger.Warn($"High memory usage detected: {metrics.WorkingSetMemoryMB}MB", "Performance");
            }

            // Warn if handle count is high
            if (metrics.HandleCount > 1000)
            {
                _logger.Warn($"High handle count detected: {metrics.HandleCount}", "Performance");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to log performance metrics: {ex.Message}", "Performance", ex);
        }
    }

    ~PerformanceMonitor()
    {
        _metricsTimer?.Dispose();
    }
}
