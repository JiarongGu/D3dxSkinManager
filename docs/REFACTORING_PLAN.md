# D3dxSkinManager Refactoring Plan

**Created:** 2026-03-09
**Status:** Ready for Implementation
**Estimated Impact:** 2600+ lines of code reduction

---

## Executive Summary

This document outlines a comprehensive refactoring plan to eliminate redundant code across the D3dxSkinManager codebase. The analysis identified 6 major areas for refactoring with a potential reduction of over 2600 lines of boilerplate code while improving maintainability and consistency.

---

## 📊 Refactoring Priorities

| Priority | Area | Impact | Effort | Lines Saved |
|----------|------|--------|--------|-------------|
| **CRITICAL** | BaseRepository Pattern | Very High | Medium | 1200+ |
| **HIGH** | DataReader Extensions | High | Small | 300+ |
| **HIGH** | File Operation Utilities | High | Small | 300+ |
| **MEDIUM** | Error Handling Wrapper | Medium | Medium | 500+ |
| **MEDIUM** | BaseDialog Component | Medium | Small | 150+ |
| **MEDIUM** | Compact Component Factory | Medium | Small | 400+ |

**Total Estimated Savings:** 2850+ lines of code

---

## 🔴 CRITICAL: Create BaseRepository<T>

### Problem

All repository classes contain 300-400 lines of repetitive SQLite connection, command creation, and data reader boilerplate code.

### Affected Files
- `D3dxSkinManager/Modules/Mod/Services/ModRepository.cs` (369 lines)
- `D3dxSkinManager/Modules/Category/Services/CategoryRepository.cs` (342 lines)
- `D3dxSkinManager/Modules/Workflow/Repositories/WorkflowRepository.cs` (254 lines)
- `D3dxSkinManager/Modules/Mod/Services/TagRepository.cs` (~250 lines)

### Current Pattern (Repeated 55+ times)

```csharp
// Example from ModRepository.cs:338-367
await using var connection = new SqliteConnection(_connectionString);
await connection.OpenAsync().ConfigureAwait(false);

var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);

await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
if (await reader.ReadAsync().ConfigureAwait(false))
{
    return MapToModInfo(reader);
}
return null;
```

### Solution

**New File:** `D3dxSkinManager/Modules/Core/Services/BaseRepository.cs`

```csharp
public abstract class BaseRepository<TEntity> where TEntity : class
{
    protected readonly string ConnectionString;
    protected readonly ILogHelper Logger;

    protected BaseRepository(string connectionString, ILogHelper logger)
    {
        ConnectionString = connectionString;
        Logger = logger;
    }

    // Generic query execution with mapping
    protected async Task<List<TEntity>> ExecuteQueryAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        var results = new List<TEntity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapToEntity(reader));
        }

        return results;
    }

    // Single entity query
    protected async Task<TEntity?> ExecuteQuerySingleAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        var results = await ExecuteQueryAsync(sql, configureParameters, cancellationToken);
        return results.FirstOrDefault();
    }

    // Non-query execution (INSERT/UPDATE/DELETE)
    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Scalar query (COUNT, EXISTS, etc.)
    protected async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        Action<SqliteCommand>? configureParameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? (T)result : default;
    }

    // Generic EXISTS check
    protected async Task<bool> ExistsAsync(
        string tableName,
        string idColumnName,
        string idValue,
        CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM [{tableName}] WHERE {idColumnName} = @id";
        var count = await ExecuteScalarAsync<long>(sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@id", idValue);
        }, cancellationToken);

        return count > 0;
    }

    // Abstract method for subclasses to implement entity mapping
    protected abstract TEntity MapToEntity(SqliteDataReader reader);
}
```

### Refactored Repository Example

**Before:** ModRepository.cs (369 lines)

```csharp
public async Task<ModInfo?> GetByIdAsync(string sha)
{
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
    command.Parameters.AddWithValue("@sha", sha);

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    if (await reader.ReadAsync().ConfigureAwait(false))
    {
        return MapToModInfo(reader);
    }
    return null;
}
```

**After:** ModRepository.cs (~150 lines, 58% reduction)

