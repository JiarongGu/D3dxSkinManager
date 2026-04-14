using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Log levels for categorizing log messages
/// Matches frontend LogLevel enum exactly
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose - Extremely detailed diagnostic information (high-frequency events like mouse movements, IPC messages)</summary>
    Verbose = 0,

    /// <summary>Debug - Verbose diagnostic information</summary>
    Debug = 1,

    /// <summary>Info - General informational messages</summary>
    Info = 2,

    /// <summary>Warn - Warning messages</summary>
    Warn = 3,

    /// <summary>Error - Error messages</summary>
    Error = 4,

    /// <summary>All - Show everything (special value for filtering)</summary>
    All = -1,

    /// <summary>Off - Disable all logging (special value for filtering)</summary>
    Off = -2
}

/// <summary>
/// Interface for centralized logging service
/// </summary>
public interface ILogHelper
{
    /// <summary>
    /// Gets or sets the minimum log level that will be output
    /// </summary>
    LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Log a verbose message (high-frequency events like mouse movements, IPC messages)
    /// </summary>
    void Verbose(string message, string? source = null);

    /// <summary>
    /// Log a debug message (verbose diagnostic information)
    /// </summary>
    void Debug(string message, string? source = null);

    /// <summary>
    /// Log an informational message
    /// </summary>
    void Info(string message, string? source = null);

    /// <summary>
    /// Log a warning message
    /// </summary>
    void Warn(string message, string? source = null);

    /// <summary>
    /// Log an error message
    /// </summary>
    void Error(string message, string? source = null, Exception? exception = null);

    /// <summary>
    /// Log a message with specific level
    /// </summary>
    void Log(LogLevel level, string message, string? source = null, Exception? exception = null);

    /// <summary>
    /// Flush any buffered log entries to disk
    /// </summary>
    Task FlushAsync();
}

/// <summary>
/// Centralized logging service
/// Writes logs to centralized log directory under data\logs and console
/// Thread-safe with async file writing
/// </summary>
public class LogHelper : ILogHelper, IDisposable
{
    private readonly IGlobalPathService _globalPaths;
    private readonly IAppEnvironment _appEnvironment;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _logsBaseDirectory;
    private bool _disposed;

    // Batching infrastructure
    private readonly ConcurrentQueue<(string logFile, string logEntry)> _logQueue = new();
    private readonly global::System.Timers.Timer _batchTimer;
    private readonly object _batchLock = new();
    private const int BatchIntervalMs = 100; // Flush logs every 100ms

    public LogLevel MinimumLevel
    {
        // Always use AppEnvironment's MinimumLogLevel as the single source of truth
        get => _appEnvironment.MinimumLogLevel;
        set => _appEnvironment.MinimumLogLevel = value;
    }

    // Constructor for DI (preferred)
    public LogHelper(IGlobalPathService globalPaths, IAppEnvironment appEnvironment)
    {
        _globalPaths = globalPaths;
        _appEnvironment = appEnvironment;

        _logsBaseDirectory = _globalPaths.LogsDirectory;

        // AppEnvironment already has the log level configured (OFF by default or from env var)
        // No need to set it here - just use AppEnvironment.MinimumLogLevel directly

        // Initialize batching timer
        _batchTimer = new global::System.Timers.Timer(BatchIntervalMs);
        _batchTimer.Elapsed += (sender, e) => FlushLogBatch();
        _batchTimer.AutoReset = true;
        _batchTimer.Start();
    }

    public static LogHelper Create(AppEnvironment environment) 
    {
        var globalPaths = new GlobalPathService(environment);
        return new LogHelper(globalPaths, environment);
    }

    public void Verbose(string message, string? source = null)
    {
        Log(LogLevel.Verbose, message, source);
    }

    public void Debug(string message, string? source = null)
    {
        Log(LogLevel.Debug, message, source);
    }

    public void Info(string message, string? source = null)
    {
        Log(LogLevel.Info, message, source);
    }

    public void Warn(string message, string? source = null)
    {
        Log(LogLevel.Warn, message, source);
    }

