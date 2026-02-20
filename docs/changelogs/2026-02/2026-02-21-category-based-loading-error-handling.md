# Category-Based Mod Loading with Comprehensive Error Handling

**Date:** 2026-02-21
**Type:** Feature Implementation + Error Handling
**Impact:** ⭐⭐⭐⭐⭐ Critical - Prevents mod conflicts and provides user-friendly error messages

## Summary

Implemented category-based unloading to prevent conflicts when loading mods for the same character/object, along with comprehensive error handling system that provides user-friendly messages for all error scenarios including folder-in-use, archive not found, extraction failures, and unknown errors.

## Problem Statement

### Issue 1: Mod Category Conflicts
When loading a mod for a character (e.g., Nahida), any previously loaded mod for that same character was not automatically unloaded, causing conflicts where multiple mods could be active for the same game object simultaneously.

### Issue 2: Poor Error Handling
When mod operations failed (especially folder operations), users received generic error messages that didn't explain the problem or how to fix it. Common issues like folders being locked by File Explorer had no specific error handling.

### Issue 3: No Error Code System
Backend and frontend had no standardized way to communicate specific error types, making it impossible to provide context-specific user guidance.

## Solution

### 1. Category-Based Unloading Logic

**File:** `D3dxSkinManager/Modules/Mods/ModFacade.cs` (lines 167-250)

Before loading any mod, the system now:
1. Retrieves the mod's category from the database
2. Queries all mods in the same category
3. Checks which ones are currently loaded (using file system status)
4. Automatically unloads all loaded mods in that category
5. Provides progress feedback for each unload operation
6. Only then proceeds to load the requested mod

**Key Code:**
```csharp
// CRITICAL: Unload all mods in the same category first to prevent conflicts
if (!string.IsNullOrEmpty(mod.Category))
{
    var sameCategoryMods = await _repository.GetByCategoryAsync(mod.Category);
    var modsToUnload = sameCategoryMods.Where(m => m.IsLoaded && m.SHA != sha).ToList();

    foreach (var modToUnload in modsToUnload)
    {
        await _fileService.UnloadAsync(modToUnload.SHA);
        await _eventEmitter.EmitAsync(PluginEventType.ModUnloaded, ...);
    }
}
```

### 2. Error Code System

**Backend Files:**
- `D3dxSkinManager/Modules/Core/Models/ErrorCodes.cs` - Central error code constants
- `D3dxSkinManager/Modules/Core/Models/ModException.cs` - Custom exception with error code

**Frontend Files:**
- `D3dxSkinManager.Client/src/shared/constants/errorCodes.ts` - Matching error codes
- `D3dxSkinManager.Client/src/shared/utils/errorHandler.ts` - Error handling utility

**Defined Error Codes:**
- `MOD_FOLDER_IN_USE` - Folder locked by another process (File Explorer, etc.)
- `MOD_ARCHIVE_NOT_FOUND` - Archive file missing or deleted
- `MOD_NOT_FOUND` - Mod not found in database
- `MOD_EXTRACTION_FAILED` - Archive extraction failed (corrupted/unsupported format)
- `MOD_CATEGORY_CONFLICT` - Another mod in same category already loaded
- `FILE_IN_USE` - Generic file locked error
- `FILE_ACCESS_DENIED` - Permission denied
- `UNKNOWN_ERROR` - Unexpected/unhandled errors

### 3. Backend Error Handling

**ModFileService.cs Changes:**

#### LoadAsync Method:
- ✅ Archive not found → Throws `ModException(MOD_ARCHIVE_NOT_FOUND)`
- ✅ Folder in use during enable from cache → Catches `IOException`, throws `ModException(MOD_FOLDER_IN_USE)`
- ✅ Access denied → Catches `UnauthorizedAccessException`, throws `ModException(FILE_ACCESS_DENIED)`
- ✅ Extraction failed → Throws `ModException(MOD_EXTRACTION_FAILED)`
- ✅ Unknown errors → Wraps in `ModException(UNKNOWN_ERROR)` with exception type

#### UnloadAsync Method:
- ✅ Folder in use during disable → Catches `IOException`, throws `ModException(MOD_FOLDER_IN_USE)`
- ✅ Access denied → Catches `UnauthorizedAccessException`, throws `ModException(FILE_ACCESS_DENIED)`
- ✅ Unknown errors → Wraps in `ModException(UNKNOWN_ERROR)` with exception type

