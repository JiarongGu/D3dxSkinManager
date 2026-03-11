# Changelog: Preview Image Cache Busting & Delete Renumbering

**Date:** 2026-03-06
**Type:** Bug Fix + Enhancement
**Modules Affected:** Core (CustomSchemeHandler), Context (ImageService), Mod (Frontend)

---

## Overview

Fixed two critical bugs in preview image management:

1. **Cache Busting Issue**: Browser cached old preview images after thumbnail reordering operations
2. **Delete Bug**: Deleting a preview didn't renumber remaining previews, causing import operations to overwrite existing images

---

## Changes

### 1. Timestamp-Based Cache Busting

**Problem:**
- Backend reorders preview files when setting thumbnail (e.g., preview3 → preview1, preview1 → preview2)
- Browser cache showed old images at old positions
- Store-level cache timestamp wasn't sufficient as URLs were identical

**Solution:**
- Added timestamp query parameter to preview image URLs (`app://path?t=1234567890`)
- Browser treats URLs with different timestamps as new resources
- CustomSchemeHandler strips query parameters before file lookup

#### Backend Changes

**File:** `D3dxSkinManager\Modules\Core\Services\CustomSchemeHandler.cs`

```csharp
// Added query parameter stripping (line 107-112)
// Strip query parameters (e.g., ?t=1234567890) used for cache busting
var queryIndex = encodedPath.IndexOf('?');
if (queryIndex >= 0)
{
    encodedPath = encodedPath.Slice(0, queryIndex);
}
```

#### Frontend Changes

**File:** `D3dxSkinManager.Client\src\shared\utils\imageUrlHelper.ts`

```typescript
// Added optional cacheTimestamp parameter
export function toAppUrl(path: string | undefined, cacheTimestamp?: number): string | undefined {
  // ... existing code ...

  // Append timestamp for cache busting if provided
  if (cacheTimestamp !== undefined) {
    return `${baseUrl}?t=${cacheTimestamp}`;
  }

  return baseUrl;
}
```

**Files Modified:**
- `PreviewImageCarousel.tsx` - Pass timestamp to `toAppUrl()`
- `ModPreviewPanel.tsx` - Pass timestamp for fullscreen preview URLs
- `modOperations.ts` - Call `bustPreviewCache()` in `reloadCurrentPreview()`

---

### 2. Preview Delete with Renumbering

**Problem:**
User workflow that reproduced the bug:
1. Load mod → auto-imports `preview1.png`
2. Paste clipboard → creates `preview2.png`
3. Set preview2 as thumbnail → swaps: clipboard→preview1, original→preview2
4. Delete preview1 → leaves only `preview2.png`
5. Paste clipboard again → import sees 1 existing preview, creates `preview2.png` **overwriting original!**

**Root Cause:**
- `DeletePreviewAsync` only deleted the file without renumbering
- `ImportPreviewFromClipboardAsync` uses `existingPreviews.Count + 1` for next filename
- Gap in numbering caused overwrites

**Solution:**
After deleting a preview, renumber all subsequent previews to fill the gap.

**Example:**
- Before: `[preview1, preview2, preview3, preview4]`
- Delete preview2
- After: `[preview1, preview2, preview3]` (old preview3→preview2, old preview4→preview3)

#### Implementation

**File:** `D3dxSkinManager\Modules\Context\Services\ImageService.cs` (line 573-634)

```csharp
public async Task<bool> DeletePreviewAsync(string id, string previewPath)
{
    // 1. Get all previews and find deleted index
    var allPreviews = await GetPreviewPathsAsync(id);
    var deletedIndex = allPreviews.FindIndex(/*...*/);

    // 2. Delete the target file
    File.Delete(absolutePreviewPath);

    // 3. Renumber all previews after deleted one
    for (int i = deletedIndex + 1; i < allPreviews.Count; i++)
    {
        var currentPath = /* absolute path of preview at index i */;
        var currentExtension = Path.GetExtension(currentPath);

        // New filename is one number lower (fill the gap)
        var newFileName = $"preview{i}{currentExtension}";
        var newPath = Path.Combine(previewDirectory, newFileName);

        File.Move(currentPath, newPath);
        // Invalidate both old and new paths in cache
    }

    // 4. Invalidate cache and emit event
    _schemeHandler.InvalidatePaths(pathsToInvalidate);
    await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_DELETED, /*...*/);
}
```

**Key Features:**
- Preserves file extensions during renumbering
- Invalidates cache for all affected paths
- Emits PREVIEW_DELETED event
- Maintains sequential numbering (no gaps)

---

### 3. ModEnrichmentService Path Population

**Enhancement:** Added `CachePath` and `PreviewFolderPath` to `ModInfo` for frontend use.