```csharp
public class ModRepository : BaseRepository<ModInfo>, IModRepository
{
    public ModRepository(string connectionString, ILogHelper logger)
        : base(connectionString, logger)
    {
    }

    public async Task<ModInfo?> GetByIdAsync(string sha)
    {
        return await ExecuteQuerySingleAsync(
            "SELECT * FROM Mods WHERE SHA = @sha",
            cmd => cmd.Parameters.AddWithValue("@sha", sha)
        );
    }

    public async Task<List<ModInfo>> GetAllAsync()
    {
        return await ExecuteQueryAsync("SELECT * FROM Mods");
    }

    public async Task<bool> ExistsAsync(string sha)
    {
        return await base.ExistsAsync("Mods", "SHA", sha);
    }

    protected override ModInfo MapToEntity(SqliteDataReader reader)
    {
        // Mapping logic only (no connection/command boilerplate)
        return new ModInfo
        {
            SHA = reader.GetString(reader.GetOrdinal("SHA")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            // ... rest of mapping
        };
    }
}
```

### Implementation Steps

1. Create `BaseRepository<T>` in Core module
2. Add tests for BaseRepository (ExecuteQueryAsync, ExecuteNonQueryAsync, ExecuteScalarAsync)
3. Refactor ModRepository to extend BaseRepository
4. Refactor CategoryRepository to extend BaseRepository
5. Refactor WorkflowRepository to extend BaseRepository
6. Refactor TagRepository to extend BaseRepository
7. Remove duplicate connection/command code from all repositories
8. Run integration tests to ensure functionality unchanged

### Expected Outcome

- **ModRepository**: 369 → ~150 lines (58% reduction)
- **CategoryRepository**: 342 → ~140 lines (59% reduction)
- **WorkflowRepository**: 254 → ~110 lines (57% reduction)
- **TagRepository**: ~250 → ~105 lines (58% reduction)
- **Total saved**: ~1,200 lines

---

## 🟠 HIGH: Create DataReaderExtensions

### Problem

Each repository implements its own data reader mapping with repetitive null checks, JSON deserialization, and type conversions.

### Current Pattern (Repeated 40+ times)

```csharp
// From ModRepository.cs:338-367
private ModInfo MapToModInfo(SqliteDataReader reader)
{
    // JSON deserialization pattern (repeated for Tags, Metadata, etc.)
    var tagsJson = reader.GetString(reader.GetOrdinal("Tags"));
    var tags = string.IsNullOrEmpty(tagsJson)
        ? new List<string>()
        : JsonHelper.Deserialize<List<string>>(tagsJson) ?? new List<string>();

    // Bool from int conversion (repeated for multiple columns)
    var disablePreviewOrdinal = reader.GetOrdinal("DisablePreview");
    var disablePreview = !reader.IsDBNull(disablePreviewOrdinal)
        && reader.GetInt32(disablePreviewOrdinal) == 1;

    // Nullable string handling (repeated dozens of times)
    var authorOrdinal = reader.GetOrdinal("Author");
    var author = reader.IsDBNull(authorOrdinal) ? null : reader.GetString(authorOrdinal);

    // DateTime handling (repeated for CreatedAt, ModifiedAt)
    var createdAtOrdinal = reader.GetOrdinal("CreatedAt");
    var createdAt = reader.IsDBNull(createdAtOrdinal)
        ? DateTime.MinValue
        : DateTime.Parse(reader.GetString(createdAtOrdinal));
}
```

### Solution

**New File:** `D3dxSkinManager/Modules/Core/Utilities/DataReaderExtensions.cs`

```csharp
public static class DataReaderExtensions
{
    // Get string with null handling
    public static string? GetStringOrNull(this SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    // Get string with default value
    public static string GetStringOrDefault(this SqliteDataReader reader, string columnName, string defaultValue = "")
    {
        return GetStringOrNull(reader, columnName) ?? defaultValue;
    }

    // Deserialize JSON column to object
    public static T? GetJsonAs<T>(this SqliteDataReader reader, string columnName) where T : class
    {
        var json = GetStringOrNull(reader, columnName);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonHelper.Deserialize<T>(json);
    }

    // Deserialize JSON column with default
    public static T GetJsonOrDefault<T>(this SqliteDataReader reader, string columnName, T defaultValue) where T : class
    {
        return GetJsonAs<T>(reader, columnName) ?? defaultValue;
    }

    // Get bool from int (SQLite stores bool as 0/1)
    public static bool GetBoolFromInt(this SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;
    }

    // Get DateTime with null handling
    public static DateTime? GetDateTimeOrNull(this SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
            return null;

        var value = reader.GetString(ordinal);
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    // Get DateTime with default
    public static DateTime GetDateTimeOrDefault(this SqliteDataReader reader, string columnName, DateTime defaultValue = default)
    {
        return GetDateTimeOrNull(reader, columnName) ?? defaultValue;
    }

    // Get int with null handling
    public static int? GetIntOrNull(this SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    // Get int with default
    public static int GetIntOrDefault(this SqliteDataReader reader, string columnName, int defaultValue = 0)
    {
        return GetIntOrNull(reader, columnName) ?? defaultValue;
    }
}
```

