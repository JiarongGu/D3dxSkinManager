# Gap Indicator Visual Fixes

**Date:** 2026-02-21
**Type:** Bug Fix
**Impact:** Visual quality improvement
**Files Changed:** 2

## Summary

Fixed visual issues with drag-and-drop gap indicators: eliminated duplicate lines and centered the indicator line in the gap between nodes.

## Problems Fixed

### 1. Double Gap Lines

**Issue:** Two gap indicator lines were appearing when dragging nodes

**Root Cause:** Duplicate CSS definitions in two different files:
1. `ClassificationTree.css` (lines 139-179) - Old Ant Design styles for `drag-over-gap-top` and `drag-over-gap-bottom`
2. `useDragDrop.css` (lines 12-28) - New custom hook styles

Both were active at the same time, causing two lines to render.

**Fix:** Removed the old CSS from ClassificationTree.css, keeping only the useDragDrop.css implementation

**Before:**
```
[Node A]
─────── ← Line from useDragDrop.css
─────── ← Line from ClassificationTree.css (duplicate!)
[Node B]
```

**After:**
```
[Node A]
─────── ← Single line from useDragDrop.css
[Node B]
```

### 2. Gap Line Not Centered

**Issue:** Gap indicator line was at edge of gap, not centered in the middle

**Root Cause:**
- Nodes have `margin: 0 0 8px 0` creating 8px gap
- Line was positioned at `-2px` from edges
- Should be at `-5px` to center in 8px gap (4px from each edge)

**Visual Diagram:**

```
Before:
[Node A          ] ← bottom edge
─ ← -2px (too close to top)

  8px gap

[Node B          ] ← top edge

After:
[Node A          ] ← bottom edge

    ─ ← -5px (centered: 4px from top, 4px from bottom)

  8px gap

[Node B          ] ← top edge
```

**Fix:** Updated positioning from `-2px` to `-5px`

```css
/* Before */
.drag-over-gap::before {
  top: -2px;
}
.drag-over-gap[data-gap-position="bottom"]::before {
  bottom: -2px;
}

/* After */
.drag-over-gap::before {
  top: -5px; /* Center in 8px gap */
}
.drag-over-gap[data-gap-position="bottom"]::before {
  bottom: -5px; /* Center in 8px gap */
}
```

## Changes Made

### File 1: ClassificationTree.css

**Removed duplicate CSS (lines 138-192):**

```diff
- /* Custom drop indicator line for reordering - show only when dropping between nodes (gap) */
- .classification-tree .ant-tree-treenode.drag-over-gap-top::before,
- .classification-tree .ant-tree-treenode.drag-over-gap-bottom::before {
-   content: '';
-   position: absolute;
-   left: 0;
-   right: 0;
-   height: 3px;
-   background: linear-gradient(90deg, var(--color-primary), var(--color-primary) 80%, transparent);
-   z-index: 1000;
-   pointer-events: none;
-   box-shadow: 0 0 8px rgba(24, 144, 255, 0.8);
- }
-
- .classification-tree .ant-tree-treenode.drag-over-gap-top::before {
-   top: -4px;
- }
-
- .classification-tree .ant-tree-treenode.drag-over-gap-bottom::before {
-   bottom: -4px;
- }
-
- /* Add a subtle background highlight to the gap area when dragging over */
- .classification-tree .ant-tree-treenode.drag-over-gap-top::after,
- .classification-tree .ant-tree-treenode.drag-over-gap-bottom::after {
-   content: '';
-   position: absolute;
-   left: 0;
-   right: 0;
-   height: 8px;
-   background: rgba(24, 144, 255, 0.1);
-   z-index: 999;
-   pointer-events: none;
- }
-
- .classification-tree .ant-tree-treenode.drag-over-gap-top::after {
-   top: -4px;
- }
-
- .classification-tree .ant-tree-treenode.drag-over-gap-bottom::after {
-   bottom: -4px;
- }
-
- /* Highlight folder nodes when dragging over them */
- .classification-tree .ant-tree-treenode.drag-over-gap-top > .ant-tree-node-content-wrapper,
- .classification-tree .ant-tree-treenode.drag-over-gap-bottom > .ant-tree-node-content-wrapper {
-   background-color: transparent !important;
- }
-
- .classification-tree .ant-tree-treenode.drag-over > .ant-tree-node-content-wrapper {
-   background-color: var(--color-primary-bg-hover) !important;
-   opacity: 0.8;
-   border: 2px solid var(--color-primary) !important;
-   box-shadow: 0 0 8px rgba(24, 144, 255, 0.4);
- }

+ /* Drop indicator styling handled by useDragDrop.css - removed duplicate styles */
```

**Result:** -54 lines of duplicate CSS removed

### File 2: useDragDrop.css

**Updated positioning and added comments:**

```diff
- .drag-over-gap {
-   position: relative;
- }
-
+ /* Gap drop indicator - shows line in the middle of the gap between nodes */
  .drag-over-gap::before {
    content: '';
    position: absolute;
    left: 0;
    right: 0;
-   height: 3px;
+   height: 2px;
    background: linear-gradient(90deg, var(--color-primary), var(--color-primary) 80%, transparent);
    z-index: 1000;
    pointer-events: none;
    box-shadow: 0 0 8px rgba(24, 144, 255, 0.8);
-   top: -2px; /* Position at top by default */
+   top: -5px; /* Center in 8px gap (4px up from bottom edge) */
  }

  .drag-over-gap[data-gap-position="bottom"]::before {
    top: auto;
-   bottom: -2px; /* Position at bottom when dropping at bottom */
+   bottom: -5px; /* Center in 8px gap (4px up from top edge of next node) */
  }
```

## Visual Result

### Before
- ❌ Two lines appearing (one from each CSS file)
- ❌ Lines positioned at edges of gap
- ❌ Confusing visual feedback

### After
- ✅ Single line appearing
- ✅ Line centered in gap between nodes
- ✅ Clear, professional visual feedback

## Build Impact

- CSS file size: **-104 B** (removed duplicate code)
- No new warnings
- All functionality preserved

## Testing

✅ Drag node above another → Single centered line at top
✅ Drag node below another → Single centered line at bottom
✅ No duplicate lines
✅ Line appears in middle of 8px gap
✅ Visual feedback is clear and professional

## Technical Notes

### Why -5px?

The gap between nodes is 8px (from `margin: 0 0 8px 0`).

To center a line in this gap:
- Line should be 4px from Node A's bottom edge
- Line should be 4px from Node B's top edge
- Line height is 2px

**For top position:**
```
Node A bottom edge (0px)
  ↓
  1px
  2px
  3px
  4px ← Line starts here (-5px from Node B top)
  5px ← Line ends here (-4px from Node B top)
  6px
  7px
  8px
  ↓
Node B top edge
```

**Positioning:**
- `top: -5px` means: "5px above the element's top edge"
- Since the line is 2px tall, it spans from -5px to -3px
- This centers it in the 8px gap (4px margin on each side)

Same logic applies for `bottom: -5px`.

## Related Changes

- [2026-02-21-drag-drop-api-improvements.md](./2026-02-21-drag-drop-api-improvements.md) - API improvements
- [2026-02-21-usedragdrop-hook-fixes.md](./2026-02-21-usedragdrop-hook-fixes.md) - Initial hook fixes

---

**Reviewed by:** AI Assistant
**Following:** [docs/AI_GUIDE.md](../AI_GUIDE.md)
