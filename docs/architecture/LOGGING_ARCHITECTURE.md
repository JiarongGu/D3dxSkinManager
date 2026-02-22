# Logging Architecture

## Overview

The D3dxSkinManager uses a unified, level-based logging system with frontend-backend integration. Logs are written to `data\logs` directory with daily rotation.

## Key Principles

### 1. Unified Frontend-Backend Logging
- **Frontend**: Browser console + optional backend persistence
- **Backend**: Centralized file logging in `data\logs`
- **Integration**: Frontend logs can be sent to backend for unified persistence
- **Benefit**: Complete visibility across the entire application stack

### 2. Configurable Log Levels
- **Levels**: ALL (4), DEBUG (0), INFO (1), WARN (2), ERROR (3), OFF (-1)
- **Frontend & Backend**: Synchronized log level from GlobalSettings
- **Default**: OFF (until user configures)
- **Development Console**: Always shows all logs regardless of settings (dev only)
- **File Output**: Respects log level settings in both dev and prod

### 3. Simple Initialization
- **AppEnvironment**: Reads log level from GlobalSettings on startup
- **No Complex Extensions**: Direct instantiation, no unnecessary wrappers
- **Single Source of Truth**: AppEnvironment.MinimumLogLevel
- **Runtime Updates**: GlobalSettingsService updates AppEnvironment.MinimumLogLevel

### 4. Use GlobalPathService
- **Lesson Learned**: Services should not manage their own file paths
- **Solution**: Always use `IGlobalPathService` for path management
- **Why**: Centralized path management, consistency, easier testing

## Log Levels

The following log levels are available in both frontend and backend:

| Level | Value | Description | Use Case |
|-------|-------|-------------|----------|
| **ALL** | 4 | Show everything | Deep debugging, trace all execution |
| **DEBUG** | 0 | Debug messages | Development, detailed diagnostics |
| **INFO** | 1 | Informational | General application flow (recommended) |
| **WARN** | 2 | Warnings | Potential issues, deprecated usage |
| **ERROR** | 3 | Errors | Failures, exceptions |
| **OFF** | -1 | Disable logging | Silent mode (default) |

### Log Level Filtering Logic

```csharp
// Backend (LogHelper.cs)
if (_minimumLevel == LogLevel.Off)
    return; // Logging disabled

if (_minimumLevel != LogLevel.All && level < _minimumLevel)
    return; // Filter out logs below minimum level
```

```typescript
// Frontend (logger.ts)
if (currentLevel === LogLevel.OFF)
    return false; // Logging disabled

if (currentLevel === LogLevel.ALL)
    return true; // Log everything

return level >= currentLevel; // Filter based on level
```

## File Structure

```
data\logs\
└── {yyyy-MM-dd}.log          # Daily log file (INFO+ messages)
```

Example log entry:
```
[2026-02-23 14:30:15.123] [INFO   ] [SourceName] Message content
[timestamp]               [level  ] [source    ] message
```

## Frontend Logging

### Logger Usage

```typescript
import { logger } from 'shared/utils/logger';

// Log messages
logger.debug('Detailed diagnostic info', { userId: 123 });
logger.info('User logged in');
logger.warn('Deprecated API called');
logger.error('Failed to load data', error);

// Configure log level
logger.setLevel('DEBUG');
logger.setLevel(LogLevel.INFO);

// Enable backend persistence (send logs to backend)
logger.setPersistence(true);
```

### Log Level Configuration

Log level is stored in `GlobalSettings` and synchronized with backend:
- **UI**: Settings page has dropdown to select log level
- **Persistence**: Saved to `data/settings/global.json`
- **Sync**: Frontend automatically loads level from backend on init

### Frontend Log Persistence

When persistence is enabled (`logger.setPersistence(true)`):
1. Frontend logs are sent to backend via IPC
2. Backend writes them to daily log file with `[Frontend]` prefix
3. Fire-and-forget pattern prevents blocking
4. Silently fails if backend unavailable (logs still in browser console)

```typescript
// Example log flow with persistence enabled
logger.info('User action');
// -> Browser console: [INFO] User action
// -> Backend file: [2026-02-23 14:30:15.123] [INFO] [Frontend] User action
```

## Backend Logging

### LogHelper Usage

```csharp
public class MyService
{
    private readonly ILogHelper _logger;

    public MyService(ILogHelper logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.Info("Service initialized", "MyService");
        _logger.Debug("Detailed state info", "MyService");
        _logger.Warning("Deprecated method used", "MyService");
        _logger.Error("Operation failed", "MyService", exception);
    }
}
```

### Log Level Enum

```csharp
public enum LogLevel
{
    Debug = 0,      // Verbose diagnostic information
    Info = 1,       // General informational messages
    Warning = 2,    // Warning messages
    Error = 3,      // Error messages
    All = 4,        // Show everything (special filter value)
    Off = -1        // Disable all logging (special filter value)
}
```

### LogHelper Initialization

```csharp
// For DI container (preferred)
public LogHelper(IGlobalPathService globalPaths, AppEnvironment appEnvironment)
{
    _globalPaths = globalPaths;
    _appEnvironment = appEnvironment;
    _logsBaseDirectory = _globalPaths.LogsDirectory;
    // AppEnvironment.MinimumLogLevel already configured from GlobalSettings
}

// For bootstrap/manual instantiation
public static LogHelper Create(AppEnvironment environment)
{
    var globalPaths = new GlobalPathService(environment);
    return new LogHelper(globalPaths, environment);
}
```