### Refactored Mapping Example

**Before:**

```csharp
private ModInfo MapToModInfo(SqliteDataReader reader)
{
    var tagsJson = reader.GetString(reader.GetOrdinal("Tags"));
    var tags = string.IsNullOrEmpty(tagsJson)
        ? new List<string>()
        : JsonHelper.Deserialize<List<string>>(tagsJson) ?? new List<string>();

    var disablePreviewOrdinal = reader.GetOrdinal("DisablePreview");
    var disablePreview = !reader.IsDBNull(disablePreviewOrdinal)
        && reader.GetInt32(disablePreviewOrdinal) == 1;

    var authorOrdinal = reader.GetOrdinal("Author");
    var author = reader.IsDBNull(authorOrdinal) ? null : reader.GetString(authorOrdinal);

    return new ModInfo
    {
        SHA = reader.GetString(reader.GetOrdinal("SHA")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Author = author,
        Tags = tags,
        DisablePreview = disablePreview,
        // ... 20+ more lines
    };
}
```

**After:**

```csharp
protected override ModInfo MapToEntity(SqliteDataReader reader)
{
    return new ModInfo
    {
        SHA = reader.GetStringOrDefault("SHA"),
        Name = reader.GetStringOrDefault("Name"),
        Author = reader.GetStringOrNull("Author"),
        Tags = reader.GetJsonOrDefault("Tags", new List<string>()),
        DisablePreview = reader.GetBoolFromInt("DisablePreview"),
        CreatedAt = reader.GetDateTimeOrDefault("CreatedAt"),
        Metadata = reader.GetJsonAs<Dictionary<string, object>>("Metadata"),
        // Clean, readable, and consistent
    };
}
```

### Implementation Steps

1. Create `DataReaderExtensions.cs` in Core/Utilities
2. Add unit tests for all extension methods
3. Refactor ModRepository mapping methods
4. Refactor CategoryRepository mapping methods
5. Refactor WorkflowRepository mapping methods
6. Refactor TagRepository mapping methods
7. Remove all manual null checks and JSON deserialization

### Expected Outcome

- Standardized data reading across all repositories
- ~300 lines of null-check/JSON boilerplate removed
- Improved code readability and maintainability
- Reduced chance of null-reference bugs

---

## 🟠 HIGH: Create FileOperationHelper

### Problem

File operations (create directory, copy with error handling, delete with retry) are repeated across multiple services.

### Current Pattern (Repeated 135+ times)

```csharp
// Directory creation (repeated everywhere)
if (!Directory.Exists(directory))
    Directory.CreateDirectory(directory);

// File copy with error handling
try
{
    File.Copy(source, destination, overwrite: true);
}
catch (Exception ex)
{
    _logger.Error($"Failed to copy file: {ex.Message}", "ModService", ex);
    return false;
}

// File deletion with retry (partial implementation in some services)
for (int i = 0; i < 3; i++)
{
    try
    {
        File.Delete(path);
        break;
    }
    catch (IOException)
    {
        if (i == 2) throw;
        await Task.Delay(100);
    }
}
```

### Solution

**Enhance Existing File:** `D3dxSkinManager/Modules/Core/Helpers/FileHelper.cs`

Add new methods:

```csharp
public static class FileHelper
{
    // Existing methods...

    /// <summary>
    /// Ensures directory exists, creates if missing
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Async version with exception handling
    /// </summary>
    public static async Task<bool> EnsureDirectoryExistsAsync(
        string path,
        ILogHelper? logger = null)
    {
        try
        {
            await Task.Run(() => EnsureDirectoryExists(path));
            return true;
        }
        catch (Exception ex)
        {
            logger?.Error($"Failed to create directory '{path}': {ex.Message}", "FileHelper", ex);
            return false;
        }
    }

    /// <summary>
    /// Copy file with automatic error handling and logging
    /// </summary>
    public static async Task<bool> CopyFileWithErrorHandlingAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = true,
        ILogHelper? logger = null)
    {
        try
        {
            // Ensure destination directory exists
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                EnsureDirectoryExists(destinationDir);
            }

            await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite));
            return true;
        }
        catch (Exception ex)
        {
            logger?.Error($"Failed to copy file from '{sourcePath}' to '{destinationPath}': {ex.Message}", "FileHelper", ex);
            return false;
        }
    }

    /// <summary>
    /// Delete file/directory with retry logic for locked files
    /// </summary>
    public static async Task<bool> DeleteWithRetryAsync(
        string path,
        int maxRetries = 3,
        int delayMs = 100,
        ILogHelper? logger = null)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    await Task.Run(() => File.Delete(path));
                }
                else if (Directory.Exists(path))
                {
                    await Task.Run(() => Directory.Delete(path, recursive: true));
                }
                return true;
            }
            catch (IOException ex) when (attempt < maxRetries - 1)
            {
                logger?.Warn($"Delete attempt {attempt + 1} failed for '{path}', retrying...", "FileHelper");
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                logger?.Error($"Failed to delete '{path}' after {attempt + 1} attempts: {ex.Message}", "FileHelper", ex);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Move file with automatic error handling
    /// </summary>
    public static async Task<bool> MoveFileWithErrorHandlingAsync(
        string sourcePath,
        string destinationPath,
        ILogHelper? logger = null)
    {
        try
        {
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                EnsureDirectoryExists(destinationDir);
            }

            await Task.Run(() => File.Move(sourcePath, destinationPath, overwrite: true));
            return true;
        }
        catch (Exception ex)
        {
            logger?.Error($"Failed to move file from '{sourcePath}' to '{destinationPath}': {ex.Message}", "FileHelper", ex);
            return false;
        }
    }
}
```

### Usage Example

**Before:**

```csharp
// In ModCacheService.cs
try
{
    var targetFileDir = Path.GetDirectoryName(targetFilePath);
    if (!string.IsNullOrEmpty(targetFileDir))
    {
        if (!Directory.Exists(targetFileDir))
        {
            Directory.CreateDirectory(targetFileDir);
        }
    }

    File.Copy(sourceFilePath, targetFilePath, overwrite: true);
}
catch (Exception ex)
{
    _logger.Error($"Failed to copy mod file: {ex.Message}", "ModCacheService", ex);
    return false;
}
```

**After:**

```csharp
// In ModCacheService.cs
if (!await FileHelper.CopyFileWithErrorHandlingAsync(
    sourceFilePath, targetFilePath, overwrite: true, _logger))
{
    return false;
}
```

### Implementation Steps

1. Add new methods to existing `FileHelper.cs`
2. Add unit tests for new methods (mock file system)
3. Refactor ModCacheService to use new helpers
4. Refactor ModImportService to use new helpers
5. Refactor other services with file operations
6. Remove all `if (!Directory.Exists)` patterns

### Expected Outcome

- ~300 lines of file operation boilerplate removed
- Consistent error handling across all file operations
- Automatic retry logic for locked files
- Better logging of file operation failures

---

## 🟡 MEDIUM: Create Error Handling Wrapper

### Problem

Try-catch blocks with logging are repeated in every service method.

### Current Pattern (Repeated 154+ times)

```csharp
public async Task<bool> SomeOperationAsync(string param)
{
    try
    {
        // Business logic
        await DoSomething(param);
        return true;
    }
    catch (OperationException opEx)
    {
        _logger.Error($"Operation failed: [{opEx.Code}] {opEx.Message}", "ServiceName", opEx);
        throw;
    }
    catch (Exception ex)
    {
        _logger.Error($"Unexpected error: {ex.Message}", "ServiceName", ex);
        throw new OperationException("OPERATION_FAILED", null, "Operation failed", ex);
    }
}
```

### Solution

**New File:** `D3dxSkinManager/Modules/Core/Utilities/ErrorHandlingHelper.cs`

