---
name: file-watcher
description: Generate FileSystemWatcher service for monitoring directory changes and emitting events for cache invalidation
---

# FileSystemWatcher Service Generator

Generate a complete FileSystemWatcher service that monitors a directory and emits events when files/folders change.

**Purpose**: Watching file system changes allows automatic cache invalidation. This skill generates the complete pattern with proper synchronization, disposal, and fire-and-forget event emission.

## Arguments

**Format**: `/file-watcher <WatcherName> <Module> <DirectoryPath> <NotifyFilters> <EventsToEmit>`

**Example**:
```
/file-watcher ModCacheWatcher Mod cache/Mods DirectoryName CACHE_CHANGED
/file-watcher ProfileWatcher Profile profiles FileName,DirectoryName PROFILE_CHANGED
```

**Parameters**:
- `WatcherName` - Name ending with "Watcher" (e.g., ModCacheWatcher)
- `Module` - Module name (Mod, Profile, Workflow)
- `DirectoryPath` - Relative path to watch (e.g., cache/Mods, profiles)
- `NotifyFilters` - Comma-separated filters: FileName, DirectoryName, Size, LastWrite
- `EventsToEmit` - Comma-separated event names to emit on changes

## What This Skill Generates

1. **Interface file** (`I{WatcherName}.cs`)
   - StartWatching() method
   - StopWatching() method
   - IDisposable interface

2. **Implementation file** (`{WatcherName}.cs`)
   - FileSystemWatcher initialization
   - Event handlers (Created, Deleted, Renamed, Changed)
   - Lock synchronization
   - Fire-and-forget event emission
   - Proper disposal

3. **Service registration** (updates `{Module}ServiceExtensions.cs`)
   - Singleton registration

## Pattern to Follow

```csharp
// 1. Interface (I{WatcherName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Services;

/// <summary>
/// File system watcher for {directory description}
/// </summary>
public interface I{WatcherName} : IDisposable
{
    /// <summary>
    /// Start watching for file system changes
    /// </summary>
    void StartWatching();

    /// <summary>
    /// Stop watching for file system changes
    /// </summary>
    void StopWatching();
}

// 2. Implementation ({WatcherName}.cs)
namespace D3dxSkinManager.Modules.{Module}.Services;

/// <summary>
/// Monitors {directory} for changes and emits cache invalidation events
/// </summary>
public class {WatcherName} : I{WatcherName}
{
    private readonly IProfileContext _profileContext;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public {WatcherName}(
        IProfileContext profileContext,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _profileContext = profileContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void StartWatching()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                _logger.Verbose("Watcher already started");
                return;
            }

            var directoryPath = Path.Combine(_profileContext.ProfilePath, "{DirectoryPath}");

            if (!Directory.Exists(directoryPath))
            {
                _logger.Warn($"Directory does not exist: {directoryPath}");
                return;
            }

            _watcher = new FileSystemWatcher(directoryPath)
            {
                NotifyFilter = NotifyFilters.{NotifyFilters},
                IncludeSubdirectories = {IncludeSubdirectories},
                EnableRaisingEvents = true
            };

            // Subscribe to events
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Changed += OnChanged;

            _logger.Info($"Started watching: {directoryPath}");
        }
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            if (_watcher == null) return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
            _watcher = null;

            _logger.Info("Stopped watching");
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Verbose($"File system change detected: {e.ChangeType} - {e.Name}");

        // Fire-and-forget pattern (don't block FileSystemWatcher thread)
        _ = Task.Run(async () =>
        {
            try
            {
                await _eventBus.EmitAsync(
                    ModuleNames.{MODULE},
                    {Module}Events.{EVENT_NAME},
                    new
                    {
                        ChangeType = e.ChangeType.ToString(),
                        Path = e.FullPath,
                        Name = e.Name
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to emit event: {ex.Message}");
            }
        });
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.Verbose($"File system renamed: {e.OldName} → {e.Name}");

        _ = Task.Run(async () =>
        {
            try
            {
                await _eventBus.EmitAsync(
                    ModuleNames.{MODULE},
                    {Module}Events.{EVENT_NAME},
                    new
                    {
                        ChangeType = "Renamed",
                        OldPath = e.OldFullPath,
                        NewPath = e.FullPath,
                        OldName = e.OldName,
                        NewName = e.Name
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to emit event: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        StopWatching();
    }
}
```