### AppEnvironment Initialization

```csharp
// In AppEnvironment.Create()
private static LogLevel ReadLogLevel(AppEnvironment environment)
{
    var globalPathService = new GlobalPathService(environment);
    var globalSettingService = new GlobalSettingsService(globalPathService, environment);
    return globalSettingService.GetLogLevelAsync().GetAwaiter().GetResult();
}
```

### Service Registration (Direct ServiceCollection)

```csharp
// In ApplicationHost - No ServiceContainer wrapper needed
var services = new ServiceCollection();
services.AddSingleton(_environment);
services.AddCoreServices();
services.AddSettingsServices();
// ... other services
_serviceProvider = services.BuildServiceProvider();
```

## Frontend-Backend Integration

### IPC Message Flow

```
Frontend                              Backend
--------                              -------
logger.error("msg")
  |
  ├─> Browser console
  |
  └─> (if persistence enabled)
      bridgeService.sendMessage({
        module: 'SYSTEM',
        type: 'LOG_FROM_FRONTEND',
        payload: {
          level: 'ERROR',
          message: 'msg',
          timestamp: '...',
          source: 'Frontend'
        }
      })
                                  ──> SystemFacade
                                       |
                                       └─> LogHelper.Log()
                                           |
                                           └─> File: [Frontend] msg
```

### Handling Frontend Logs in Backend

SystemFacade handles `LOG_FROM_FRONTEND` messages:

```csharp
private object LogFromFrontendHandler(IpcRequest request)
{
    var level = GetValue("level") ?? "INFO";
    var message = GetValue("message") ?? "";
    var source = GetValue("source") ?? "Frontend";

    var logLevel = level.ToUpperInvariant() switch
    {
        "DEBUG" => LogLevel.Debug,
        "INFO" => LogLevel.Info,
        "WARN" or "WARNING" => LogLevel.Warning,
        "ERROR" => LogLevel.Error,
        _ => LogLevel.Info
    };

    _logger.Log(logLevel, $"[Frontend] {message}", source);
    return new { success = true };
}
```

## Performance Considerations

1. **Async File Writing**: All file I/O is async to avoid blocking
2. **Fire and Forget**: Log writing doesn't block the calling code
3. **SemaphoreSlim**: Thread-safe with minimal overhead
4. **Early Filtering**: Logs below minimum level are filtered before formatting
5. **Frontend Persistence**: Fire-and-forget IPC, no await/blocking

## Common Mistakes to Avoid

### 1. Don't Use Factory Functions with Custom AddSingleton

❌ **Wrong**:
```csharp
services.AddSingleton<ILogHelper>(sp => new LogHelper(...));
```

✅ **Correct**:
```csharp
AddSingleton<ILogHelper, LogHelper>(services);
```

### 2. Don't Manage Your Own Paths

❌ **Wrong**:
```csharp
_logsBaseDirectory = Path.Combine(baseDirectory, "data", "logs");
```

✅ **Correct**:
```csharp
_logsBaseDirectory = _globalPaths.LogsDirectory;
```

### 3. Don't Use console.log Directly in Frontend

❌ **Wrong**:
```typescript
console.log("User action");
console.error("Failed", error);
```

✅ **Correct**:
```typescript
import { logger } from 'shared/utils/logger';
logger.info("User action");
logger.error("Failed", error);
```

### 4. Don't Await Logger Persistence

❌ **Wrong**:
```typescript
await logger.info("Message"); // Logger methods are void
```

✅ **Correct**:
```typescript
logger.info("Message"); // Fire and forget
```

## Environment Variables

- `D3DX_LOG_LEVEL`: Override default log level (ALL, DEBUG, INFO, WARNING, ERROR, OFF)
- `ASPNETCORE_ENVIRONMENT`: Set to "Development" for development mode

## Settings Configuration

Log level is stored in `data/settings/global.json`:

```json
{
  "theme": "light",
  "logLevel": "INFO",
  "language": "en",
  ...
}
```

### UI Configuration

Settings View (`modules/settings/components/SettingsView.tsx`):
- Dropdown with log level options
- Uses `Logger.getLevelOptions()` for options
- Updates GlobalSettings via `settingsService.updateGlobalSetting()`
- Backend applies log level immediately via `GlobalSettingsService.ApplyLogLevel()`

## Benefits of Current Design

1. **Unified**: Frontend and backend logs in one place
2. **Simple**: Easy to understand and maintain
3. **Centralized**: All logs in `data\logs`
4. **Performant**: Async, non-blocking, early filtering
5. **Flexible**: Configurable via UI, settings file, or environment variables
6. **Daily Rotation**: Prevents huge log files
7. **Color-Coded Console**: Easy visual distinction in terminal
8. **Optional Persistence**: Frontend logs can be ephemeral (console only) or persisted

## Migration Notes

If migrating from profile-based logging:
- Old: Logs scattered in `data\profiles\{id}\logs\`
- New: Centralized in `data\logs\`
- Benefit: Easier to find and analyze logs across all profiles
- No separate debug/info/warning/error files - single daily file with level filtering
