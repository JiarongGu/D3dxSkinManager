# Delayed Loading UX Pattern

**Last Updated:** 2026-02-20
**Status:** ✅ Active Pattern

## Overview

The Delayed Loading pattern eliminates UI flicker for fast operations while providing user feedback for slow operations. It's the primary loading pattern for all data fetching in D3dxSkinManager.

---

## The Problem

Traditional loading patterns have two extremes:

**Option 1: Always Show Loading**
```typescript
setLoading(true);
const data = await fetchData();
setLoading(false);
```
❌ **Problem**: Fast operations (<100ms) cause visible flicker
❌ **Bad UX**: Spinner appears/disappears too quickly, feels janky

**Option 2: Never Show Loading**
```typescript
const data = await fetchData();
updateUI(data);
```
❌ **Problem**: Slow operations leave users wondering if anything is happening
❌ **Bad UX**: App feels unresponsive on slow networks

---

## The Solution: Delayed Loading

Only show loading spinner if the operation takes longer than a threshold (default 100ms):

```typescript
const { loading, execute } = useDelayedLoading(200);

const loadData = async () => {
  await execute(async () => {
    const data = await fetchData();
    updateUI(data);
  });
};
```

✅ **Fast operations (<100ms)**: No spinner, feels instant
✅ **Slow operations (>100ms)**: Spinner appears, user gets feedback
✅ **Best of both worlds**: Smooth for fast, informative for slow

---

## How It Works

```
User Action
    ↓
Start Timer (100ms)
    ↓
Operation Begins
    ↓
    ├─ [If completes <100ms]
    │       ↓
    │   Clear timer
    │   No loading shown ✨
    │       ↓
    │   UI updates
    │
    └─ [If takes >100ms]
            ↓
        Timer fires → loading: true
            ↓
        Spinner appears 🔄
            ↓
        Operation completes
            ↓
        loading: false
            ↓
        Spinner disappears
            ↓
        UI updates
```

---

## Implementation Guide

### 1. Create the Hook Instance

```typescript
import { useDelayedLoading } from '../../../shared/hooks/useDelayedLoading';

const { loading, execute } = useDelayedLoading(100); // 100ms threshold
```

**Threshold Guidelines:**
- **UI interactions**: 50ms (dialogs, modals)
- **Data loading**: 100ms (fetching lists, trees)
- **Heavy operations**: 200ms (complex calculations)

### 2. Sync with Reducer

```typescript
useEffect(() => {
  dispatch({ type: "SET_LOADING", payload: loading });
}, [loading]);
```

**Why?** The hook manages its own internal loading state. This syncs it with your reducer so components can access it via context.

### 3. Wrap Operations

```typescript
const loadData = useCallback(async (profileId: string) => {
  await execute(async () => {
    const data = await fetchData(profileId);
    dispatch({ type: "SET_DATA", payload: data });
  });
}, [execute]);
```

**Important:** The actual data fetching and state updates happen INSIDE the `execute()` callback.

### 4. Remove Automatic Loading Reset

In your reducer:

```typescript
// ❌ DON'T DO THIS
case "SET_DATA":
  return { ...state, data: action.payload, loading: false };

// ✅ DO THIS
case "SET_DATA":
  return { ...state, data: action.payload };
  // Don't set loading: false - let useDelayedLoading control it
```

**Why?** If the reducer sets `loading: false`, it will override the hook's state management.

---

## Complete Example

### Hook Implementation

```typescript
// hooks/useMyData.ts
import { useReducer, useCallback, useEffect } from "react";
import { useDelayedLoading } from "../../../shared/hooks/useDelayedLoading";
import { myService } from "../services/myService";
import { myReducer, initialState } from "../reducers/myReducer";

export function useMyData() {
  const [state, dispatch] = useReducer(myReducer, initialState);

  // 1. Create hook instance
  const { loading, execute } = useDelayedLoading(200);

  // 2. Sync with reducer
  useEffect(() => {
    dispatch({ type: "SET_LOADING", payload: loading });
  }, [loading]);

  // 3. Wrap operations
  const loadData = useCallback(async (id: string) => {
    await execute(async () => {
      const data = await myService.getData(id);
      dispatch({ type: "SET_DATA", payload: data });
    });
  }, [execute]);

  return {
    state,
    dispatch,
    loadData
  };
}
```

### Reducer Implementation

