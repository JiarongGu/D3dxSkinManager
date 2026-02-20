# 2026-02-20 - Archive Extraction 7z Support & Optimistic Update Fixes

## Summary
Added native 7z archive support using SevenZipSharp library, fixed critical optimistic update bugs including UI not updating on load/unload and category change not unloading mods. Implemented proper tree count calculation for classification optimistic updates.

## Problems Solved

### 1. 7z Archive Extraction Failing
**Problem**: Archives with `.7z` format were failing to extract with error:
```
Cannot determine compressed stream type. Supported Reader Formats: Ace, Arc, Arj, Zip, GZip, BZip2, Tar, Rar, LZip, Lzw, XZ, ZStandard
```

**Root Cause**: SharpCompress `ReaderFactory` doesn't support 7z format.

**Solution**:
- Replaced SharpCompress with SevenZipSharp + 7z.Libs packages
- Added global 7z library initialization in `Program.cs:290-316`
- Updated path logic to find DLL in `x64/` or `x86/` subdirectories
- Updated `ArchiveService.cs`, `FileService.cs`, and `MigrationStep5MigrateModArchives.cs`

**Files Modified**:
- `D3dxSkinManager/D3dxSkinManager.csproj` - Added SevenZipSharp and 7z.Libs packages
- `D3dxSkinManager/Program.cs` - Added `Initialize7zLibrary()` method
- `D3dxSkinManager/Modules/Core/Services/ArchiveService.cs` - Switched to SevenZipSharp
- `D3dxSkinManager/Modules/Core/Services/FileService.cs` - Switched to SevenZipSharp
- `D3dxSkinManager/Modules/Migration/Steps/MigrationStep5MigrateModArchives.cs` - Use IArchiveService

### 2. UI Not Updating on Load/Unload
**Problem**: When loading/unloading mods, the UI remained unchanged. Optimistic verification showed "✓ Verification passed" but UI didn't update.

**Root Cause**: Optimistic update only dispatched to `modsDataReducer`, not to `classificationReducer`. The filtered mod list shown in UI wasn't being updated.

**Solution**:
- Updated `loadModInGame` and `unloadModFromGame` in `ModsContext.tsx:288-382` to dispatch to BOTH reducers:
  - `UPDATE_MOD_LOCAL` → updates main mods array
  - `UPDATE_FILTERED_MOD` → updates classification filtered mods array

**Files Modified**:
- `D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx`

### 3. Category Update Not Unloading Mods
**Problem**: When changing category on a loaded mod, the backend should unload it (remove `DISABLED-` prefix from folder), but `mod.IsLoaded` remained `true` in the event data.

**Root Cause Two Issues**:
1. `UpdateCategoryAsync` called `_repository.GetByIdAsync()` directly, bypassing the facade's `GetModByIdAsync()` which populates `IsLoaded` from file system
2. After calling `UnloadModAsync`, the in-memory `mod` object still had old `IsLoaded` value