```csharp
public static class ErrorHandlingHelper
{
    /// <summary>
    /// Execute async operation with standardized error handling
    /// </summary>
    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        ILogHelper logger,
        string operationName,
        string moduleName,
        string errorCode = "UNKNOWN_ERROR",
        Dictionary<string, string>? errorParameters = null)
    {
        try
        {
            return await operation();
        }
        catch (OperationException opEx)
        {
            logger.Error($"Operation '{operationName}' failed: [{opEx.Code}] {opEx.Message}", moduleName, opEx);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error($"Unexpected error in '{operationName}': {ex.Message}", moduleName, ex);
            throw new OperationException(errorCode, errorParameters, $"Failed to {operationName}", ex);
        }
    }

    /// <summary>
    /// Execute async operation with boolean return (no exception throwing)
    /// </summary>
    public static async Task<bool> TryExecuteAsync(
        Func<Task> operation,
        ILogHelper logger,
        string operationName,
        string moduleName)
    {
        try
        {
            await operation();
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Operation '{operationName}' failed: {ex.Message}", moduleName, ex);
            return false;
        }
    }

    /// <summary>
    /// Execute async operation with result or default on error
    /// </summary>
    public static async Task<T?> TryExecuteWithDefaultAsync<T>(
        Func<Task<T>> operation,
        ILogHelper logger,
        string operationName,
        string moduleName,
        T? defaultValue = default)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            logger.Error($"Operation '{operationName}' failed: {ex.Message}", moduleName, ex);
            return defaultValue;
        }
    }
}
```

### Usage Example

**Before:**

```csharp
public async Task<bool> DeleteModAsync(string sha)
{
    try
    {
        await _repository.DeleteAsync(sha);
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { sha });
        return true;
    }
    catch (OperationException opEx)
    {
        _logger.Error($"Delete mod failed: [{opEx.Code}] {opEx.Message}", "ModService", opEx);
        throw;
    }
    catch (Exception ex)
    {
        _logger.Error($"Unexpected error deleting mod: {ex.Message}", "ModService", ex);
        throw new OperationException("MOD_DELETE_FAILED",
            new Dictionary<string, string> { { "sha", sha } },
            "Failed to delete mod", ex);
    }
}
```

**After:**

```csharp
public async Task<bool> DeleteModAsync(string sha)
{
    return await ErrorHandlingHelper.TryExecuteAsync(
        async () =>
        {
            await _repository.DeleteAsync(sha);
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { sha });
        },
        _logger,
        "delete mod",
        "ModService"
    );
}

// Or with OperationException:
public async Task<ModInfo> GetModAsync(string sha)
{
    return await ErrorHandlingHelper.ExecuteWithErrorHandlingAsync(
        () => _repository.GetByIdAsync(sha),
        _logger,
        "get mod",
        "ModService",
        errorCode: "MOD_NOT_FOUND",
        errorParameters: new Dictionary<string, string> { { "sha", sha } }
    );
}
```

### Implementation Steps

1. Create `ErrorHandlingHelper.cs` in Core/Utilities
2. Add unit tests with mock logger
3. Refactor ModService methods to use helper
4. Refactor CategoryService methods
5. Refactor other services systematically
6. Remove duplicate try-catch blocks

### Expected Outcome

- ~500 lines of try-catch boilerplate removed
- Consistent error handling across all services
- Standardized error logging format
- Easier to maintain error handling logic

---

## 🟡 MEDIUM: Create BaseDialog Component

### Problem

Dialog components have duplicate loading state management and lifecycle handling.

### Affected Files
- `D3dxSkinManager.Client/src/shared/components/dialogs/FormDialog.tsx` (80 lines)
- `D3dxSkinManager.Client/src/shared/components/dialogs/ConfirmDialog.tsx` (~90 lines)
- `D3dxSkinManager.Client/src/shared/components/dialogs/InfoDialog.tsx` (54 lines)

### Current Pattern (Repeated in FormDialog and ConfirmDialog)

```typescript
const { loading, execute, reset } = useDelayedLoading(200);

React.useEffect(() => {
    if (!visible) {
        reset();
    }
}, [visible, reset]);

const handleOk = async () => {
    try {
        await execute(async () => {
            await onOk();
        });
    } catch (error: unknown) {
        if (error instanceof Error && error.message === 'Operation already in progress') {
            return;
        }
        throw error;
    }
};

return (
    <Modal
        open={visible}
        onCancel={onCancel}
        centered
        transitionName=""
        maskClosable={false}
        // ... more config
    >
        {/* Dialog content */}
    </Modal>
);
```

