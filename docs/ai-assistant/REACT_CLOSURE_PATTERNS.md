# React Closure Patterns and Solutions

**Last Updated:** 2026-02-21
**Status:** ✅ Production Pattern

**TL;DR:** Use `useStableRef` from `src/shared/hooks/useStableRef.ts` to access current values in callbacks without adding dependencies. Supports single or multiple values.

---

## Table of Contents

1. [The Problem](#the-problem)
2. [Why It Happens](#why-it-happens)
3. [The Solution: useStableRef](#the-solution-usestableref)
4. [Real-World Example](#real-world-example)
5. [When to Use](#when-to-use)
6. [Anti-Patterns](#anti-patterns)

---

## The Problem

### Symptom

Your React callbacks access stale data even though you've added dependencies to `useCallback`:

```tsx
function MyComponent({ items }: { items: Item[] }) {
  // items starts as []
  // Later, items updates to [item1, item2, item3]

  const handleClick = useCallback(() => {
    console.log(items.length); // Logs 0, even though items has 3 items!
  }, [items]); // items IS in dependency array!

  // This callback is passed to a third-party library or stored in a ref
  useEffect(() => {
    thirdPartyLib.registerHandler(handleClick);
  }, []);

  return <div>Items: {items.length}</div>; // Shows 3 correctly
}
```

### Root Cause

When `handleClick` is passed to `thirdPartyLib.registerHandler` in the `useEffect` with empty deps `[]`, it captures the **initial** version of `handleClick` (when `items` was empty). Even though `handleClick` gets recreated when `items` changes, the third-party library is still calling the **old** callback.

---

## Why It Happens

### JavaScript Closures

When a function is created, it "closes over" variables from its surrounding scope at **creation time**:

```tsx
const items = [1, 2, 3];

const callback = () => {
  console.log(items); // Captures [1, 2, 3]
};

// Later...
items = [4, 5, 6]; // This doesn't affect callback
callback(); // Still logs [1, 2, 3]
```

### React's useCallback

`useCallback` memoizes functions to prevent recreation on every render. When dependencies change, it creates a **new** function:

```tsx
// Render 1: items = []
const callback1 = useCallback(() => console.log(items), [items]); // Captures []

// Render 2: items = [1, 2, 3]
const callback2 = useCallback(() => console.log(items), [items]); // Captures [1, 2, 3]

// callback1 !== callback2 (different function references)
```

### The Problem with Refs and Event Handlers

When you store a callback in a ref or pass it to external code that doesn't update frequently:

```tsx
const callbackRef = useRef(callback1); // Stores first version

// Even when callback2 is created, callbackRef.current still points to callback1
```

---

## The Solution: useStableRef

### Single Value

**Use when**: You need to access frequently changing data in a callback

```tsx
import { useStableRef } from 'shared/hooks/useStableRef';

function MyComponent({ items }: { items: Item[] }) {
  // Store latest items in a ref that auto-updates
  const itemsRef = useStableRef(items);

  const handleClick = useCallback(() => {
    // Always accesses current items
    console.log(itemsRef.current.length);
  }, []); // Empty deps - callback never recreated

  useEffect(() => {
    thirdPartyLib.registerHandler(handleClick);
  }, []); // Can safely use empty deps

  return <button onClick={handleClick}>Process Items</button>;
}
```

### Multiple Values

**Use when**: You need to access multiple frequently changing values

```tsx
import { useStableRef } from 'shared/hooks/useStableRef';

function MyComponent({ items, filters, selectedId }) {
  // Store multiple values in refs that auto-update
  const [itemsRef, filtersRef, selectedIdRef] = useStableRef(items, filters, selectedId);

  const handleSearch = useCallback(() => {
    const filtered = itemsRef.current.filter(item =>
      filtersRef.current.includes(item.type) &&
      item.id !== selectedIdRef.current
    );
    return filtered;
  }, []); // No dependencies needed!

  return <SearchButton onSearch={handleSearch} />;
}
```

### How It Works

```tsx
export function useStableRef<T>(value: T) {
  const ref = useRef<T | null>(null);

  // Lazy initialization - only creates ref on first render
  if (ref.current === null) {
    ref.current = { current: value };
  }

  // Update ref whenever value changes
  useEffect(() => {
    ref.current!.current = value;
  }, [value]);

  return ref.current;
}
```

### Benefits

- ✅ Callback never recreates (empty dependency array)
- ✅ Always accesses current data
- ✅ Safe for event handlers and refs
- ✅ No memory leaks
- ✅ Supports single or multiple values
- ✅ Simple, explicit pattern

---

## Real-World Example

### The Bug: Tree Drag-Drop with Stale Data

**Problem:** Drag-drop tree operations accessed empty tree array even after tree loaded.

```tsx
// useClassificationTreeOperations.tsx (BEFORE FIX)
export function useClassificationTreeOperations({ tree, ... }) {
  const handleNodeReorder = useCallback(
    async (dragNodeId: string, dropNodeId: string) => {
      // tree was [] when this callback was first created
      const dropNode = findNodeById(tree, dropNodeId); // tree is []!
      // ... rest of logic fails
    },
    [tree] // tree IS in deps, but callback is still stale in event handler
  );

  return { handleNodeReorder };
}

// ClassificationTree.tsx
function ClassificationTree() {
  const { handleNodeReorder } = useClassificationTreeOperations({ tree, ... });

  // useDragDrop stores handleNodeReorder in a ref
  useDragDrop({
    eventType: 'drop',
    onDrop: (e) => {
      // Calls handleNodeReorder from when tree was empty!
      handleNodeReorder(dragId, dropId);
    }
  });
}
```

**Timeline:**
1. Component mounts, `tree = []` (loading)
2. `handleNodeReorder` created, captures `tree = []`
3. `useDragDrop` stores `handleNodeReorder` in ref
4. Tree loads, `tree = [node1, node2, ...]`
5. `handleNodeReorder` recreated with new `tree`
6. **But** `useDragDrop`'s ref still has old `handleNodeReorder`
7. User drops node → calls old `handleNodeReorder` → `tree = []` → bug

### The Fix: useStableRef Pattern

```tsx
// useClassificationTreeOperations.tsx (AFTER FIX)
export function useClassificationTreeOperations({ tree, ... }) {
  // Store tree in ref that auto-updates
  const treeRef = useStableRef(tree);

  const handleNodeReorder = useCallback(
    async (dragNodeId: string, dropNodeId: string) => {
      // Always uses current tree!
      const currentTree = treeRef.current;
      const dropNode = findNodeById(currentTree, dropNodeId); // Works!
      // ... rest of logic succeeds
    },
    [] // Empty deps - callback never recreated
  );

  return { handleNodeReorder };
}
```

**Plus** (defense in depth):

```tsx
// useDragDrop.ts - Update ref when handlers change
export function useDragDrop(...handlers: DragDropHandler[]) {
  const handlersMapRef = useRef(/* initial */);

  // Update ref whenever handlers change
  useEffect(() => {
    handlersMapRef.current = handlers.reduce(...);
  }, [handlers]);

  // Event listeners use handlersMapRef.current
}
```

**Result:** Both patterns work together to ensure callbacks always have current data.

---

## When to Use

### Use `useStableRef`

**When:**
- Accessing frequently changing data (arrays, objects, primitives)
- Callback passed to refs or third-party libraries
- Event handlers that need current data
- Avoiding dependency arrays
- Multiple values need to be accessed without recreation

**Example scenarios:**
- Tree data in drag-drop handlers ✅ (our use case)
- Filter state in search callbacks
- Selected items in batch operations
- Configuration objects in workers
- Multiple interdependent values

### Use `useCallback` with Dependencies

**When:**
- Callback only uses props/state that don't change often
- No closure issues (callback not stored in refs)
- Normal event handlers in component JSX
- Dependencies are stable

**Example scenarios:**
- Form submit handlers
- Button click handlers (not stored)
- Modal confirm handlers

---

## Anti-Patterns

### ❌ DON'T: Add dependencies without checking callback usage

```tsx
// BAD: handler is stored in a ref, so recreating it doesn't help
const handler = useCallback(() => {
  doSomething(data);
}, [data]); // Recreated on every data change, but ref holds old version

useEffect(() => {
  refThatDoesntUpdate.current = handler; // Only runs once!
}, []);
```

**Fix:** Use `useStableRef` for `data` and empty deps for `handler`

### ❌ DON'T: Omit dependencies to "fix" stale closures

```tsx
// BAD: ESLint warning ignored, potential bugs
const handler = useCallback(() => {
  doSomething(data);
}, []); // eslint-disable-line react-hooks/exhaustive-deps
```

**Fix:** Use `useStableRef(data)` and acknowledge empty deps is intentional

### ❌ DON'T: Use state setters for data access

```tsx
// BAD: Trying to use setter's callback to access current state
const [items, setItems] = useState([]);

const handler = useCallback(() => {
  setItems(current => {
    doSomething(current); // Anti-pattern for data access
    return current;
  });
}, []);
```

**Fix:** Use `useStableRef(items)` for clean data access

### ❌ DON'T: Manually create refs for every value

```tsx
// BAD: Duplicating ref update pattern in every component
const dataRef = useRef(data);
useEffect(() => {
  dataRef.current = data;
}, [data]);

const handler = useCallback(() => {
  use(dataRef.current);
}, []);
```

**Fix:** Use `useStableRef(data)` utility to encapsulate the pattern

---

## Checklist

Before implementing a callback:

- [ ] Is this callback stored in a ref? → Consider `useStableRef`
- [ ] Is this callback passed to a third-party library? → Consider `useStableRef`
- [ ] Does this callback access frequently changing data? → Consider `useStableRef`
- [ ] Do I need to access multiple values? → Use `useStableRef` with multiple args
- [ ] Is this a normal event handler in JSX? → Regular `useCallback` is fine
- [ ] Do I have dependencies in the array? → Verify they actually update the callback usage

---

## Related Files

- **Hook implementation:** [src/shared/hooks/useStableRef.ts](../../D3dxSkinManager.Client/src/shared/hooks/useStableRef.ts)
- **Real-world usage:** [src/modules/mods/components/ClassificationPanel/useClassificationTreeOperations.tsx](../../D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/useClassificationTreeOperations.tsx)
- **Drag-drop hook:** [src/shared/hooks/useDragDrop.ts](../../D3dxSkinManager.Client/src/shared/hooks/useDragDrop.ts)

---

## Summary

**TL;DR:**
- `useCallback` with dependencies doesn't guarantee callbacks always use current data
- When callbacks are stored in refs or passed to external code, they can become stale
- **Solution:** Use `useStableRef` to store data that changes frequently
- Supports single value: `const ref = useStableRef(value)`
- Supports multiple values: `const [ref1, ref2] = useStableRef(value1, value2)`
- The utility is in `src/shared/hooks/useStableRef.ts`

**Remember:** If you're adding dependencies to `useCallback` and still getting stale data, you likely have a closure issue that needs `useStableRef`.