**Solution**:
- Changed to call `GetModByIdAsync()` (facade method) instead of repository directly
- Added logging to verify unload is being called
- Removed useless `SetLoadedStateAsync` calls (it's a no-op since `IsLoaded` is determined from file system)

**Files Modified**:
- `D3dxSkinManager/Modules/Mods/ModFacade.cs:282-319` - Fixed GetByIdAsync call, added logging
- `D3dxSkinManager/Modules/Mods/ModFacade.cs:164-186` - Removed no-op SetLoadedStateAsync calls
- `D3dxSkinManager.Tests/Modules/Mods/ModFacadeTests.cs` - Updated tests to not verify no-op calls

### 4. Optimistic Update Verification Showing Mismatches
**Problem**: Optimistic update verification was detecting mismatches and refreshing unnecessarily due to:
1. Array ordering differences between frontend and backend
2. Tree count mismatches when changing mod category

**Root Cause**:
1. Backend returned mods ordered by `Category, Name` but frontend expected different order
2. Frontend didn't calculate expected tree counts after category change

**Solution**:
**Ordering Fix**:
- Changed backend queries to `ORDER BY SHA` for consistency
- Files: `ModRepository.cs:82, 201`

**Tree Count Fix**:
- Added `updateTreeCounts()` helper function that:
  - Decrements old category node count: `-1`
  - Increments new category node count: `+1`
  - Handles ancestor detection: if moving to ancestor node (e.g., `parent/child` → `parent`), only `-1` from child, no `+1` to parent
  - Recursively updates all nodes in tree
- Dispatches optimistic tree update to UI immediately
- Verifies expected tree vs backend tree

**Files Modified**:
- `D3dxSkinManager/Modules/Mods/Services/ModRepository.cs` - Changed ORDER BY to SHA
- `D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx:411-428, 463-525` - Added tree count calculation

### 5. Detailed Mismatch Logging
**Problem**: When verification detected mismatches, logs just said "Mismatch detected" without showing what was different.

**Solution**:
- Added `findDifferences()` function to `useOptimisticUpdate.ts` that recursively compares objects/arrays
- Shows exact field paths and expected vs actual values
- Example output:
  ```
  [OptimisticUpdate] ❌ Mismatch detected
  Found 2 difference(s):
    - tree[2].children[0].modCount: expected 0, got 1
    - tree[2].children[1].modCount: expected 2, got 1
  ```

**Files Modified**:
- `D3dxSkinManager.Client/src/shared/hooks/useOptimisticUpdate.ts:15-54, 122-128`

### 6. Same-Category Move Optimization
**Problem**: When dragging a mod to the same category it's already in, the system was still performing unnecessary updates and backend calls.

**Solution**:
- Added early return check in `updateModCategory` function
- If `oldCategory === categoryId`, immediately return `true` without any operations
- Skips optimistic updates, backend calls, and verification

**Files Modified**:
- `D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx:411-414` - Added same-category check

## Technical Details

### Backend Tree Count Logic
From `ClassificationService.cs:433-448`:
```csharp
private int CalculateNodeModCount(ClassificationNode node, Dictionary<string, int> modsByCategory)
{
    // Get direct mod count for this node's category
    var directCount = modsByCategory.TryGetValue(node.Id, out var count) ? count : 0;

    // Recursively calculate counts for all children
    var childrenCount = 0;
    foreach (var child in node.Children)
    {
        childrenCount += CalculateNodeModCount(child, modsByCategory);
    }

    // Total count is direct + children
    node.ModCount = directCount + childrenCount;
    return node.ModCount;
}
```

**Key insight**: `node.ModCount = directCount + childrenCount`

This means when moving from `parent/child` to `parent`:
- `child` loses 1 direct mod → child total decreases by 1
- `parent` gains 1 direct mod → parent direct increases by 1
- But parent total = parent direct + child total
- Since parent direct +1 and child total -1, parent total stays same!

### Frontend Tree Count Algorithm
```typescript
const updateTreeCounts = (tree, mods, oldCategory, newCategory) => {
  // 1. Detect if moving to ancestor (e.g., parent/child → parent)
  const isAncestor = (ancestorId, childId) => { /* recursive check */ };
  const movingToAncestor = oldCategory ? isAncestor(newCategory, oldCategory) : false;

  // 2. Update nodes
  const updateNode = (node) => {
    // Decrement old category
    if (node.id === oldCategory) {
      node.modCount -= 1;
    }

    // Increment new category ONLY if not moving to ancestor
    if (node.id === newCategory && !movingToAncestor) {
      node.modCount += 1;
    }

    // Recurse children
    if (node.children) {
      node.children = node.children.map(updateNode);
    }

    return node;
  };

  return tree.map(updateNode);
};
```

## Testing

### Test Scenarios
1. ✅ Extract 7z archive - should work without errors
2. ✅ Drag mod to same category - should do nothing (no backend call)
3. ✅ Load mod - UI updates instantly, folder renamed from `DISABLED-{SHA}` to `{SHA}`
4. ✅ Unload mod - UI updates instantly, folder renamed from `{SHA}` to `DISABLED-{SHA}`
5. ✅ Change category on loaded mod - mod gets unloaded (folder gets `DISABLED-` prefix), backend log shows unload
6. ✅ Change category on unloaded mod - no unload needed
7. ✅ Move mod from `parent/child` to `parent` - only child count decreases, parent stays same
8. ✅ Move mod from `parent1` to `parent2` - parent1 decreases, parent2 increases
9. ✅ Verification passes - no unnecessary refreshes
10. ✅ Verification fails - detailed diff in console, automatic refresh

### Verification Logging Examples

**Success**:
```
[OptimisticUpdate] ✓ Verification passed - no refresh needed
```

**Mismatch**:
```
[OptimisticUpdate] ❌ Mismatch detected, calling onMismatch
Found 1 difference(s):
  - [0].isLoaded: expected true, got false
```

## Impact

**User Experience**:
- ✅ 7z archives now extract successfully
- ✅ Instant UI feedback when loading/unloading mods
- ✅ Tree counts update smoothly when moving mods between categories
- ✅ No more jarring full-page refreshes unless truly necessary

**Developer Experience**:
- ✅ Detailed mismatch logging makes debugging optimistic updates easy
- ✅ Consistent ordering eliminates false positive mismatches
- ✅ Reusable `useOptimisticUpdate` hook with proper TypeScript generics

## Related Documentation
- See `docs/architecture/OPTIMISTIC_UPDATES.md` for optimistic update pattern
- See `docs/core/ARCHIVE_EXTRACTION.md` for archive service documentation
- See `docs/troubleshooting/OPTIMISTIC_UPDATE_VERIFICATION.md` for verification debugging

## Breaking Changes
None - all changes are backward compatible.

## Dependencies Changed
- ➕ Added: `SevenZipSharp 0.64.0`
- ➕ Added: `7z.Libs 18.6.0`
- ➖ Removed: `SharpCompress` (no longer needed)