```typescript
// reducers/myReducer.ts
export type MyAction =
  | { type: "SET_DATA"; payload: MyData[] }
  | { type: "SET_LOADING"; payload: boolean }
  | { type: "SET_ERROR"; payload: string | null };

export function myReducer(state: MyState, action: MyAction): MyState {
  switch (action.type) {
    case "SET_DATA":
      // 4. Don't set loading: false
      return { ...state, data: action.payload, error: null };

    case "SET_LOADING":
      return { ...state, loading: action.payload };

    case "SET_ERROR":
      return { ...state, error: action.payload };

    default:
      return state;
  }
}
```

---

## Real-World Examples

### Example 1: Classification Tree Loading

**File:** `modules/mods/context/hooks/useClassificationData.ts`

```typescript
const { loading, execute } = useDelayedLoading(200);

useEffect(() => {
  dispatch({ type: "SET_CLASSIFICATION_LOADING", payload: loading });
}, [loading]);

const loadClassificationTree = useCallback(async (profileId: string) => {
  await execute(async () => {
    const tree = await classificationService.getClassificationTree(profileId);
    dispatch({ type: "SET_CLASSIFICATION_TREE", payload: tree });
  });
}, [execute]);
```

**Result:** Tree loads instantly if cached, shows spinner only if slow.

### Example 2: Mod List Loading

**File:** (historical example — the pattern now lives in `shared/hooks/useDelayedLoading.ts` consumers, e.g. the shared dialogs)

```typescript
const { loading, execute } = useDelayedLoading(200);

useEffect(() => {
  dispatch({ type: "SET_LOADING", payload: loading });
}, [loading]);

const loadMods = useCallback(async (profileId: string) => {
  await execute(async () => {
    const mods = await modService.getAllMods(profileId);
    dispatch({ type: "SET_MODS", payload: mods });
  });
}, [execute]);
```

**Result:** Fast mod list loads feel instant, slow ones show feedback.

### Example 3: Confirm Dialog

**File:** `shared/components/dialogs/ConfirmDialog.tsx`

```typescript
const { loading, execute, reset } = useDelayedLoading(50); // Faster threshold for UI

useEffect(() => {
  if (!visible) reset(); // Reset when dialog closes
}, [visible, reset]);

const handleOk = async () => {
  await execute(async () => {
    await onOk();
  });
};

return (
  <Modal
    okButtonProps={{ loading }} // Bind to button
    onOk={handleOk}
  />
);
```

**Result:** Fast confirmations don't flash loading state, slow ones disable button.

---

## Timing Thresholds Reference

| Operation Type | Threshold | Rationale |
|----------------|-----------|-----------|
| **Dialogs/Modals** | 50ms | UI interactions should feel instant |
| **Data Fetching** | 100ms | Balance between flicker and feedback |
| **Tree Operations** | 100ms | Drag-drop should feel smooth |
| **Heavy Computation** | 200ms | Users expect these to take time |
| **File Operations** | 200ms | Disk I/O is inherently slower |

**Rule of Thumb:** Use 100ms unless you have a specific reason to change it.

---

## Common Patterns

### Pattern 1: Error Handling

```typescript
const loadData = useCallback(async (id: string) => {
  try {
    await execute(async () => {
      const data = await myService.getData(id);
      dispatch({ type: "SET_DATA", payload: data });
    });
  } catch (error) {
    // Handle "Operation already in progress" gracefully
    if (error instanceof Error && error.message === 'Operation already in progress') {
      return; // Ignore duplicate calls
    }

    // Handle real errors
    dispatch({ type: "SET_ERROR", payload: error.message });
  }
}, [execute]);
```

### Pattern 2: Multiple Operations

```typescript
const loadAllData = useCallback(async (id: string) => {
  await execute(async () => {
    // Run operations in parallel
    const [users, posts, comments] = await Promise.all([
      fetchUsers(id),
      fetchPosts(id),
      fetchComments(id)
    ]);

    dispatch({ type: "SET_ALL_DATA", payload: { users, posts, comments } });
  });
}, [execute]);
```

### Pattern 3: Dependent Operations

```typescript
const loadWithDependencies = useCallback(async (id: string) => {
  await execute(async () => {
    // First operation
    const user = await fetchUser(id);
    dispatch({ type: "SET_USER", payload: user });

    // Second operation (depends on first)
    const posts = await fetchUserPosts(user.id);
    dispatch({ type: "SET_POSTS", payload: posts });
  });
}, [execute]);
```

---

## Migration from useOptimisticUpdate

### Before (Complex Verification)

```typescript
const { verify } = useOptimisticUpdate({
  fetchFn: async (id) => await fetchData(id),
  onMismatch: () => {
    // Complex mismatch handling
    refreshData();
  },
  verificationDelay: 50,
  normalizeForComparison: (data) => sortById(data)
});

const updateData = async () => {
  setOptimisticState(newState);
  await saveData();
  verify(expectedState, id); // Complex verification
};
```