**File:** `D3dxSkinManager\Modules\Mod\Services\ModEnrichmentService.cs`

```csharp
// Populate file paths (line 94-103)
if (mod.HasCache)
{
    mod.CachePath = Path.Combine(_profilePaths.CacheModsDirectory, mod.Id);
}

if (mod.HasPreviewFolder)
{
    mod.PreviewFolderPath = _profilePaths.GetPreviewDirectoryPath(mod.Id);
}
```

**Frontend Type:** `D3dxSkinManager.Client\src\shared\types\mod.types.ts`

```typescript
export interface ModInfo {
  // ... existing properties ...
  cachePath?: string;          // Absolute path to cache directory
  previewFolderPath?: string;  // Absolute path to preview directory
}
```

---

## Testing

### Manual Testing Flow

1. **Cache Busting Test:**
   - Load mod with preview
   - Set different image as thumbnail
   - Verify UI shows reordered images immediately (no browser cache)

2. **Delete Renumbering Test:**
   - Load mod → auto-imports preview1
   - Paste clipboard → creates preview2
   - Set preview2 as thumbnail
   - Delete preview1
   - Paste again → should create preview2 (not overwrite preview1)

### Automated Tests

**File:** `D3dxSkinManager.Tests\Modules\Context\Services\ImageServiceTests.cs`

Created comprehensive test suite covering:
- ✅ Delete middle preview with renumbering
- ✅ Delete last preview (no renumbering needed)
- ✅ Delete first preview (renumber all)
- ✅ Preserve extensions during renumbering
- ✅ File not found error handling
- ✅ Full bug reproduction scenario (paste-delete-paste)

**Test Count:** 6 test cases
**Coverage:** DeletePreviewAsync core logic + edge cases

---

## Files Modified

### Backend
- ✅ `CustomSchemeHandler.cs` - Query parameter stripping
- ✅ `ImageService.cs` - Delete with renumbering
- ✅ `ModEnrichmentService.cs` - Path population
- ✅ `ModInfo.cs` - Added CachePath/PreviewFolderPath properties

### Frontend
- ✅ `imageUrlHelper.ts` - Timestamp parameter support
- ✅ `PreviewImageCarousel.tsx` - Pass timestamp to URLs
- ✅ `ModPreviewPanel.tsx` - Pass timestamp to fullscreen
- ✅ `modOperations.ts` - Cache busting on reload
- ✅ `mod.types.ts` - Added path properties to ModInfo
- ✅ `PreviewImageContextMenu.tsx` - Use new path properties

### Tests
- ✅ `ImageServiceTests.cs` - New test file with 6 test cases

---

## Build Status

**Backend:** ✅ Build succeeded
**Frontend:** ✅ Build succeeded
**Tests:** ⚠️ Pre-existing test failures (unrelated to changes)
**Production Build:** ✅ Completed successfully

---

## Architecture Compliance

### Event-Driven Pattern ✅
- ImageService emits `PREVIEW_DELETED` event
- ModProvider subscribes and calls `reloadCurrentPreview()`
- Cache busting integrated into reload flow

### Module Boundaries ✅
- ImageService handles file operations
- CustomSchemeHandler handles URL processing
- No cross-module repository access

### Error Handling ✅
- `FileNotFoundException` for missing files
- Path validation before operations
- Cache invalidation on all operations

---

## Performance Impact

**Minimal:**
- Query parameter parsing: O(n) where n = URL length
- File renumbering: O(n) where n = number of previews after deleted index
- Typical case: 2-4 previews → negligible impact

**Cache Optimization:**
- CustomSchemeHandler already caches paths
- Query parameters stripped before cache lookup
- No additional cache entries created

---

## Future Improvements

1. **Batch Delete:** Support deleting multiple previews with single renumber operation
2. **Undo Support:** Implement preview operation history
3. **Preview Reordering:** Allow manual reordering without thumbnail setting
4. **Test Infrastructure:** Fix pre-existing test failures to enable CI/CD

---

## Related Issues

**Bug Report:** User-reported issue with preview deletion causing image overwrites
**Discovered During:** ModPreviewPanel refactoring and reactive state management implementation

---

## Documentation Updates

- ✅ Created `CHANGELOG_2026-03-06_PREVIEW_CACHE_BUSTING.md`
- ✅ Added comprehensive test documentation
- ✅ Updated inline code comments

---

## Verification Checklist

- [x] Backend builds successfully
- [x] Frontend builds successfully
- [x] Production build succeeds
- [x] Test cases added for new functionality
- [x] Documentation updated
- [x] No breaking changes to API
- [x] Event-driven pattern maintained
- [x] Module boundaries respected
- [x] Error handling in place

---

**Status:** ✅ Complete and Ready for Testing
**Next Steps:** Manual testing by user to verify bug fix
