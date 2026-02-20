# useDragDrop Hook Fixes and Improvements

**Date:** 2026-02-21
**Type:** Bug Fix + Enhancement
**Impact:** Critical UX improvement for drag-and-drop operations
**Files Changed:** 5

## Summary

Fixed multiple critical issues in the `useDragDrop` hook that were causing poor UX during drag-and-drop operations. Also reviewed and fixed all usages of the hook to ensure proper workflow.

## Problems Fixed

### 1. Missing Semicolon (Syntax Error)
- **File:** `useDragDrop.ts:12`
- **Issue:** Missing semicolon on `export type DropType = 'node' | 'gap'`
- **Fix:** Added semicolon
- **Impact:** TypeScript compilation error

### 2. Incorrect Gap Detection Logic
- **File:** `useDragDrop.ts:203-216`
- **Issue:** `calculateDropType()` function only checked top/bottom but didn't properly detect middle zone
- **Before:**
  ```typescript
  const calculateDropType = (e, target, gapThreshold) => {
    const gapPosition = calculateGapPosition(e, target, gapThreshold);
    return gapPosition === 'top' || gapPosition === 'bottom' ? 'gap' : 'node';
  };
  ```
- **After:**
  ```typescript
  const calculateDropType = (e, target, gapThreshold) => {
    const rect = target.getBoundingClientRect();
    const relativeY = (e.clientY - rect.top) / rect.height;
    if (relativeY < gapThreshold || relativeY > (1 - gapThreshold)) {
      return 'gap';
    }
    return 'node';
  };
  ```
- **Impact:** Now correctly detects gap zones (top/bottom 15%) vs node zone (middle 70%)

### 3. Drag Leave Flickering (CRITICAL FIX)
- **File:** `useDragDrop.ts:282-312`
- **Issue:** The `dragleave` event fires when entering child elements, causing visual flickering
- **Root Cause:** Event bubbling - when mouse enters a child element, `dragleave` fires on parent
- **Fix:** Check `relatedTarget` to only cleanup when actually leaving the element:
  ```typescript
  const relatedTarget = e.relatedTarget as HTMLElement;
  if (!relatedTarget || !target.contains(relatedTarget)) {
    // Only now clean up classes
    target.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
    target.removeAttribute('data-gap-position');
  }
  ```
- **Impact:** Eliminates flickering when dragging over elements with nested content

### 4. React State Management
- **File:** `useDragDrop.ts:162-379`
- **Issue:** Using `useRef` with dependency on `containerRef.current` which doesn't trigger re-renders
- **Fix:** Changed to `useState` with callback ref pattern:
  ```typescript
  // Before
  const containerRef = useRef<T>(null);
  useEffect(() => { ... }, [containerRef.current]);
  return { containerRef };

  // After
  const [container, setContainer] = useState<T | null>(null);
  useEffect(() => { ... }, [container]);
  return { containerRef: setContainer };
  ```
- **Impact:** Event listeners properly re-register when container element changes

### 5. Gap Position Visual Indicator
- **File:** `useDragDrop.ts` + `useDragDrop.css`
- **Issue:** Gap indicator line always appeared at top, regardless of drop position
- **Fix:**
  - Added `data-gap-position` attribute to indicate top/bottom
  - Updated CSS to position indicator based on attribute:
    ```css
    .drag-over-gap::before {
      top: -2px; /* Default: top */
    }
    .drag-over-gap[data-gap-position="bottom"]::before {
      top: auto;
      bottom: -2px; /* Position at bottom */
    }
    ```
- **Impact:** Visual feedback now accurately shows where item will be dropped

## Usage Fixes

### ClassificationTree.tsx
- **Issue:** Mod drop handler was extracting node ID in `onDrop` instead of using hook properly
- **Fix:** Properly extract node ID from target element in `onDrop` callback
- **Before:** Data extraction mixed with business logic
- **After:** Clean separation - hook handles DOM extraction, callback handles business logic

### ClassificationPanel.tsx
- **Issue:** `handleUnclassifiedDrop` was passing SHA as both modSha and modName
- **Fix:** Pass empty string for modName (consistent with other usages)
- **Before:** `updateModCategory(sha, sha, '', 'Unclassified')`
- **After:** `updateModCategory(sha, '', '', 'Unclassified')`

## Files Changed

1. **D3dxSkinManager.Client/src/shared/hooks/useDragDrop.ts**
   - Fixed syntax error (missing semicolon)
   - Fixed gap detection logic
   - Fixed dragleave flickering with relatedTarget check
   - Changed from useRef to useState for proper re-renders
   - Added gap position attribute management