## Key Patterns

### 1. Lock Synchronization

**Why**: Prevents race conditions when starting/stopping watcher.

```csharp
private readonly object _lock = new();

public void StartWatching()
{
    lock (_lock)  // Ensure thread-safe access
    {
        if (_watcher != null) return;  // Already started
        // ... create watcher
    }
}
```

### 2. Fire-and-Forget Event Emission

**Why**: FileSystemWatcher events run on thread pool threads. Don't block them with async operations.

```csharp
private void OnChanged(object sender, FileSystemEventArgs e)
{
    // ✅ CORRECT - Fire-and-forget with Task.Run
    _ = Task.Run(async () =>
    {
        await _eventBus.EmitAsync(...);
    });
}

// ❌ WRONG - Would block FileSystemWatcher thread
private async void OnChanged(object sender, FileSystemEventArgs e)
{
    await _eventBus.EmitAsync(...);  // Blocks!
}
```

### 3. Proper Disposal

**Why**: FileSystemWatcher holds OS resources. Must be disposed properly.

```csharp
public void Dispose()
{
    StopWatching();  // Cleanup watcher
}

public void StopWatching()
{
    lock (_lock)
    {
        if (_watcher == null) return;

        _watcher.EnableRaisingEvents = false;  // Stop watching
        _watcher.Created -= OnChanged;         // Unsubscribe events
        _watcher.Deleted -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Changed -= OnChanged;
        _watcher.Dispose();                    // Dispose
        _watcher = null;                       // Clear reference
    }
}
```

### 4. Event Emission Pattern

Events include change metadata:

```csharp
await _eventBus.EmitAsync(
    ModuleNames.MOD,
    ModEvents.CACHE_CHANGED,
    new
    {
        ChangeType = e.ChangeType.ToString(),  // Created, Deleted, Changed
        Path = e.FullPath,                     // Full path to changed item
        Name = e.Name                          // File/folder name
    }
);
```

## NotifyFilter Options

Common combinations:

```csharp
// Monitor directory creation/deletion only
NotifyFilter = NotifyFilters.DirectoryName

// Monitor file creation/deletion/modification
NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite

// Monitor file size changes
NotifyFilter = NotifyFilters.Size

// Monitor all changes
NotifyFilter = NotifyFilters.FileName |
               NotifyFilters.DirectoryName |
               NotifyFilters.Size |
               NotifyFilters.LastWrite
```

## IncludeSubdirectories

```csharp
// Watch only top-level directory
IncludeSubdirectories = false

// Watch all subdirectories recursively
IncludeSubdirectories = true
```

## Steps to Execute

1. **Find or create Services directory**:
   - Path: `Modules/{Module}/Services/`

2. **Create interface file**:
   - Path: `Modules/{Module}/Services/I{WatcherName}.cs`
   - StartWatching() and StopWatching() methods
   - Implement IDisposable

3. **Create implementation file**:
   - Path: `Modules/{Module}/Services/{WatcherName}.cs`
   - Inject IProfileContext, IProfileEventBus, ILogHelper
   - Initialize FileSystemWatcher in StartWatching()
   - Subscribe to events (Created, Deleted, Renamed, Changed)
   - Use fire-and-forget pattern for event emission
   - Implement proper disposal

4. **Register service**:
   - Find `Modules/{Module}/{Module}ServiceExtensions.cs`
   - Add: `services.AddSingleton<I{WatcherName}, {WatcherName}>();`

5. **Add auto-start** (if watcher should start immediately):
   - In module initialization, call `_watcher.StartWatching()`

## Example Output

For: `/file-watcher ModCacheWatcher Mod cache/Mods DirectoryName CACHE_CHANGED`

**Interface** (`Modules/Mod/Services/IModCacheWatcher.cs`):
```csharp
namespace D3dxSkinManager.Modules.Mod.Services;

public interface IModCacheWatcher : IDisposable
{
    void StartWatching();
    void StopWatching();
}
```

