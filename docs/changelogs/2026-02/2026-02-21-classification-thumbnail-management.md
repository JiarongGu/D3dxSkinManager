# Classification Thumbnail Management Implementation

**Date**: 2026-02-21
**Type**: Feature Implementation
**Impact**: ⭐⭐⭐⭐⭐ (High - Core classification feature completion)

## Overview

Implemented complete "Add Classification" feature with thumbnail support, SHA-256-based deduplication, comprehensive validation (frontend + backend), and file lock detection during deletion. Created reusable `FileTransferService` for managed file copying across the application.

## Changes Summary

### New Services Created

1. **FileTransferService** (`D3dxSkinManager/Modules/Core/Services/FileTransferService.cs`)
   - Reusable service for copying files with SHA-256 deduplication
   - Automatic collision prevention through hash-based naming
   - External file detection and validation
   - Returns relative paths for database storage

2. **HashService** (`D3dxSkinManager/Modules/Core/Services/HashService.cs`)
   - SHA-256 hash calculation for files
   - Used by FileTransferService for deduplication

### Frontend Changes

1. **ClassificationScreen.tsx**
   - Added thumbnail file picker integration
   - Implemented IPC-based async validation for duplicate names
   - Proper Ant Design form validation with `Promise.reject/resolve`
   - Thumbnail preview using `app://` custom scheme
   - Integration with `useProfile` for profileId access

2. **classificationService.ts**
   - Added `nodeExists()` IPC method for database validation
   - Documented `findNodeById` as local-only, `nodeExists` for validation

3. **ClassificationPanel.tsx**
   - Integration with Add Classification screen

### Backend Changes

1. **ClassificationService.cs**
   - Integrated FileTransferService for thumbnail copying
   - SHA-256-based thumbnail storage in `thumbnails/` directory
   - Changed deletion order: thumbnails BEFORE nodes
   - Enhanced error handling in `CleanupThumbnailIfUnusedAsync`:
     - Throws `InvalidOperationException` on file lock (IOException)
     - Throws `InvalidOperationException` on permission denied (UnauthorizedAccessException)
     - Stops deletion cleanly when file locks detected

2. **ModFacade.cs**
   - Added `CHECK_CLASSIFICATION_NODE_EXISTS` IPC handler
   - Created `CheckClassificationNodeExistsAsync` method
   - Enhanced `CreateClassificationNodeAsync` with duplicate validation
   - Throws descriptive errors that propagate to frontend

3. **CustomSchemeHandler.cs**
   - Removed overly restrictive data directory security check
   - Now serves files from anywhere on filesystem (safe for desktop app)
   - Enables thumbnail preview for external files

4. **CoreServiceExtensions.cs**
   - Registered FileTransferService in DI container
   - Registered HashService in DI container

### Translation Updates

- Added English translations for classification UI
- Added Chinese translations for classification UI

## Technical Details

### SHA-256 Deduplication Flow

1. User selects thumbnail file (any location)
2. FileTransferService calculates SHA-256 hash
3. Target filename: `{SHA256}.{extension}`
4. Checks if file already exists in thumbnails directory
5. If exists: skip copy (deduplication)
6. If not exists: copy file
7. Returns relative path: `thumbnails/{SHA256}.{extension}`

### Validation Flow (Frontend)

1. User types classification name
2. Ant Design form triggers async validator
3. Calls `classificationService.nodeExists(profileId, name)`
4. IPC message to backend: `CHECK_CLASSIFICATION_NODE_EXISTS`
5. Backend queries database via `ClassificationService.NodeExistsAsync`
6. Returns boolean result
7. Frontend displays inline error if duplicate exists

### Validation Flow (Backend)

1. User submits classification creation
2. Frontend sends IPC message: `CREATE_CLASSIFICATION_NODE`
3. Backend calls `ModFacade.CreateClassificationNodeAsync`
4. Checks `NodeExistsAsync` before creation
5. Throws `InvalidOperationException` if duplicate
6. Error propagates to frontend notification system

### Deletion with File Lock Detection

1. User requests classification deletion
2. Backend recursively deletes children first
3. **BEFORE** deleting node: attempts to delete thumbnail
4. If file locked: throws `InvalidOperationException` with descriptive message
5. Deletion stops cleanly
6. User sees error notification
7. If successful: proceeds to delete node from database

## Files Changed

### New Files
- `D3dxSkinManager/Modules/Core/Services/FileTransferService.cs`
- `D3dxSkinManager/Modules/Core/Services/HashService.cs`

### Modified Files
- `D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/ClassificationScreen.tsx`
- `D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/ClassificationPanel.tsx`
- `D3dxSkinManager.Client/src/shared/services/classificationService.ts`
- `D3dxSkinManager/Modules/Mods/Services/ClassificationService.cs`
- `D3dxSkinManager/Modules/Mods/ModFacade.cs`
- `D3dxSkinManager/Modules/Core/Services/CustomSchemeHandler.cs`
- `D3dxSkinManager/Modules/Core/CoreServiceExtensions.cs`
- `D3dxSkinManager/Languages/en.json`
- `D3dxSkinManager/Languages/cn.json`

## Benefits

1. **Data Integrity**: Dual validation (frontend + backend) prevents duplicate classifications
2. **Storage Efficiency**: SHA-256 deduplication prevents duplicate thumbnail storage
3. **User Experience**: Real-time validation feedback with Ant Design form UI
4. **Error Handling**: Clear error messages when files are locked or inaccessible
5. **Code Reusability**: FileTransferService can be used across application
6. **Portability**: Relative paths in database enable profile/data folder moves

## Testing Recommendations

1. **Duplicate Prevention**:
   - Try creating classification with existing name (should show inline error)
   - Try submitting anyway (should show backend error notification)

2. **Deduplication**:
   - Add same image as thumbnail for multiple classifications
   - Verify only one copy in thumbnails directory
   - Check all classifications display correctly

3. **File Lock Detection**:
   - Create classification with thumbnail
   - Open thumbnail in image viewer (locks file on Windows)
   - Try deleting classification
   - Verify error message shows and deletion stops

4. **External File Preview**:
   - Select thumbnail from Desktop or any external location
   - Verify preview shows correctly in Add Classification dialog
   - Verify file copies to thumbnails directory on save

## Related Documentation

- [features/CLASSIFICATION_SYSTEM.md](../features/CLASSIFICATION_SYSTEM.md) (if exists)
- [architecture/FILE_MANAGEMENT.md](../architecture/FILE_MANAGEMENT.md) (if exists)
- [how-to/ADD_CLASSIFICATION.md](../how-to/ADD_CLASSIFICATION.md) (if created)

## Future Improvements

1. Image cropping/resizing before storage
2. Thumbnail size limits (max dimension/file size)
3. Batch classification creation
4. Classification import/export
5. Thumbnail cache warming on startup
6. WebP conversion for better compression