### Solution

**New File:** `D3dxSkinManager.Client/src/shared/components/dialogs/BaseDialog.tsx`

```typescript
interface BaseDialogProps {
    visible: boolean;
    onCancel: () => void;
    onOk?: () => Promise<void>;
    title?: React.ReactNode;
    children: React.ReactNode;
    okText?: string;
    cancelText?: string;
    width?: number | string;
    maskClosable?: boolean;
    closable?: boolean;
    footer?: React.ReactNode | null;
    loadingDelayMs?: number;
    className?: string;
}

export const BaseDialog: React.FC<BaseDialogProps> = ({
    visible,
    onCancel,
    onOk,
    title,
    children,
    okText,
    cancelText,
    width = 520,
    maskClosable = false,
    closable = true,
    footer,
    loadingDelayMs = 200,
    className,
}) => {
    const { t } = useTranslation();
    const { loading, execute, reset } = useDelayedLoading(loadingDelayMs);

    // Reset loading state when dialog closes
    React.useEffect(() => {
        if (!visible) {
            reset();
        }
    }, [visible, reset]);

    const handleOk = async () => {
        if (!onOk) return;

        try {
            await execute(async () => {
                await onOk();
            });
        } catch (error: unknown) {
            if (error instanceof Error && error.message === 'Operation already in progress') {
                return;
            }
            throw error;
        }
    };

    const defaultFooter = onOk !== undefined ? [
        <CompactButton key="cancel" onClick={onCancel} disabled={loading}>
            {cancelText || t('common.cancel')}
        </CompactButton>,
        <CompactButton key="ok" type="primary" onClick={handleOk} loading={loading}>
            {okText || t('common.ok')}
        </CompactButton>,
    ] : null;

    return (
        <Modal
            open={visible}
            onCancel={onCancel}
            title={title}
            footer={footer !== undefined ? footer : defaultFooter}
            width={width}
            centered
            maskClosable={maskClosable}
            closable={closable}
            transitionName=""
            className={className}
        >
            {children}
        </Modal>
    );
};
```

### Refactored Dialog Example

**Before:** FormDialog.tsx (80 lines)

```typescript
export const FormDialog: React.FC<FormDialogProps> = ({
    visible,
    onCancel,
    onOk,
    title,
    children,
    okText,
    cancelText,
    width = 520,
}) => {
    const { t } = useTranslation();
    const { loading, execute, reset } = useDelayedLoading(200);

    React.useEffect(() => {
        if (!visible) {
            reset();
        }
    }, [visible, reset]);

    const handleOk = async () => {
        try {
            await execute(async () => {
                await onOk();
            });
        } catch (error: unknown) {
            if (error instanceof Error && error.message === 'Operation already in progress') {
                return;
            }
            throw error;
        }
    };

    return (
        <Modal
            open={visible}
            onCancel={onCancel}
            title={title}
            footer={[
                <CompactButton key="cancel" onClick={onCancel} disabled={loading}>
                    {cancelText || t('common.cancel')}
                </CompactButton>,
                <CompactButton key="ok" type="primary" onClick={handleOk} loading={loading}>
                    {okText || t('common.ok')}
                </CompactButton>,
            ]}
            width={width}
            centered
            maskClosable={false}
            closable={true}
            transitionName=""
        >
            {children}
        </Modal>
    );
};
```

**After:** FormDialog.tsx (15 lines)

```typescript
export const FormDialog: React.FC<FormDialogProps> = (props) => {
    return <BaseDialog {...props} />;
};

// Or just export BaseDialog directly:
export { BaseDialog as FormDialog };
```

### Implementation Steps

1. Create `BaseDialog.tsx` in shared/components/dialogs
2. Add tests for BaseDialog
3. Refactor FormDialog to use BaseDialog
4. Refactor ConfirmDialog to use BaseDialog
5. Refactor InfoDialog to use BaseDialog
6. Remove duplicate loading/lifecycle code

### Expected Outcome