**Implementation** (`Modules/Mod/Services/ModCacheWatcher.cs`):
```csharp
namespace D3dxSkinManager.Modules.Mod.Services;

public class ModCacheWatcher : IModCacheWatcher
{
    private readonly IProfileContext _profileContext;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public ModCacheWatcher(
        IProfileContext profileContext,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _profileContext = profileContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void StartWatching()
    {
        lock (_lock)
        {
            if (_watcher != null) return;

            var cachePath = Path.Combine(_profileContext.ProfilePath, "cache/Mods");
            if (!Directory.Exists(cachePath)) return;

            _watcher = new FileSystemWatcher(cachePath)
            {
                NotifyFilter = NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnCacheChanged;
            _watcher.Deleted += OnCacheChanged;
            _watcher.Renamed += OnCacheRenamed;

            _logger.Info($"Started watching mod cache: {cachePath}");
        }
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            if (_watcher == null) return;

            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCacheChanged;
            _watcher.Deleted -= OnCacheChanged;
            _watcher.Renamed -= OnCacheRenamed;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnCacheChanged(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await _eventBus.EmitAsync(
                ModuleNames.MOD,
                ModEvents.CACHE_CHANGED,
                new { ChangeType = e.ChangeType.ToString(), Name = e.Name }
            );
        });
    }

    private void OnCacheRenamed(object sender, RenamedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await _eventBus.EmitAsync(
                ModuleNames.MOD,
                ModEvents.CACHE_CHANGED,
                new { ChangeType = "Renamed", OldName = e.OldName, NewName = e.Name }
            );
        });
    }

    public void Dispose() => StopWatching();
}
```

**Registration** (`Modules/Mod/ModServiceExtensions.cs`):
```csharp
services.AddSingleton<IModCacheWatcher, ModCacheWatcher>();
```

## Integration with Cache Invalidation

FileSystemWatcher typically triggers cache invalidation:

```csharp
// In service that uses IMemoryCache
public class ModQueryService
{
    public ModQueryService(
        IMemoryCache cache,
        IProfileContext profileContext,
        IProfileEventBus eventBus)
    {
        _cache = cache;
        _cacheKey = $"ActiveMods_{profileContext.ProfileId}";

        // Subscribe to file watcher events → invalidate cache
        eventBus.Subscribe(ModuleNames.MOD, ModEvents.CACHE_CHANGED, _ =>
        {
            _cache.Remove(_cacheKey);  // Invalidate on file system change
            return Task.CompletedTask;
        });
    }
}
```

## Important Rules

- ✅ Use lock synchronization for Start/Stop
- ✅ Use fire-and-forget pattern (Task.Run) for event emission
- ✅ Check directory exists before creating watcher
- ✅ Implement proper disposal (unsubscribe + dispose + null)
- ✅ Log at Verbose level (file system events are noisy)
- ✅ Include change metadata in events
- ❌ Don't block FileSystemWatcher thread (no async void handlers)
- ❌ Don't forget to unsubscribe events in disposal
- ❌ Don't watch too many files (performance impact)

## Common Use Cases

1. **Cache Invalidation** (most common):
   ```
   /file-watcher ModCacheWatcher Mod cache/Mods DirectoryName CACHE_CHANGED
   ```

2. **Profile Changes**:
   ```
   /file-watcher ProfileConfigWatcher Profile profiles FileName PROFILE_CONFIG_CHANGED
   ```

3. **Asset Monitoring**:
   ```
   /file-watcher AssetWatcher Mod assets FileName,Size ASSET_CHANGED
   ```

## Reference Examples

See existing watchers:
- `Modules/Mod/Services/ModCacheWatcher.cs` - Monitors mod cache directory
- Directory creation/deletion events trigger cache refresh

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial file watcher skill

**How to update this skill**:
1. If fire-and-forget pattern changes, update event emission
2. If synchronization strategy changes, update lock pattern
3. If disposal pattern changes, update StopWatching/Dispose
4. Update reference examples as better implementations emerge
