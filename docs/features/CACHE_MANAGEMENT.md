# Cache Management System

> **Authoritative quick-ref for file-op safety: `.claude/knowledge/filesystem-operation-serialization.md`**
> (planner, per-mod queue, archive-patch — current). This doc is the deep expansion; where they disagree,
> the rule wins.

**Version:** 1.0
**Last Updated:** 2026-03-07
**Module:** Profile Configuration

---

## Overview

Automatic cache management system that intelligently cleans up old disabled mod caches on a per-category basis to save disk space while preserving recently used mods.

## Key Features

1. **Category-Specific Cleanup** - Only affects disabled caches within the same category as the mod being loaded/unloaded
2. **Unclassified Exclusion** - Unclassified mods (null/empty/whitespace category) are never cleaned up
3. **Configurable Limits** - Per-profile configuration with enable/disable toggle and max cache count (1-100, default: 10)
4. **Automatic Triggering** - Cleanup runs automatically after mod load/unload operations (fire-and-forget)
5. **Atomic Operations** - Uses FileOperationPlanner for safe, retryable deletions

## Architecture

### Configuration Model

**Location:** `D3dxSkinManager/Modules/Profile/Models/ProfileConfiguration.cs`

```csharp
public class CacheManagementConfiguration
{
    /// <summary>
    /// Enable automatic cleanup of old disabled caches
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of disabled caches to keep per category (default: 10)
    /// When exceeded, oldest caches (by LastWriteTime) are deleted automatically
    /// Valid range: 1-100
    /// </summary>
    public int MaxDisabledCaches { get; set; } = 10;
}
```

**Storage:** Persisted in `{profileId}/config.json`

### Service Implementation

**Location:** `D3dxSkinManager/Modules/Mod/Services/ModCacheService.cs`

```csharp
public interface IModCacheService
{
    /// <summary>
    /// Clean up old disabled caches for a specific category
    /// Only affects disabled caches of mods in the same category
    /// Unclassified mods (null/empty/whitespace category) are NOT cleaned up
    /// </summary>
    Task<int> CleanupOldDisabledCachesAsync(string? modCategory);
}
```

**Algorithm:**
1. Early exit if category is null/empty/whitespace (unclassified)
2. Check if feature is enabled in profile configuration
3. Get all mods in the specified category from database
4. Filter disabled cache directories to only those matching category mod IDs
5. Sort by LastWriteTime (most recent first)
6. Keep configured max count, delete oldest beyond limit
7. Use FileOperationPlanner for atomic deletion operations

### Integration Points

**Location:** `D3dxSkinManager/Modules/Mod/Services/ModLifecycleService.cs`

```csharp
public async Task<ModLoadResult> LoadAsync(string id)
{
    // ... load logic ...

    // Trigger cleanup (fire-and-forget, non-blocking)
    _ = Task.Run(async () =>
    {
        await _cacheService.CleanupOldDisabledCachesAsync(mod.Category);
    });
}

public async Task<bool> UnloadAsync(string id)
{
    var mod = await _repository.GetByIdAsync(id);
    var success = await _cacheService.DisableCacheAsync(id);

    if (success)
    {
        // Trigger cleanup (fire-and-forget, non-blocking)
        _ = Task.Run(async () =>
        {
            await _cacheService.CleanupOldDisabledCachesAsync(mod?.Category);
        });
    }
}
```

## User Interface

**Location:** `D3dxSkinManager.Client/src/modules/setting/components/SettingsView.tsx`

**Design Pattern:** Follows mod edit screen CompactSwitch design

```tsx
<Form.Item
  label="Cache Management"
  tooltip="Automatically manage disabled mod caches to save disk space"
>
  <Space style={{ alignItems: "center" }}>
    <CompactSwitch
      checkedChildren="Enable"
      unCheckedChildren="Disable"
    />
    <span>Keep Recent:</span>
    <InputNumber
      min={1}
      max={100}
      disabled={!enabled}
      style={{ width: "80px" }}
    />
    <span style={{ color: "var(--text-secondary)", fontSize: "12px" }}>
      cached mods
    </span>
  </Space>
</Form.Item>
```

**Layout:** `[Enable|Disable] Keep Recent: [10] cached mods`

## Behavior Examples

### Scenario 1: Category-Specific Cleanup

**Setup:**
- Profile max: 10 disabled caches
- Category "Character" has 15 disabled caches
- Category "Weapon" has 5 disabled caches

**Action:** Load/unload a "Character" mod

**Result:**
- ✅ Deletes 5 oldest "Character" caches (keeps recent 10)
- ❌ Does NOT touch "Weapon" caches (different category)

### Scenario 2: Unclassified Mods

**Setup:**
- Profile max: 10 disabled caches
- 20 unclassified disabled caches exist

**Action:** Load/unload an unclassified mod

**Result:**
- ✅ Skips cleanup entirely (logs: "Skipping cache cleanup for unclassified mod")
- ❌ All 20 unclassified caches remain untouched

### Scenario 3: Feature Disabled