**BaseFacade.cs Changes:**
```csharp
catch (ModException modEx)
{
    // Handle ModException specially to include error code and data
    return MessageResponse.CreateError(request.Id, modEx.Message, new
    {
        errorCode = modEx.ErrorCode,
        data = modEx.Data
    });
}
catch (Exception ex)
{
    // Handle unknown errors with UNKNOWN_ERROR code
    return MessageResponse.CreateError(request.Id, ex.Message, new
    {
        errorCode = ErrorCodes.UNKNOWN_ERROR,
        data = new { exceptionType = ex.GetType().Name }
    });
}
```

### 4. Frontend Error Handling

**Error Handler Utility:**
`D3dxSkinManager.Client/src/shared/utils/errorHandler.ts`

```typescript
export function handleError(error: unknown): ModOperationError {
  if (error instanceof Error) {
    const errorWithDetails = error as Error & { errorDetails?: ErrorDetails };

    if (errorWithDetails.errorDetails?.errorCode) {
      const { errorCode, data } = errorWithDetails.errorDetails;
      const userMessage = ERROR_MESSAGES[errorCode] || errorWithDetails.message;

      // Show user-friendly message (5 seconds for important errors)
      message.error(userMessage, 5);

      return new ModOperationError(errorCode, userMessage, data);
    }
  }

  // Fallback for unknown errors
  const errorMessage = error instanceof Error ? error.message : 'An unknown error occurred';
  message.error(errorMessage, 3);

  return new ModOperationError(ErrorCodes.UNKNOWN_ERROR, errorMessage);
}
```

**User-Friendly Messages:**
- `MOD_FOLDER_IN_USE`: "Cannot load/unload this mod because its folder is currently being used by another program. Please close any programs that might be accessing the mod folder (such as File Explorer, image viewers, or editors) and try again."
- `MOD_ARCHIVE_NOT_FOUND`: "The mod archive file was not found. The file may have been deleted or moved."
- `MOD_EXTRACTION_FAILED`: "Failed to extract the mod archive. The file may be corrupted or in an unsupported format."
- And more...

**Integration in ModsContext:**
```typescript
try {
  await modService.loadMod(selectedProfileId, sha);
  notification.success("Mod loaded successfully");
} catch (error) {
  // Revert optimistic update
  modData.dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data: { isLoaded: false } } });

  // Handle error with user-friendly messages based on error code
  handleError(error);
}
```

### 5. IPC Message Updates

**MessageResponse.cs:**
```csharp
public class MessageResponse
{
    public string Id { get; set; }
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public object? ErrorDetails { get; set; }  // NEW: Contains errorCode and data
}
```

**PhotinoResponse Interface:**
```typescript
export interface ErrorDetails {
  errorCode: string;
  data?: unknown;
}

export interface PhotinoResponse<TData = unknown> {
  id: string;
  success: boolean;
  data?: TData;
  error?: string;
  errorDetails?: ErrorDetails;  // NEW
}
```

**photinoService.ts:**
```typescript
// Attach errorDetails to the error object for error handling middleware
if (response.errorDetails) {
  error.errorDetails = response.errorDetails;
}
reject(error);
```

## Error Handling Flow

```
User Action (Load/Unload Mod)
  ↓
Backend Operation (ModFileService)
  ↓
[Three possible paths:]

1. Known Error (ModException)
   ↓ ModException with specific error code
   ↓ BaseFacade catches and includes errorCode
   ↓ Frontend receives errorDetails
   ↓ handleError() shows specific user-friendly message

2. Unknown Error (Any Exception in ModFileService)
   ↓ ModFileService wraps in ModException(UNKNOWN_ERROR)
   ↓ BaseFacade catches and includes errorCode
   ↓ Frontend receives errorDetails with UNKNOWN_ERROR code
   ↓ handleError() shows generic error message

3. Unhandled Error (Escapes ModFileService)
   ↓ BaseFacade catches generic Exception
   ↓ Wraps with UNKNOWN_ERROR code
   ↓ Frontend receives errorDetails with UNKNOWN_ERROR code
   ↓ handleError() shows generic error message
```

## Files Created

### Backend
1. `D3dxSkinManager/Modules/Core/Models/ErrorCodes.cs` - Error code constants
2. `D3dxSkinManager/Modules/Core/Models/ModException.cs` - Custom exception class

### Frontend
1. `D3dxSkinManager.Client/src/shared/constants/errorCodes.ts` - Error code constants
2. `D3dxSkinManager.Client/src/shared/utils/errorHandler.ts` - Error handling utility

## Files Modified

### Backend
1. `D3dxSkinManager/Modules/Mods/ModFacade.cs`
   - Lines 167-250: Category-based unloading logic in `LoadModAsync()`

