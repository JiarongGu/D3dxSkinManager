# Event-Based selectedMod Refresh Architecture

**Date:** 2026-03-07
**Type:** Architecture Improvement
**Modules:** Mod (Frontend)

## Overview

Refactored from **direct invocation** to **event-based reaction** for refreshing `selectedMod` in Zustand store. This provides a stateless, reactive architecture where `selectedMod` automatically updates when specific mod events occur, ensuring ModPreview always displays current enriched data.

## Problem Statement

### Before: Direct Invocation (Broken)

```typescript
// _refreshMods() tried to update selectedMod directly
async function _refreshMods(profileId: string): Promise<void> {
  if (selectedCategory) {
    await loadModsByCategory(profileId, selectedCategory.id);
  } else if (selectedMod?.sha) {  // ❌ This prevented proper updates!
    const freshMod = await modService.getModBySha(profileId, selectedMod.sha);
    setSelectedMod(freshMod);
    return; // Early return prevented category path from updating selectedMod
  }

  // Lines 52-66: UNREACHABLE when category selected + mod selected
  if (selectedMod?.sha) {
    const updatedMod = mods.find(m => m.sha === selectedMod.sha);
    setSelectedMod(updatedMod);  // Never executed!
  }
}
```

**Issues:**
1. ❌ `else if` logic made lines 52-66 unreachable in most cases
2. ❌ Mixed responsibilities: list refresh + selection refresh in one function
3. ❌ `selectedMod` wasn't refreshed when user loads/unloads mods in ModPreview
4. ❌ Cache/preview folder buttons in ModPreview showed as disabled (stale data)

### After: Event-Based Reaction (Fixed)

```typescript
// 1. Dedicated function for selectedMod refresh
export async function refreshSelectedMod(profileId: string): Promise<void> {
  const { selectedMod, setSelectedMod } = useModsStore.getState();
  if (!selectedMod?.sha) return;

  const freshMod = await modService.getModBySha(profileId, selectedMod.sha);
  if (freshMod) {
    setSelectedMod(freshMod);  // ✅ Updates with enriched data
  }
}

// 2. ModProvider subscribes to specific events
const handleSelectedModUpdate = useCallback(
  debounce(() => {
    if (!selectedProfileId) return;
    void modOps.refreshSelectedMod(selectedProfileId);
  }, 20),  // Debounce for deduplication
  [selectedProfileId]
);

// 3. Subscribe to targeted events
eventBus.subscribe(Module.MOD, ModEventType.LOADED, handleSelectedModUpdate);
eventBus.subscribe(Module.MOD, ModEventType.UNLOADED, handleSelectedModUpdate);
eventBus.subscribe(Module.MOD, ModEventType.METADATA_UPDATED, handleSelectedModUpdate);
eventBus.subscribe(Module.MOD, ModEventType.CACHE_CHANGED, handleSelectedModUpdate);
```

## Architecture Changes

### 1. Separation of Concerns

| Function | Responsibility | Triggers |
|----------|---------------|----------|
| `refreshMods()` | Refresh category mod list | `MOD_LIST_UPDATED` event |
| `refreshSelectedMod()` | Refresh selected mod enrichment | `LOADED`, `UNLOADED`, `METADATA_UPDATED`, `CACHE_CHANGED` |

### 2. Event Subscription Strategy

**ModProvider Event Subscriptions:**

```typescript
// Broad event: Refresh mod list
MOD_LIST_UPDATED → refreshMods() + loadStatistics()
  ↳ Triggers: LOADED, UNLOADED, DELETED, IMPORTED, METADATA_UPDATED, CATEGORY_UPDATED, CACHE_CHANGED
  ↳ Debounced: 20ms (prevents rapid-fire events)

// Specific events: Refresh selected mod only
LOADED → refreshSelectedMod()
UNLOADED → refreshSelectedMod()
METADATA_UPDATED → refreshSelectedMod()
CACHE_CHANGED → refreshSelectedMod()
  ↳ Debounced: 20ms (deduplication when multiple events fire simultaneously)
```

### 3. Debouncing Strategy

**All debouncing centralized in ModProvider** (removed from operations):

```typescript
// Before: Scattered debouncing
export const refreshMods = debounce(_refreshMods, 20);  // In modOperations.ts
export const loadModsByCategory = debounce(_loadModsByCategory, 20);  // In categoryOperations.ts

// After: Centralized in ModProvider
const handleModListUpdate = useCallback(
  debounce(() => void modOps.refreshMods(profileId), 20),
  [selectedProfileId]
);

const handleSelectedModUpdate = useCallback(
  debounce(() => void modOps.refreshSelectedMod(profileId), 20),
  [selectedProfileId]
);
```

**Benefits:**
- ✅ Single source of truth for debounce timing
- ✅ Easier to adjust debounce values globally
- ✅ Operations are pure functions (no hidden timing logic)

## Implementation Details

### Files Modified

#### Frontend

**1. `operations/modOperations.ts`**
- ✅ Created `refreshSelectedMod()` function
- ✅ Simplified `refreshMods()` - removed `selectedMod` update logic
- ✅ Removed debouncing (moved to ModProvider)

**2. `operations/categoryOperations.ts`**
- ✅ Removed debouncing from `refreshCategoryTree()`
- ✅ Removed debouncing from `loadModsByCategory()`

**3. `ModProvider.tsx`**
- ✅ Added `handleSelectedModUpdate` handler with 20ms debounce
- ✅ Subscribed to `LOADED`, `UNLOADED`, `METADATA_UPDATED`, `CACHE_CHANGED` events
- ✅ Added cleanup for debounced handler

