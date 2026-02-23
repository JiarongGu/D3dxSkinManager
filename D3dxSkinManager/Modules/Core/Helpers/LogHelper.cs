using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Log levels for categorizing log messages
/// Matches frontend LogLevel enum exactly
/// </summary>
public enum LogLevel
{
    /// <summary>Debug - Verbose diagnostic information</summary>
    Debug = 0,

    /// <summary>Info - General informational messages</summary>
    Info = 1,

    /// <summary>Warn - Warning messages</summary>
    Warn = 2,

    /// <summary>Error - Error messages</summary>
    Error = 3,

    /// <summary>All - Show everything (special value for filtering)</summary>
    All = 4,

    /// <summary>Off - Disable all logging (special value for filtering)</summary>
    Off = -1
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
    }

    public static LogHelper Create(AppEnvironment environment) 
    {
        var globalPaths = new GlobalPathService(environment);
        return new LogHelper(globalPaths, environment);
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
        // - Console Output: Development mode ONLY (always shows all logs regardless of settings)
        // - File Output: Respects log level settings in both dev and prod modes
        // - Production: No console output at all, only file output based on settings
        // - Log Level Settings: Apply ONLY to file output, not console

        // Console: Development only, shows everything
        if (_appEnvironment.IsDevelopment)
        {
            WriteToConsole(level, logEntry);
        }

        // File: Respects log level settings (OFF, DEBUG, INFO, WARNING, ERROR, ALL)
        if (ShouldLog(level))
        {
            _ = WriteToFileAsync(level, logEntry, source);
        }
    }

    private bool ShouldLog(LogLevel level)
    {
        // Skip logging if disabled (Off=-1)
        if (_appEnvironment.MinimumLogLevel == LogLevel.Off)
        {
            return false;
        }

        // Log everything if minimum is All=4
        if (_appEnvironment.MinimumLogLevel == LogLevel.All)
        {
            return true;
        }

        // Otherwise, check if level meets minimum threshold
        return level >= _appEnvironment.MinimumLogLevel;
    }

    public async Task FlushAsync()
    {
        // Wait for any pending writes to complete
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Just release - actual flushing happens in WriteToFileAsync
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

    private async Task WriteToFileAsync(LogLevel level, string logEntry, string? source)
    {
        if (_disposed) return;

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (level >= LogLevel.Info)
            {
                // Append to log file
                var logFile = Path.Combine(_logsBaseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
                await File.AppendAllTextAsync(logFile, logEntry + Environment.NewLine).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // If logging fails, write to console as fallback
            Console.WriteLine($"[LogHelper] Failed to write to log file: {ex.Message}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _writeLock.Dispose();
    }
}
