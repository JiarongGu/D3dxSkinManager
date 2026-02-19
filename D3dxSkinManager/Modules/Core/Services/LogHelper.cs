using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Log levels for categorizing log messages
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Interface for centralized logging service
/// </summary>
public interface ILogHelper
{
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
    void Warning(string message, string? source = null);

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
/// Writes logs to profile-specific log directory and console
/// Thread-safe with async file writing
/// </summary>
public class LogHelper : ILogHelper, IDisposable
{
    private readonly string _logDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public LogHelper(IProfileContext profileContext)
    {
        _logDirectory = Path.Combine(profileContext.ProfilePath, "logs");

        // Ensure log directory exists
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Debug(string message, string? source = null)
    {
        Log(LogLevel.Debug, message, source);
    }

    public void Info(string message, string? source = null)
    {
        Log(LogLevel.Info, message, source);
    }

    public void Warning(string message, string? source = null)
    {
        Log(LogLevel.Warning, message, source);
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

        // Write to console
        WriteToConsole(level, logEntry);

        // Write to file asynchronously (fire and forget)
        _ = WriteToFileAsync(level, logEntry);
    }

    public async Task FlushAsync()
    {
        // Wait for any pending writes to complete
        await _writeLock.WaitAsync();
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
                LogLevel.Warning => ConsoleColor.Yellow,
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

    private async Task WriteToFileAsync(LogLevel level, string logEntry)
    {
        if (_disposed) return;

        await _writeLock.WaitAsync();
        try
        {
            // Determine log file based on level
            var logFileName = level switch
            {
                LogLevel.Error => "error.log",
                _ => "app.log"
            };

            var logFilePath = Path.Combine(_logDirectory, logFileName);

            // Append to log file
            await File.AppendAllTextAsync(logFilePath, logEntry + Environment.NewLine);

            // Also write errors to app.log for complete history
            if (level == LogLevel.Error)
            {
                var appLogPath = Path.Combine(_logDirectory, "app.log");
                await File.AppendAllTextAsync(appLogPath, logEntry + Environment.NewLine);
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