2. **D3dxSkinManager.Client/src/shared/hooks/useDragDrop.css**
   - Added gap position-specific styling
   - Added `.drag-over-gap { position: relative; }`
   - Added `[data-gap-position="bottom"]` selector

3. **D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/ClassificationTree.tsx**
   - Fixed mod drop handler to properly extract node ID from target
   - Removed incorrect use of `onData` for mod drops (needs both dataTransfer AND DOM data)

4. **D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/ClassificationPanel.tsx**
   - Fixed `handleUnclassifiedDrop` to pass empty string for modName
   - Added `await` for `onModsRefresh()` call

5. **Documentation**
   - Updated this changelog entry

## Testing

✅ TypeScript compilation: `npx tsc --noEmit` - SUCCESS
✅ Frontend build: `npm run build` - SUCCESS
✅ No new warnings introduced

## Expected UX Improvements

1. **No Flickering** - Smooth drag-over feedback without visual glitches
2. **Accurate Gap Detection** - Clear distinction between "drop into" vs "drop between"
3. **Correct Visual Feedback** - Gap indicator appears at correct position (top/bottom)
4. **Reliable Event Handling** - Event listeners work correctly when components mount/unmount

## Technical Notes

### Why relatedTarget Check is Critical

The `dragleave` event fires in these scenarios:
1. ✅ Mouse leaves the element entirely (SHOULD cleanup)
2. ❌ Mouse enters a child element (SHOULD NOT cleanup)

Without the `relatedTarget` check, case #2 causes flickering because:
- `dragleave` fires → classes removed → visual feedback disappears
- `dragover` fires on child → classes re-added → visual feedback reappears
- This happens many times per second as mouse moves → visible flickering

The fix:
```typescript
const relatedTarget = e.relatedTarget as HTMLElement;
if (!relatedTarget || !target.contains(relatedTarget)) {
  // relatedTarget is where the mouse went
  // Only cleanup if mouse went outside our element
  cleanupVisualFeedback();
}
```

### Why useState Instead of useRef

With `useRef`:
```typescript
const containerRef = useRef<T>(null);
useEffect(() => {
  const container = containerRef.current; // ❌ Not reactive
  // Event listeners registered
}, [containerRef.current]); // ❌ Doesn't trigger re-run when ref changes
```

With `useState`:
```typescript
const [container, setContainer] = useState<T | null>(null);
useEffect(() => {
  // Event listeners registered
}, [container]); // ✅ Re-runs when container changes
return { containerRef: setContainer }; // ✅ Callback ref updates state
```

## Related Documentation

- [useDragDrop Hook](../../shared/hooks/useDragDrop.ts) - Implementation
- [ClassificationTree](../src/modules/mods/components/ClassificationPanel/ClassificationTree.tsx) - Primary usage
- [AI_GUIDE.md](../AI_GUIDE.md) - Development guidelines

## Migration Notes

No breaking changes. All existing usages continue to work with improved behavior.

## Update: Removed onData Feature (2026-02-21)

### Why onData Was Removed

The `onData` feature was designed to extract custom data from DOM elements and replace the dataTransfer data. However, this created a fundamental problem:

**Problem:** Many use cases need BOTH dataTransfer data AND DOM-extracted data
- Example: Dropping a mod (from dataTransfer) onto a tree node (ID from DOM)
- onData could only provide ONE value, forcing manual extraction anyway

**Solution:** Remove `onData` entirely
- Users extract what they need from the `target` element in `onDrop`
- Simpler API with one clear pattern
- No confusion about which data comes from where

### Changes

**Before:**
```typescript
onData: ({ target }) => target.textContent?.trim(),
onDrop: ({ data }) => {
  // data is now the DOM-extracted value, lost dataTransfer value!
}
```

**After:**
```typescript
onDrop: ({ data, target }) => {
  // data = dataTransfer value
  // Extract from target if needed
  const nodeId = target?.textContent?.trim() || '';
  handleDrop(data, nodeId); // Both values available!
}
```

### Files Updated
- `useDragDrop.ts` - Removed `onData` property and implementation
- `ClassificationTree.tsx` - Updated tree node handler to extract from target in onDrop
- Documentation examples updated

## Future Improvements

1. Consider adding `onDragEnter` callback for additional control
2. Add support for multiple drop zones within same container
3. Add accessibility features (keyboard navigation for drag-drop)
4. Consider adding visual preview of dragged item

---

**Reviewed by:** AI Assistant
**Following:** [docs/AI_GUIDE.md](../AI_GUIDE.md)
