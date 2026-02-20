# Drag-and-Drop API Improvements

**Date:** 2026-02-21
**Type:** Refactoring + Bug Fix
**Impact:** Better API design, fixed style cleanup issues
**Files Changed:** 3

## Summary

Improved the drag-and-drop API to be more domain-focused and layered, replacing confusing `gap-top`/`gap-bottom` strings with clearer `dropType: 'node' | 'gap'` and `gapSide: 'top' | 'bottom'` parameters. Also fixed critical style cleanup issue where drag styles weren't being properly removed when moving between nodes.

## Problems Fixed

### 1. Confusing API Design

**Issue:** Using combined string values like `'gap-top'` and `'gap-bottom'` violated separation of concerns

**Before:**
```typescript
handleNodeReorder(
  dragNodeId,
  dropNodeId,
  dropToGap: boolean,      // Redundant - duplicates type info
  dropPosition?: 'gap-top' | 'gap-bottom'  // Confusing - combines two concerns
)
```

**Problems:**
- `dropToGap` boolean duplicates information (if position is 'gap-top', it's obviously a gap)
- `'gap-top'` combines drop type AND position in one string
- Not layered - mixing domain concepts
- Harder to reason about the code

**After:**
```typescript
handleNodeReorder(
  dragNodeId: string,
  dropNodeId: string,
  dropType: 'node' | 'gap',     // Clear: what kind of drop
  gapSide?: 'top' | 'bottom'    // Clear: which side (only for gaps)
)
```

**Benefits:**
- ✅ Layered design - separate concerns
- ✅ Clear domain language
- ✅ Type-safe with proper optionality
- ✅ Self-documenting code

### 2. Style Cleanup Issues

**Issue:** Drag styles weren't being cleaned up when moving between nodes, causing visual artifacts

**Problem:**
When dragging over Node A, then Node B:
1. Node A gets `drag-over-gap` class
2. Node B gets `drag-over-gap` class
3. **BUG:** Node A still has `drag-over-gap` class!
4. Result: Multiple nodes show drag styling at once

**Root Cause:**
The old code only cleaned up the current target:
```typescript
// ❌ Only cleans current target
target.classList.remove('drag-over-node', 'drag-over-gap', ...);
```

**Fix:**
Clean up ALL nodes before applying to current:
```typescript
// ✅ Clean up all nodes first
const allNodes = container?.querySelectorAll(handler.nodeSelector);
allNodes?.forEach(node => {
  node.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
  node.removeAttribute('data-gap-position');
});

// Then apply to current target
target.classList.add(className);
```

**Benefits:**
- ✅ Only one node shows drag styling at a time
- ✅ No leftover styles after drag ends
- ✅ Clean visual feedback

## Changes Made

### File 1: useClassificationTreeOperations.tsx

**Function signature update:**
```diff
const handleNodeReorder = useCallback(
  async (
    dragNodeId: string,
    dropNodeId: string,
-   dropToGap: boolean,
-   dropPosition?: 'gap-top' | 'gap-bottom'
+   dropType: 'node' | 'gap',
+   gapSide?: 'top' | 'bottom'
  ) => {
```

**Usage update:**
```diff
- if (!dropToGap && !expandedKeys.includes(dropNodeId)) {
+ if (dropType === 'node' && !expandedKeys.includes(dropNodeId)) {
    onExpandedKeysChange([...expandedKeys, dropNodeId]);
  }

- if (dropToGap) {
+ if (dropType === 'gap') {
    // Gap drop logic
-   if (dropPosition === 'gap-top') {
+   if (gapSide === 'top') {
      finalPosition = dropNodeIndex;
    } else {
      finalPosition = dropNodeIndex + 1;
    }
  }
```

### File 2: ClassificationTreeContext.tsx

**Type definition update:**
```diff
handleNodeReorder: (
  dragNodeId: string,
  dropNodeId: string,
- dropToGap: boolean,
- dropPosition?: 'gap-top' | 'gap-bottom'
+ dropType: 'node' | 'gap',
+ gapSide?: 'top' | 'bottom'
) => Promise<void>;
```

### File 3: ClassificationTree.tsx

**Handler call update:**
```diff
logger.debug('[TreeDrop] Reordering:', {
  dragNode: draggedNodeKeyRef.current,
  dropNode: dropNodeId,
- type,
- position: dropPosition
+ dropType: type,
+ gapSide: gapPosition
});

handleNodeReorder(
  draggedNodeKeyRef.current,
  dropNodeId,
- type === 'gap',
- dropPosition
+ type,
+ gapPosition
);
```

### File 4: useDragDrop.ts

**Style cleanup fix:**
```diff
// Get custom or default class names
const nodeClass = handler.classes?.node ?? 'drag-over-node';
const gapClass = handler.classes?.gap ?? 'drag-over-gap';

- // Remove old classes from current target only
- target.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
+ // Clean up all previous drag styling from all nodes in container
+ // This ensures only the current target has styling
+ const allNodes = container?.querySelectorAll(handler.nodeSelector);
+ allNodes?.forEach(node => {
+   node.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
+   node.removeAttribute('data-gap-position');
+ });

// Determine if drop is allowed
const dropAllowed = (allow === 'all') || allow === dropType;

// Add appropriate class to current target
if (dropAllowed) {
  const className = dropType === 'node' ? nodeClass : gapClass;
  target.classList.add(className);

  if (dropType === 'gap') {
    const gapPosition = calculateGapPosition(e, target, gapThreshold);
    target.setAttribute('data-gap-position', gapPosition);
  }
-   // ❌ This else is not needed - cleanup handles it
- } else {
-   target.removeAttribute('data-gap-position');
- }
}
```

## API Comparison

### Before (Confusing)
```typescript
// Dropping between nodes at top
handleNodeReorder(dragId, dropId, true, 'gap-top');

// Dropping between nodes at bottom
handleNodeReorder(dragId, dropId, true, 'gap-bottom');

// Dropping into node
handleNodeReorder(dragId, dropId, false, undefined);
```

Problems:
- Boolean + string feels redundant
- Have to remember `true` means gap
- Combined string mixes concerns
- `undefined` for node drops is unclear

### After (Clear)
```typescript
// Dropping between nodes at top
handleNodeReorder(dragId, dropId, 'gap', 'top');

// Dropping between nodes at bottom
handleNodeReorder(dragId, dropId, 'gap', 'bottom');

// Dropping into node
handleNodeReorder(dragId, dropId, 'node');
```

Benefits:
- Self-documenting
- Proper optional parameter (gapSide only for gaps)
- Layered: type first, then details
- Clear domain language

## Testing

✅ TypeScript compilation: SUCCESS
✅ Frontend build: SUCCESS
✅ No new warnings
✅ All drag-drop scenarios work:
  - Drag node above another (gap, top)
  - Drag node below another (gap, bottom)
  - Drag node into another (node)
  - Styles clean up properly when moving

## Best Practices Applied

Following [docs/AI_GUIDE.md](../AI_GUIDE.md):

1. ✅ **Domain-Focused Design** - Parameters match domain concepts
2. ✅ **Layered Architecture** - Type and details separated
3. ✅ **Type Safety** - Proper union types and optionality
4. ✅ **Clear Naming** - `dropType` and `gapSide` are self-explanatory
5. ✅ **Bug Fixes** - Properly cleanup styles across all nodes

## Migration Guide

For other code using the old API:

**Before:**
```typescript
const handleDrop = (drag, drop, isGap, position) => {
  if (isGap) {
    if (position === 'gap-top') {
      // ...
    } else {
      // ...
    }
  } else {
    // node drop
  }
};
```

**After:**
```typescript
const handleDrop = (drag, drop, dropType, gapSide) => {
  if (dropType === 'gap') {
    if (gapSide === 'top') {
      // ...
    } else {
      // ...
    }
  } else {
    // node drop
  }
};
```

## Related Changes

- [2026-02-21-usedragdrop-hook-fixes.md](./2026-02-21-usedragdrop-hook-fixes.md) - Initial hook fixes
- [2026-02-21-classification-tree-drag-drop-fix.md](./2026-02-21-classification-tree-drag-drop-fix.md) - Fixed node reordering
- [2026-02-21-classification-tree-refactoring.md](./2026-02-21-classification-tree-refactoring.md) - Code cleanup

---

**Reviewed by:** AI Assistant
**Following:** [docs/AI_GUIDE.md](../AI_GUIDE.md)
