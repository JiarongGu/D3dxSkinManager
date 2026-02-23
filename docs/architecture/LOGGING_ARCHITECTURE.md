# Logging Architecture

## Overview

Unified logging system with frontend-backend integration, daily rotation, and configurable levels.

## Log Levels

| Level | Value | Use Case |
|-------|-------|----------|
| **ALL** | 4 | Trace everything (deep debugging) |
| **DEBUG** | 0 | Development diagnostics |
| **INFO** | 1 | Normal operations (recommended) |
| **WARN** | 2 | Potential issues |
| **ERROR** | 3 | Failures/exceptions |
| **OFF** | -1 | Silent mode (default) |

## Architecture

```
Frontend Console
    ↓ Optional IPC
Backend LogHelper
    ↓ Level Filter
File System (data/logs/)
```

## Backend Implementation

### LogHelper Pattern
```csharp
public class LogHelper
{
    private readonly IGlobalPathService _globalPathService;
    private readonly LogLevel _minimumLevel;

    public void Log(LogLevel level, string message, string className)
    {
        if (_minimumLevel == LogLevel.Off) return;
        if ((int)level < (int)_minimumLevel) return;

        var logEntry = $"[{DateTime.Now:HH:mm:ss}] [{level}] [{className}] {message}";

        // Write to daily log file
        var logPath = Path.Combine(_globalPathService.LogsPath,
                                   $"app-{DateTime.Now:yyyy-MM-dd}.log");
        File.AppendAllTextAsync(logPath, logEntry + Environment.NewLine);
    }
}
```

### Service Usage
```csharp
public class ModService
{
    private readonly LogHelper _logger;

    public async Task LoadModAsync(string sha)
    {
        _logger.Info($"Loading mod: {sha}");
        try
        {
            // Load mod
            _logger.Debug($"Mod loaded: {sha}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load mod {sha}: {ex.Message}");
            throw;
        }
    }
}
```

## Frontend Implementation

### Console Wrapper
```typescript
class LogService {
  private minLevel: LogLevel;

  log(level: LogLevel, message: string, ...args: any[]) {
    if (this.minLevel === LogLevel.Off) return;
    if (level < this.minLevel) return;

    // Always log to console in dev
    if (import.meta.env.DEV) {
      console[level](message, ...args);
    }

    // Optionally send to backend
    if (this.sendToBackend) {
      bridgeService.sendMessage({
        module: 'SYSTEM',
        type: 'LOG',
        payload: { level, message, timestamp: Date.now() }
      });
    }
  }
}
```

### Component Usage
```typescript
const logger = useLogger();

// Simple logging
logger.info('Mod loaded successfully');
logger.error('Failed to load mod', error);
logger.debug('Cache hit for', modId);
```

## Configuration

### Global Settings
```csharp
public class GlobalSettings
{
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Off;
    public bool EnableFrontendLogging { get; set; } = false;
}
```

### Runtime Updates
```csharp
// SettingsFacade.cs
public async Task UpdateLogLevelAsync(LogLevel newLevel)
{
    AppEnvironment.MinimumLogLevel = newLevel;
    _globalSettings.MinimumLogLevel = newLevel;
    await SaveSettingsAsync();
}
```

## File Management

### Structure
```
data/
└── logs/
    ├── app-2024-01-15.log  # Today
    ├── app-2024-01-14.log  # Yesterday
    └── [7 days retention]
```

### Rotation
- Daily files: `app-{yyyy-MM-dd}.log`
- Automatic cleanup after 7 days
- Async writes to prevent blocking

### Log Format
```
[14:23:45] [INFO] [ModService] Loading mod: abc123
[14:23:46] [ERROR] [ModService] Failed to load mod: File not found
```

## Best Practices

1. **Use IGlobalPathService** - Never hardcode log paths
2. **Class Name Context** - Always include source class
3. **Structured Messages** - Use consistent format
4. **Exception Details** - Log full exception info at ERROR level
5. **Performance** - Use DEBUG for detailed traces
6. **User Actions** - Log at INFO level

## Performance Considerations

1. **Async Writes** - Non-blocking file operations
2. **Level Filtering** - Skip logs below threshold
3. **Dev vs Prod** - Console only in development
4. **Batching** - Group writes when possible
5. **File Size** - Daily rotation prevents huge files

## Common Patterns

### Operation Tracking
```csharp
_logger.Info($"Starting operation: {operationId}");
var sw = Stopwatch.StartNew();
// ... operation ...
_logger.Info($"Completed {operationId} in {sw.ElapsedMilliseconds}ms");
```

### Error Context
```csharp
catch (Exception ex)
{
    _logger.Error($"Context: ProfileId={profileId}, ModId={modId}");
    _logger.Error($"Error: {ex.Message}");
    _logger.Debug($"StackTrace: {ex.StackTrace}");
}
```

### User Actions
```typescript
logger.info('User action: Switch profile', {
  from: oldProfile,
  to: newProfile
});
```

## Related Documentation

- [TROUBLESHOOTING.md](../ai-assistant/TROUBLESHOOTING.md) - Using logs for debugging
- [DESIGN_DECISIONS.md](../core/DESIGN_DECISIONS.md) - Logging architecture decision