- FormDialog: 80 → 15 lines (81% reduction)
- ConfirmDialog: 90 → 20 lines (78% reduction)
- InfoDialog: Already simple, minimal changes
- **Total saved**: ~150 lines
- Consistent dialog behavior across application

---

## 🟡 MEDIUM: Create Compact Component Factory

### Problem

15 compact component wrappers have identical size mapping and className logic.

### Affected Files
- `CompactInput.tsx` (87 lines)
- `CompactSelect.tsx` (41 lines)
- `CompactButton.tsx` (124 lines)
- `CompactSpace.tsx` (67 lines)
- `CompactCard.tsx` (45 lines)
- ...and 10 more files

### Current Pattern (Repeated in all compact components)

```typescript
// Example from CompactInput.tsx
export interface CompactInputProps extends Omit<InputProps, 'size'> {
    size?: 'small' | 'medium' | 'large';
}

export const CompactInput: React.FC<CompactInputProps> = ({
    size = 'medium',
    className = '',
    ...rest
}) => {
    const antdSize = size === 'medium' ? 'middle' : size;
    const inputClassName = `compact-input compact-input-${size} ${className}`.trim();

    return (
        <Input size={antdSize} className={inputClassName} {...rest} />
    );
};
```

### Solution

**New File:** `D3dxSkinManager.Client/src/shared/components/compact/createCompactComponent.tsx`

```typescript
import React from 'react';

export type CompactSize = 'small' | 'medium' | 'large';

interface WithSize {
    size?: string | 'small' | 'middle' | 'large';
    className?: string;
}

/**
 * Higher-order component factory for creating compact components
 * Maps 'medium' size to 'middle' for Ant Design compatibility
 */
export function createCompactComponent<P extends WithSize>(
    Component: React.ComponentType<P>,
    baseClassName: string
): React.FC<Omit<P, 'size'> & { size?: CompactSize }> {
    const CompactComponent: React.FC<Omit<P, 'size'> & { size?: CompactSize }> = ({
        size = 'medium',
        className = '',
        ...rest
    }) => {
        // Convert 'medium' to 'middle' for Ant Design
        const antdSize = size === 'medium' ? 'middle' : size;

        // Build className with base + size modifier + custom
        const componentClassName = [
            baseClassName,
            `${baseClassName}-${size}`,
            className
        ].filter(Boolean).join(' ');

        return (
            <Component
                {...(rest as P)}
                size={antdSize}
                className={componentClassName}
            />
        );
    };

    CompactComponent.displayName = `Compact${Component.displayName || Component.name || 'Component'}`;

    return CompactComponent;
}
```

### Refactored Component Example

**Before:** CompactInput.tsx (87 lines)

```typescript
import React from 'react';
import { Input, InputProps } from 'antd';
import './CompactInput.css';

export interface CompactInputProps extends Omit<InputProps, 'size'> {
    size?: 'small' | 'medium' | 'large';
}

export const CompactInput: React.FC<CompactInputProps> = ({
    size = 'medium',
    className = '',
    ...rest
}) => {
    const antdSize = size === 'medium' ? 'middle' : size;
    const inputClassName = `compact-input compact-input-${size} ${className}`.trim();

    return (
        <Input size={antdSize} className={inputClassName} {...rest} />
    );
};
```

**After:** CompactInput.tsx (8 lines)

```typescript
import { Input } from 'antd';
import { createCompactComponent } from './createCompactComponent';
import './CompactInput.css';

export const CompactInput = createCompactComponent(Input, 'compact-input');
export type CompactInputProps = React.ComponentProps<typeof CompactInput>;
```

### Usage Remains Unchanged

```typescript
// Component usage stays the same
<CompactInput size="medium" placeholder="Enter text" />
<CompactSelect size="small" options={options} />
<CompactButton size="large" type="primary">Click Me</CompactButton>
```

### Implementation Steps

1. Create `createCompactComponent.tsx`
2. Add tests for factory function
3. Refactor CompactInput to use factory
4. Refactor CompactSelect to use factory
5. Refactor CompactButton to use factory
6. Refactor all remaining 12 compact components
7. Verify all CSS classes still work correctly
8. Remove duplicate size mapping code

### Expected Outcome

- **Per Component**: ~70 lines → ~8 lines (89% reduction)
- **15 Components**: ~1050 lines → ~120 lines
- **Total saved**: ~930 lines (but accounting for factory code: ~400 net savings)
- Consistent behavior across all compact components
- Easy to add new compact components