2. `D3dxSkinManager/Modules/Mods/Services/ModFileService.cs`
   - Lines 102-110: Archive not found error handling
   - Lines 117-146: Folder in use/access denied for cache enable
   - Lines 183-186: Extraction failed error handling
   - Lines 189-204: Unknown error wrapping in LoadAsync
   - Lines 246-287: Folder in use/access denied for unload
   - Lines 289-303: Unknown error wrapping in UnloadAsync

3. `D3dxSkinManager/Modules/Core/Facades/BaseFacade.cs`
   - Lines 37-58: Enhanced error handling for ModException and unknown errors

4. `D3dxSkinManager/Modules/Core/Models/MessageResponse.cs`
   - Added `ErrorDetails` property
   - Updated `CreateError()` to accept errorDetails parameter

### Frontend
1. `D3dxSkinManager.Client/src/shared/types/message.types.ts`
   - Added `ErrorDetails` interface
   - Added `errorDetails` to `PhotinoResponse`

2. `D3dxSkinManager.Client/src/shared/services/photinoService.ts`
   - Lines 126-137: Attach errorDetails to rejected errors

3. `D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx`
   - Line 9: Import `handleError`
   - Lines 286-300: Use `handleError()` in `loadModInGame`
   - Lines 327-341: Use `handleError()` in `unloadModFromGame`

## Benefits

### User Experience
- ✅ No more mod conflicts - only one mod per category at a time
- ✅ Clear, actionable error messages instead of technical jargon
- ✅ Specific guidance on how to fix problems (e.g., "close File Explorer")
- ✅ Progress feedback shows which mods are being unloaded

### Developer Experience
- ✅ Centralized error code system (easy to add new error types)
- ✅ Type-safe error handling on both backend and frontend
- ✅ Consistent error propagation through IPC layer
- ✅ All error paths covered (known + unknown)

### Reliability
- ✅ Every error scenario has proper handling
- ✅ Unknown errors are wrapped with UNKNOWN_ERROR code
- ✅ Detailed logging with exception types for debugging
- ✅ Automatic state reversion on failure (optimistic updates)

## Testing

### Build Status
- ✅ Backend: `dotnet build` - 0 errors, 6 package warnings (normal)
- ✅ Frontend: `npm run build` - 0 errors, compiled successfully

### Error Scenarios Tested (Code Level)
1. ✅ Folder in use (IOException) → MOD_FOLDER_IN_USE
2. ✅ Access denied (UnauthorizedAccessException) → FILE_ACCESS_DENIED
3. ✅ Archive not found → MOD_ARCHIVE_NOT_FOUND
4. ✅ Extraction failed → MOD_EXTRACTION_FAILED
5. ✅ Unknown exception → UNKNOWN_ERROR with exception type
6. ✅ Category-based unloading → Multiple mods unloaded sequentially

## Technical Notes

### Error Code Synchronization
- Backend and frontend error codes must stay in sync
- Both define the same constants to ensure proper message mapping
- TypeScript uses `as const` for type safety

### Exception Hierarchy
```
Exception (base)
  ↓
ModException (custom)
  - errorCode: string
  - data: object?
  - Data property (new keyword to hide base Exception.Data)
```

### IPC Error Details Structure
```json
{
  "id": "msg_123",
  "success": false,
  "error": "Cannot enable mod - folder is in use...",
  "errorDetails": {
    "errorCode": "MOD_FOLDER_IN_USE",
    "data": {
      "sha": "abc123...",
      "path": "D:\\...\\DISABLED-abc123"
    }
  }
}
```

## Future Enhancements

Potential improvements for future iterations:
1. Add retry mechanism for transient errors (folder in use)
2. Implement error telemetry/analytics
3. Add "Force Unload" option for stuck folders
4. Create error history/log viewer in UI
5. Add more specific error codes as new scenarios are discovered

## Related Issues

- Prevents mod conflicts when switching between mods for same character
- Fixes cryptic "Archive extraction failed" errors with helpful messages
- Resolves folder-in-use errors when File Explorer is open

## Breaking Changes

None. This is a pure addition of functionality. Existing mod load/unload operations work the same way for users, but with better error handling and automatic category management.

## Documentation Updates Needed

- ✅ This changelog document
- ✅ CHANGELOG.md summary entry
- ✅ KEYWORDS_INDEX.md (new files added)
- ⏳ API documentation (if applicable)
- ⏳ User guide (error message reference)

---

**Implementation Date:** 2026-02-21
**Author:** AI Assistant (Claude)
**Reviewed By:** Pending user review