### After (Simple Delayed Loading)

```typescript
const { loading, execute } = useDelayedLoading(200);

const updateData = async () => {
  await execute(async () => {
    await saveData();
    const freshData = await fetchData(); // Just refresh from backend
    setState(freshData);
  });
};
```

**Benefits:**
- **~50% less code** - No verification logic needed
- **Simpler** - Just fetch and update, trust backend
- **Same UX** - Still no flicker for fast operations

---

## When NOT to Use Delayed Loading

### Use `useOptimisticUpdate` Instead When:

1. **Complex State Verification Needed**
   - Multiple interdependent fields to verify
   - Need to detect specific mismatches
   - Want automatic rollback on mismatch

2. **Unpredictable Network Delays**
   - Operations might take 50ms or 5 seconds
   - Need to show optimistic UI immediately
   - Verification is critical for data integrity

3. **Multi-Point Updates**
   - Update affects multiple data sources
   - Need to verify each independently
   - Selective refresh based on what mismatched

### Use Plain Loading State When:

1. **Always Slow Operations**
   - File uploads, downloads
   - Video processing
   - Batch operations

2. **Progress Tracking Needed**
   - Need to show progress %
   - Multi-step operations
   - User needs real-time status

---

## Testing

### Unit Tests

```typescript
describe('useDelayedLoading', () => {
  it('should not show loading for fast operations', async () => {
    const { loading, execute } = useDelayedLoading(200);

    const promise = execute(async () => {
      await sleep(50); // Fast operation
    });

    expect(loading).toBe(false); // Should still be false
    await promise;
    expect(loading).toBe(false); // Should remain false
  });

  it('should show loading for slow operations', async () => {
    const { loading, execute } = useDelayedLoading(200);

    const promise = execute(async () => {
      await sleep(150); // Slow operation
    });

    await sleep(110); // Wait past threshold
    expect(loading).toBe(true); // Should be true now

    await promise;
    expect(loading).toBe(false); // Should be false after completion
  });
});
```

### Manual Testing Checklist

- [ ] Fast operations (<100ms) don't show spinner
- [ ] Slow operations (>100ms) show spinner
- [ ] Spinner appears/disappears smoothly
- [ ] No flicker during rapid operations
- [ ] Error states clear loading correctly
- [ ] Multiple concurrent calls handled gracefully

---

## Performance Considerations

### Memory

✅ **Low overhead** - Only maintains loading state and one timeout per hook instance
✅ **Cleanup** - Timeouts always cleared in finally block
✅ **No memory leaks** - `useRef` prevents stale closures

### Timing

✅ **Precise** - Uses native `setTimeout` for accurate timing
✅ **No drift** - Each operation gets fresh timer
✅ **Cancellable** - Can reset/cancel via `reset()` function

### Concurrency

✅ **Protected** - `isProcessingRef` prevents race conditions
✅ **Explicit** - Throws error if operation already in progress
✅ **Predictable** - One operation at a time per hook instance

---

## Troubleshooting

### Issue: Loading State Stuck at True

**Cause:** Operation threw error before finally block executed

**Solution:** Ensure execute is wrapped in try/catch at call site

```typescript
try {
  await execute(async () => { /* ... */ });
} catch (error) {
  // Handle error - loading already reset by hook
}
```

### Issue: Spinner Flashes Too Quickly

**Cause:** Threshold too low for this operation

**Solution:** Increase threshold

```typescript
const { loading, execute } = useDelayedLoading(200); // Higher threshold
```

### Issue: No Loading State Ever Shown

**Cause:** Not syncing hook's loading state with reducer

**Solution:** Add useEffect sync

```typescript
useEffect(() => {
  dispatch({ type: "SET_LOADING", payload: loading });
}, [loading]);
```

---

## Related Documentation

- [useDelayedLoading.ts](../../D3dxSkinManager.Client/src/shared/hooks/useDelayedLoading.ts) - Hook implementation
- [useOptimisticUpdate.ts](../../D3dxSkinManager.Client/src/shared/hooks/useOptimisticUpdate.ts) - Alternative for complex verification
- [AI_GUIDE.md](../AI_GUIDE.md) - Mandatory session rules and coding patterns

---

## Summary

The Delayed Loading pattern provides the best UX for loading operations:

✅ **Fast = Instant** - No spinner for operations <100ms
✅ **Slow = Feedback** - Spinner appears for operations >100ms
✅ **Simple** - Easy to implement and understand
✅ **Consistent** - Same pattern across entire codebase
✅ **Performant** - Minimal overhead, proper cleanup

**Use it by default for all data loading operations.**