**4. `ModListPanel/ModList.tsx`**
- ✅ Removed deprecated `checkedPaths` state
- ✅ Removed `checkFilePaths` IPC call
- ✅ Unified folder operations: `mod.hasCache`/`mod.cachePath`, `mod.hasPreviewFolder`/`mod.previewFolderPath`

#### Backend

**5. `Modules/Mod/Services/ModEnrichmentService.cs`**
- ✅ Added `IModCacheService` dependency
- ✅ Fixed `CachePath` resolution: `_cacheService.GetCachePath(mod.SHA)`
- ✅ Now handles both active (`{SHA}`) and disabled (`DISABLED-{SHA}`) cache directories

## Event Flow

### Scenario: User Loads Mod in ModList

```
1. User clicks "Load" → modService.loadMod()
2. Backend emits events:
   - MOD:LOADED (5ms)
   - MOD_LIST_UPDATED (8ms)
   - CACHE_CHANGED (10ms)

3. ModProvider receives events:
   - MOD:LOADED → handleSelectedModUpdate (debounced 20ms)
   - MOD:LOADED also consolidates to MOD_LIST_UPDATED → handleModListUpdate (debounced 20ms)
   - CACHE_CHANGED → handleSelectedModUpdate (debounced 20ms)

4. After 20ms debounce:
   - refreshSelectedMod() executes ONCE (deduplicated LOADED + CACHE_CHANGED)
   - refreshMods() executes ONCE

5. Result:
   - ModList updates with new mod list ✅
   - ModPreview selectedMod refreshes with hasCache=true, cachePath=... ✅
   - Folder buttons in ModPreview enabled ✅
```

### Scenario: Rapid Load/Unload (Event Storm)

```
1. User rapidly loads then unloads mod (< 20ms apart)
2. Backend emits:
   - MOD:LOADED (0ms)
   - CACHE_CHANGED (5ms)
   - MOD:UNLOADED (15ms)
   - CACHE_CHANGED (18ms)

3. Debounce deduplicates:
   - All 4 events batched → refreshSelectedMod() executes ONCE at 38ms ✅
   - Prevents 4 separate IPC calls to getModBySha()
```

## Benefits

### 1. Stateless Reactivity
- **Before:** Component logic tracked when to refresh (stateful, error-prone)
- **After:** Events drive refreshes automatically (stateless, reliable)

### 2. Performance
- **Before:** Multiple IPC calls on rapid events
- **After:** Debouncing deduplicates to single call per 20ms window

### 3. Maintainability
- **Before:** Complex conditional logic in `_refreshMods()` with unreachable code
- **After:** Two simple, focused functions with clear responsibilities

### 4. Correctness
- **Before:** ModPreview showed stale data (disabled folder buttons when they should be enabled)
- **After:** Always shows current enriched data from backend

## Migration Guide

### For Future Features

**When adding new mod operations:**

1. **Backend emits event** after successful operation:
```csharp
public async Task<bool> DoSomethingAsync(string sha) {
    // Operation logic...
    await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.SOMETHING_CHANGED, new { sha });
    return true;
}
```

2. **If operation affects mod list** - Backend event handler consolidates to `MOD_LIST_UPDATED`:
```csharp
// In ModListEventHandler.cs
eventBus.Subscribe(ModuleNames.MOD, ModEvents.SOMETHING_CHANGED,
    async (_) => await EmitModListUpdated("SOMETHING_CHANGED"));
```

3. **If operation affects selectedMod enrichment** - Add subscription in ModProvider:
```typescript
const unsubscribeSomethingChanged = eventBus.subscribe(
  Module.MOD,
  ModEventType.SOMETHING_CHANGED,
  handleSelectedModUpdate  // Reuse existing handler!
);
```

### When NOT to Use This Pattern

**Don't subscribe to `MOD_LIST_UPDATED` for selectedMod refresh:**
- ❌ Too broad - fires for ALL mod changes (imports, deletes, bulk operations)
- ❌ Wasteful - refreshes selectedMod even when unrelated mods change
- ✅ Use specific events instead (LOADED, UNLOADED, etc.)

## Testing Checklist

After implementing this architecture:

- [ ] ModList: Cache folder button enabled when mod has cache ✅
- [ ] ModList: Preview folder button enabled when mod has previews ✅
- [ ] ModPreview: Cache folder button enabled when mod has cache ✅
- [ ] ModPreview: Preview folder button enabled when mod has previews ✅
- [ ] Load mod → selectedMod refreshes with `isLoaded=true` ✅
- [ ] Unload mod → selectedMod refreshes with `isLoaded=false` ✅
- [ ] Rapid load/unload → Only one refresh (debounced) ✅
- [ ] Disabled cache (`DISABLED-{SHA}`) → Cache folder opens correctly ✅

## Related Documentation

- `docs/architecture/EVENT_HUB_ARCHITECTURE.md` - Event system overview
- `docs/AI_GUIDE.md` - Event-driven architecture patterns (Section: Event-Driven Architecture)
- `docs/features/PROFILE_AWARE_EVENTS.md` - Profile-scoped events

## Summary

**Key Takeaway:** Move from **direct invocation** (calling functions when you think data is stale) to **event-based reaction** (backend tells frontend when data changes). This is the foundation of a scalable, maintainable event-driven architecture.

**Pattern:**
```
Backend Operation → Emit Event → Frontend Reacts → Update Store → UI Reflects Change
```

This pattern ensures UI state is always synchronized with backend state without manual refresh logic scattered throughout components.