    public void Error(string message, string? source = null, Exception? exception = null)
    {
        Log(LogLevel.Error, message, source, exception);
    }

    public void Log(LogLevel level, string message, string? source = null, Exception? exception = null)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logSource = source ?? "App";
        var levelStr = level.ToString().ToUpper().PadRight(7);

        // Format log entry
        var logEntry = $"[{timestamp}] [{levelStr}] [{logSource}] {message}";

        // Add exception details if present
        if (exception != null)
        {
            logEntry += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                logEntry += $"\n  StackTrace: {exception.StackTrace}";
            }
        }

        // LOGGING BEHAVIOR:
        // - Console Output: Development mode ONLY (respects log level settings, defaults to INFO)
        // - File Output: Respects log level settings in both dev and prod modes
        // - Production: No console output at all, only file output based on settings
        // - Log Level Settings: Apply to both console and file output

        // Console: Development only, respects log level settings
        if (_appEnvironment.IsDevelopment && ShouldLog(level))
        {
            WriteToConsole(level, logEntry);
        }

        // File: Respects log level settings (OFF, VERBOSE, DEBUG, INFO, WARNING, ERROR, ALL)
        if (ShouldLog(level))
        {
            QueueLogEntry(level, logEntry);
        }
    }

    private bool ShouldLog(LogLevel level)
    {
        // Skip logging if disabled (Off=-2)
        if (_appEnvironment.MinimumLogLevel == LogLevel.Off)
        {
            return false;
        }

        // Log everything if minimum is All=-1
        if (_appEnvironment.MinimumLogLevel == LogLevel.All)
        {
            return true;
        }

        // Otherwise, check if level meets minimum threshold
        return level >= _appEnvironment.MinimumLogLevel;
    }

    public async Task FlushAsync()
    {
        // Flush any queued logs immediately
        FlushLogBatch();

        // Wait for any pending writes to complete
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Just release - actual flushing happens in FlushLogBatch
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void WriteToConsole(LogLevel level, string logEntry)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            // Color-code by level
            Console.ForegroundColor = level switch
            {
                LogLevel.Verbose => ConsoleColor.DarkGray,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warn => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.WriteLine(logEntry);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>
    /// Queue a log entry for batched writing (fire-and-forget)
    /// </summary>
    private void QueueLogEntry(LogLevel level, string logEntry)
    {
        if (_disposed) return;

        // Write to file if level meets the configured minimum
        // (console output is already filtered by the caller)
        if (level >= MinimumLevel)
        {
            var logFile = Path.Combine(_logsBaseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
            _logQueue.Enqueue((logFile, logEntry));
        }
    }

    /// <summary>
    /// Flush queued log entries to disk (called every 100ms by timer)
    /// Groups entries by log file and writes in batches
    /// </summary>
    private void FlushLogBatch()
    {
        if (_disposed || _logQueue.IsEmpty) return;

        lock (_batchLock)
        {
            if (_logQueue.IsEmpty) return;

            try
            {
                // Dequeue all pending logs
                var batch = new List<(string logFile, string logEntry)>();
                while (_logQueue.TryDequeue(out var entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count == 0) return;

                // Group by log file for efficient batch writing
                var groupedLogs = batch
                    .GroupBy(x => x.logFile)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.logEntry).ToList());

                // Write each file's batch in one operation
                foreach (var (logFile, entries) in groupedLogs)
                {
                    try
                    {
                        // Combine all entries with newlines
                        var combinedContent = string.Join(Environment.NewLine, entries) + Environment.NewLine;
                        File.AppendAllText(logFile, combinedContent);
                    }
                    catch (Exception ex)
                    {
                        // If logging fails, write to console as fallback
                        Console.WriteLine($"[LogHelper] Failed to write {entries.Count} log entries to {logFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogHelper] Error in FlushLogBatch: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop the timer and flush any remaining logs
        _batchTimer?.Stop();
        FlushLogBatch();
        _batchTimer?.Dispose();

        _writeLock.Dispose();
    }
}