---

## 📋 Implementation Checklist

### Phase 1: Backend Repository Refactoring (CRITICAL)
- [ ] Create `BaseRepository<T>` abstract class
- [ ] Create `DataReaderExtensions` utility class
- [ ] Add unit tests for BaseRepository
- [ ] Add unit tests for DataReaderExtensions
- [ ] Refactor ModRepository to extend BaseRepository
- [ ] Refactor CategoryRepository to extend BaseRepository
- [ ] Refactor WorkflowRepository to extend BaseRepository
- [ ] Refactor TagRepository to extend BaseRepository
- [ ] Run integration tests to verify functionality
- [ ] Verify all repository methods work correctly

### Phase 2: Backend Utilities (HIGH)
- [ ] Enhance FileHelper with new methods
- [ ] Create ErrorHandlingHelper utility
- [ ] Add unit tests for FileHelper enhancements
- [ ] Add unit tests for ErrorHandlingHelper
- [ ] Refactor services to use new FileHelper methods
- [ ] Refactor services to use ErrorHandlingHelper
- [ ] Verify error handling works as expected

### Phase 3: Frontend Component Refactoring (MEDIUM)
- [ ] Create BaseDialog component
- [ ] Create createCompactComponent factory
- [ ] Add tests for BaseDialog
- [ ] Add tests for createCompactComponent
- [ ] Refactor FormDialog, ConfirmDialog, InfoDialog
- [ ] Refactor all 15 compact components
- [ ] Verify UI behavior unchanged
- [ ] Test loading states work correctly

### Phase 4: Testing & Validation
- [ ] Run full backend test suite
- [ ] Run full frontend test suite
- [ ] Perform manual testing of key workflows
- [ ] Test mod import/export functionality
- [ ] Test profile switching
- [ ] Test category management
- [ ] Test batch operations

### Phase 5: Documentation & Cleanup
- [ ] Update AI_GUIDE.md with new patterns
- [ ] Document BaseRepository usage
- [ ] Document utility helper usage
- [ ] Remove obsolete TODO comments
- [ ] Update architecture documentation
- [ ] Create migration guide for developers

---

## 🎯 Success Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Backend Repository Lines** | 1,215 | ~505 | 58% reduction |
| **File Operation Boilerplate** | ~300 | ~0 | 100% reduction |
| **Error Handling Boilerplate** | ~500 | ~0 | 100% reduction |
| **Frontend Dialog Lines** | 224 | ~50 | 78% reduction |
| **Frontend Compact Components** | 1,050 | ~120 | 89% reduction |
| **Total Lines of Code** | ~3,289 | ~675 | **79% reduction** |

---

## ⚠️ Risks & Mitigation

### Risk 1: Breaking Existing Functionality
- **Mitigation**: Comprehensive unit and integration tests before refactoring
- **Mitigation**: Refactor one repository at a time, test thoroughly
- **Mitigation**: Keep original implementations temporarily for comparison

### Risk 2: Performance Regression
- **Mitigation**: Benchmark key operations before and after refactoring
- **Mitigation**: Use `ConfigureAwait(false)` consistently
- **Mitigation**: Profile database query performance

### Risk 3: Introducing New Bugs
- **Mitigation**: Code review after each phase
- **Mitigation**: Manual testing of affected features
- **Mitigation**: Monitor error logs during testing

### Risk 4: Developer Confusion
- **Mitigation**: Clear documentation in AI_GUIDE.md
- **Mitigation**: Add XML comments to new base classes
- **Mitigation**: Provide migration examples

---

## 📚 References

- AI_GUIDE.md - Guidelines for implementation
- DESIGN_DECISIONS.md - Architecture constraints
- CURRENT_ARCHITECTURE.md - System architecture

---

## 🚀 Next Steps

1. **Review this plan** with stakeholders
2. **Approve refactoring priority** and scope
3. **Create feature branch** for refactoring work
4. **Implement Phase 1** (BaseRepository - highest impact)
5. **Test and validate** Phase 1 thoroughly
6. **Proceed to Phase 2** after Phase 1 approval
7. **Merge to main** after all phases complete and tested

---

**Document Version:** 1.0
**Last Updated:** 2026-03-09
**Status:** Ready for Implementation