**Setup:**
- Cache management disabled in settings
- Category has 50 disabled caches

**Action:** Load/unload any mod

**Result:**
- ✅ Cleanup skipped (logs: "Cache cleanup disabled in configuration")
- ❌ No caches deleted

## IPC Communication

**Backend Handler:** `ProfileFacade.UpdateProfileConfigAsync`

**Frontend Service:** `profileService.updateProfileConfig`

```typescript
await profileService.updateProfileConfig({
  profileId: selectedProfileId,
  cacheManagement: {
    enabled: true,
    maxDisabledCaches: 10
  }
});
```

**Event Emission:** `ProfileEvents.CONFIG_UPDATED` emitted on successful save

## File Operations

All cache deletions use **FileOperationPlanner** for:
- **Atomic operations** - No partial deletions
- **Automatic retry** - Handles transient IO errors (3 attempts with exponential backoff)
- **Conflict avoidance** - Queues operations to prevent collisions

```csharp
var deleteOp = new FileSystemOperation
{
    OperationType = FileSystemOperationType.DeleteDirectory,
    SourcePath = cachePath
};
await _operationPlanner.SubmitOperationAsync(deleteOp);
```

## State Management

**Frontend Store:** `settingsStore.ts`

```typescript
export interface SettingsState {
  // Cache Management
  cacheManagementEnabled: boolean;
  maxDisabledCaches: number;
  initialCacheManagementConfig: {
    enabled: boolean;
    maxDisabledCaches: number;
  };
}
```

**Change Tracking:** Integrated with existing `profileConfigChanged` flag for unified save/discard workflow

## Internationalization

**English Keys:** `D3dxSkinManager/Languages/en.json`
```json
{
  "common.enable": "Enable",
  "common.disable": "Disable",
  "settings.profile.cacheManagement.title": "Cache Management",
  "settings.profile.cacheManagement.tooltip": "Automatically manage disabled mod caches to save disk space",
  "settings.profile.cacheManagement.maxCaches": "Keep Recent:",
  "settings.profile.cacheManagement.hint": "cached mods"
}
```

**Chinese Keys:** `D3dxSkinManager/Languages/cn.json`
```json
{
  "common.enable": "启用",
  "common.disable": "禁用",
  "settings.profile.cacheManagement.title": "缓存管理",
  "settings.profile.cacheManagement.tooltip": "自动管理已禁用的模组缓存以节省磁盘空间",
  "settings.profile.cacheManagement.maxCaches": "保留最近：",
  "settings.profile.cacheManagement.hint": "个缓存模组"
}
```

## Logging

**Service:** ModCacheService

**Log Levels:**
- **Info:** Cache cleanup start/completion, deletions
- **Verbose:** Feature disabled, within limits, unclassified skipped
- **Warn:** Invalid configuration, deletion failures
- **Error:** Unexpected errors during cleanup

**Examples:**
```
[INFO] Cleaning up 5 old disabled cache(s) for category 'Character' (limit: 10, current: 15)
[INFO] Cleaned up old disabled cache: DISABLED-A1B2C3D4E5F6... (category: Character)
[INFO] Cache cleanup completed for category 'Character': 5 old cache(s) removed
[VERBOSE] Skipping cache cleanup for unclassified mod
[VERBOSE] Cache cleanup disabled in configuration
```

## Dependencies

**Backend:**
- `IProfileRepository` - Profile configuration retrieval
- `IModRepository` - Category mod lookups
- `IFileOperationPlanner` - Atomic deletion operations
- `IProfileContext` - Current profile ID

**Frontend:**
- `CompactSwitch` - UI component
- `InputNumber` (Ant Design) - Numeric input
- `profileService` - IPC communication
- `settingsStore` - State management

## Testing Considerations

1. **Category Isolation** - Verify cleanup only affects same category
2. **Unclassified Handling** - Confirm unclassified mods are skipped
3. **Edge Cases:**
   - Empty category (0 mods)
   - Single mod in category
   - Exactly at limit (no deletion)
   - Feature disabled
   - Invalid configuration values
4. **Concurrency** - Multiple rapid load/unload operations
5. **File System Errors** - IO failures, locked files

## Performance

- **Async Execution** - Fire-and-forget cleanup doesn't block user operations
- **Category Scoping** - Only queries/scans relevant mods, not entire cache
- **Debouncing** - Multiple rapid events within 20ms won't cause duplicate cleanups
- **Minimal Overhead** - Cleanup only runs on load/unload, not constantly

## Future Enhancements

- [ ] Manual cleanup trigger in UI
- [ ] Disk space savings statistics
- [ ] Configurable cleanup strategy (LRU vs oldest)
- [ ] Global cleanup across all categories option
- [ ] Cleanup scheduling (e.g., on app startup)

---

**Related Documentation:**
- [Profile System](./PROFILE_SYSTEM.md) - Profile configuration architecture
- [Category System](./CATEGORY_SYSTEM.md) - Category-based mod organization
- [Design Decisions](../core/DESIGN_DECISIONS.md) - Architectural patterns
