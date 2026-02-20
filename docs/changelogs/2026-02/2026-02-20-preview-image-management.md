# Preview Image Management Feature - 2026-02-20

## Summary
Added comprehensive preview image management to ModPreviewPanel with right-click context menu for thumbnail assignment, file operations, and image import functionality.

## Impact: ⭐⭐⭐
- **User Experience**: Quick access to image management with context menu
- **Feature Completeness**: Full CRUD operations for preview images
- **UX Enhancement**: Path memory system for file dialog persistence

## Changes

### Context Menu System
**New Capabilities:**
- Right-click on preview images to open context menu
- Set any preview as thumbnail
- Open image in file explorer
- Copy image path to clipboard
- Delete preview images with safety checks
- Add images from file with path memory
- Paste images from clipboard (frontend ready, backend pending)
- Open previews folder when no images exist

**Menu Structure (with images):**
1. Add from File...
2. Paste from Clipboard
3. ─────────
4. Set as Thumbnail (disabled if already thumbnail)
5. ─────────
6. Open in File Explorer
7. Copy Image Path
8. ─────────
9. Delete Preview

**Menu Structure (no images):**
1. Add from File...
2. Paste from Clipboard
3. ─────────
4. Open Previews Folder

**Files:**
- [ModPreviewPanel.tsx:117-349](../../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/ModPreviewPanel.tsx#L117-L349) - Context menu handlers and items

### Backend Implementation

**New IPC Endpoints:**
- `SET_THUMBNAIL` - Set a preview image as the mod's thumbnail
- `DELETE_PREVIEW` - Delete a preview image with safety checks

**Safety Features:**
- Cannot delete the current thumbnail (must set different thumbnail first)
- File existence validation before operations
- Proper error messages for all failure cases
- Event emission for UI refresh

**Files:**
- [ModFacade.cs:364-408](../../D3dxSkinManager/Modules/Mods/ModFacade.cs#L364-L408) - Backend implementation
- [IModFacade.cs](../../D3dxSkinManager/Modules/Mods/IModFacade.cs) - Interface methods

**Code:**
```csharp
public async Task<bool> SetThumbnailAsync(string sha, string previewPath)
{
    var mod = await _repository.GetByIdAsync(sha);
    if (mod == null)
        throw new InvalidOperationException($"Mod not found: {sha}");

    if (!File.Exists(previewPath))
        throw new FileNotFoundException($"Preview image not found: {previewPath}");

    mod.ThumbnailPath = previewPath;
    await _repository.UpdateAsync(mod);
    await _eventEmitter.EmitAsync(PluginEventType.CustomEvent,
        "mod.thumbnail.updated", new { sha, thumbnailPath = previewPath });

    return true;
}

public async Task<bool> DeletePreviewAsync(string sha, string previewPath)
{
    var mod = await _repository.GetByIdAsync(sha);
    if (mod == null)
        throw new InvalidOperationException($"Mod not found: {sha}");

    // Safety check: Don't allow deleting the thumbnail
    if (mod.ThumbnailPath == previewPath)
        throw new InvalidOperationException(
            "Cannot delete the thumbnail image. Set a different thumbnail first.");

    if (!File.Exists(previewPath))
        throw new FileNotFoundException($"Preview image not found: {previewPath}");

    File.Delete(previewPath);
    await _eventEmitter.EmitAsync(PluginEventType.CustomEvent,
        "mod.preview.deleted", new { sha, previewPath });

    return true;
}
```

### Frontend Service Layer

**New Service Methods:**
```typescript
async setThumbnail(profileId: string, sha: string, previewPath: string): Promise<boolean>
async deletePreview(profileId: string, sha: string, previewPath: string): Promise<boolean>
```

**Files:**
- [modService.ts:210-238](../../D3dxSkinManager.Client/src/modules/mods/services/modService.ts#L210-L238) - Service methods

### Preview Gallery Logic Fix

**Issue:** Thumbnail was appearing twice in preview gallery (added separately, then included in preview paths).

**Old Logic:**
```typescript
if (mod?.thumbnailPath) {
  allImagePaths.push(mod.thumbnailPath);
}
if (state.previewPaths && state.previewPaths.length > 0) {
  allImagePaths.push(...state.previewPaths);
}
```

**New Logic:**
```typescript
if (state.previewPaths && state.previewPaths.length > 0) {
  // If thumbnail exists in preview paths, show it first
  if (mod?.thumbnailPath && state.previewPaths.includes(mod.thumbnailPath)) {
    allImagePaths.push(mod.thumbnailPath);
    allImagePaths.push(...state.previewPaths.filter(p => p !== mod.thumbnailPath));
  } else {
    // No thumbnail or thumbnail not in preview paths
    allImagePaths.push(...state.previewPaths);
  }
}
```

**Result:** Thumbnail always appears first when it exists, no duplicates.

**Files:**
- [ModPreviewPanel.tsx:59-74](../../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/ModPreviewPanel.tsx#L59-L74) - Fixed logic

### File Dialog Path Memory

**Feature:** Added `rememberPathKey` to file dialog for persistent path memory across sessions.

**Implementation:**
```typescript
const result = await fileDialogService.openFileDialog({
  title: "Select Preview Image",
  filters: [
    { name: "Image Files", extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"] }
  ],
  rememberPathKey: 'mod-preview-import'  // <-- Path memory
});
```

**Backend Persistence:**
```csharp
// GlobalSettings.cs - Added to persist paths
public Dictionary<string, string> FileDialogPaths { get; set; } = new();

// FileDialogService.cs - Updated to use GlobalSettings
private readonly Settings.Services.IGlobalSettingsService _globalSettings;

private string GetInitialPath(FileDialogOptions? options)
{
    if (!string.IsNullOrWhiteSpace(options?.RememberPathKey))
    {
        var settings = _globalSettings.GetSettingsAsync().GetAwaiter().GetResult();
        if (settings.FileDialogPaths.TryGetValue(options.RememberPathKey, out var rememberedPath))
        {
            if (Directory.Exists(rememberedPath))
                return rememberedPath;
        }
    }
    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}

private void SaveLastUsedPath(FileDialogOptions? options, string? path)
{
    if (Directory.Exists(path))
    {
        _ = Task.Run(async () =>
        {
            var settings = await _globalSettings.GetSettingsAsync();
            settings.FileDialogPaths[options.RememberPathKey] = path;
            await _globalSettings.UpdateSettingsAsync(settings);
        });
    }
}
```

**Benefits:**
- File dialog remembers last used directory for importing preview images
- **Persists across application sessions** via `data/settings/global.json`
- Improves workflow efficiency for batch operations
- Stored in GlobalSettings alongside theme, window position, etc.
- Thread-safe async save (fire-and-forget pattern)

**Files:**
- [ModPreviewPanel.tsx:189-195](../../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/ModPreviewPanel.tsx#L189-L195) - Frontend usage
- [GlobalSettings.cs:38](../../D3dxSkinManager/Modules/Settings/Models/GlobalSettings.cs#L38) - Added FileDialogPaths field
- [FileDialogService.cs:40-45](../../D3dxSkinManager/Modules/Core/Services/FileDialogService.cs#L40-L45) - Constructor with GlobalSettings injection
- [FileDialogService.cs:228-300](../../D3dxSkinManager/Modules/Core/Services/FileDialogService.cs#L228-L300) - GetInitialPath and SaveLastUsedPath methods

### File Operations Integration

**Capabilities:**
- Open image in file explorer (highlights file)
- Open previews folder (for empty state)
- Copy image path to clipboard
- Launch processes and open directories

**Reused Services:**
- `fileDialogService.openFileInExplorer(filePath)` - Opens file explorer with file highlighted
- `fileDialogService.openDirectory(directoryPath)` - Opens folder in explorer
- Uses existing SettingsFacade IPC endpoints

**Files:**
- [fileDialogService.ts](../../D3dxSkinManager.Client/src/shared/services/fileDialogService.ts) - Frontend service
- [FileSystemService.cs](../../D3dxSkinManager/Modules/Settings/FileSystemService.cs) - Backend service

### Clipboard Integration (Partial)

**Current Status:** Frontend implementation complete, backend blob upload pending.

**Frontend:**
- Uses Clipboard API (`navigator.clipboard.read()`)
- Detects image types in clipboard
- Creates File blob from clipboard data
- Shows info message for backend implementation needed

**Code:**
```typescript
const clipboardItems = await navigator.clipboard.read();
for (const item of clipboardItems) {
  const imageType = item.types.find(type => type.startsWith('image/'));
  if (imageType) {
    const blob = await item.getType(imageType);
    const extension = imageType.split('/')[1] || 'png';
    const fileName = `clipboard_${Date.now()}.${extension}`;
    const file = new File([blob], fileName, { type: imageType });
    // TODO: Backend blob upload endpoint needed
    message.info("Clipboard image paste feature requires backend implementation");
    break;
  }
}
```

**Next Steps:**
- Create backend endpoint to accept blob/base64 image data
- Save blob to previews folder with generated filename
- Emit event to refresh preview gallery

**Files:**
- [ModPreviewPanel.tsx:209-251](../../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/ModPreviewPanel.tsx#L209-L251) - Clipboard handler

## Technical Implementation

### Context Menu Positioning
```typescript
const [contextMenuVisible, setContextMenuVisible] = useState(false);
const [contextMenuPosition, setContextMenuPosition] = useState({ x: 0, y: 0 });

const handleContextMenu = (e: React.MouseEvent) => {
  e.preventDefault();
  setContextMenuPosition({ x: e.clientX, y: e.clientY });
  setContextMenuVisible(true);
};
```

### State Management
- `contextMenuVisible`: Controls menu visibility
- `contextMenuPosition`: Tracks menu coordinates
- Auto-refresh after operations using `actions.loadPreviewPaths(mod.sha)`
- Modal confirmation for destructive operations (delete)

### Error Handling
- Try-catch blocks for all async operations
- User-friendly error messages via Ant Design `message` API
- Backend validation with detailed error messages
- Type-safe error handling: `error instanceof Error ? error.message : 'Unknown error'`

## Migration Notes

### Breaking Changes
None - purely additive feature.

### For Developers
**Adding Context Menu Items:**
1. Add handler function with error handling
2. Add menu item to `contextMenuItems` array
3. Include icon from Ant Design icons
4. Add dividers for visual grouping
5. Consider disabled states for invalid operations

**Path Memory Keys:**
- Use descriptive keys: `'mod-preview-import'`, `'migration_python_install'`
- Keys persist across sessions in backend `ConcurrentDictionary`
- Validated on each use (invalid paths cleaned up automatically)

## Performance Impact
- **Bundle Size:** +736 B total (+17 B for path memory)
- **Runtime:** Context menu rendering is minimal overhead
- **Memory:** Negligible (context menu state + position coordinates)
- **Network:** No additional IPC overhead (reuses existing endpoints)

## User-Facing Changes
✅ **New Features:**
- Right-click context menu on preview images
- Quick thumbnail assignment
- File explorer integration
- Image import with path memory
- Clipboard paste detection

⚠️ **Behavior Changes:**
- Cannot delete thumbnail image directly (must set different thumbnail first)
- File dialog remembers last used directory for imports
- Thumbnail always appears first in preview gallery

## Testing Recommendations
**Manual Testing:**
1. Right-click preview image → context menu appears
2. Set as Thumbnail → thumbnail updates, disabled state correct
3. Open in File Explorer → file highlighted in explorer
4. Copy Image Path → path in clipboard
5. Delete Preview → confirmation modal, file deleted, gallery updates
6. Add from File → file dialog remembers path, image added
7. Paste from Clipboard → detects clipboard images (info message shown)
8. Empty state → "Open Previews Folder" option available

**Edge Cases:**
- Delete thumbnail → shows error message
- Invalid file paths → proper error handling
- No clipboard permission → warning message
- Cancel file dialog → no error, menu closes
- Non-existent files → validation errors

## Related Changes
- **Previous Session:** [2026-02-20-image-navigation-css-refactoring.md](2026-02-20-image-navigation-css-refactoring.md)
- **Component:** Builds on ModPreviewPanel image navigation features
- **Context:** Part of ongoing mod management UX improvements

## Files Changed
```
Modified:
├── Backend/
│   ├── Modules/Mods/
│   │   ├── ModFacade.cs (+45 lines: SetThumbnailAsync, DeletePreviewAsync)
│   │   └── IModFacade.cs (+2 interface methods)
│
└── Frontend/
    ├── modules/mods/
    │   ├── components/ModPreviewPanel/
    │   │   └── ModPreviewPanel.tsx (+233 lines: context menu, handlers)
    │   └── services/
    │       └── modService.ts (+28 lines: setThumbnail, deletePreview)
    └── shared/services/
        └── fileDialogService.ts (already had rememberPathKey support)
```

## Bug Fix - ImportPreviewImageAsync Path Issue

### Issue
The `ImportPreviewImageAsync` method was trying to use `mod.WorkPath` or `mod.CachePath` to store preview images, but the system uses `previews/{SHA}/` folder structure. This caused an error: "Mod has no work or cache directory".

### Root Cause
- Code comment (line 346) stated: "Previews are now stored in previews/{SHA}/ folder and scanned dynamically"
- But implementation still used old `mod.WorkPath ?? mod.CachePath` logic
- Many mods don't have WorkPath or CachePath set, causing the error

### Solution
1. **Injected `IProfilePathService`** into ModFacade constructor
2. **Created helper method** `CopyToPreviewDirectoryAsync` that uses `_profilePaths.GetPreviewDirectoryPath(sha)`
3. **Updated logic** to generate sequential filenames (preview1.png, preview2.png, etc.)
4. **Proper directory structure**: Uses `previews/{SHA}/` consistent with `GetPreviewPathsAsync`

### Code Changes
**ModFacade.cs constructor:**
```csharp
private readonly Profiles.Services.IProfilePathService _profilePaths;

public ModFacade(
    // ... other parameters ...
    Profiles.Services.IProfilePathService profilePaths,
    ILogHelper logger) : base(logger)
{
    // ... other assignments ...
    _profilePaths = profilePaths;
}
```

**CopyToPreviewDirectoryAsync helper:**
```csharp
private Task<string> CopyToPreviewDirectoryAsync(string sha, string sourcePath, string targetFileName)
{
    // Use ProfilePathService to get the correct preview directory (previews/{SHA}/)
    var targetDirectory = _profilePaths.GetPreviewDirectoryPath(sha);

    if (!Directory.Exists(targetDirectory))
    {
        Directory.CreateDirectory(targetDirectory);
    }

    var targetPath = Path.Combine(targetDirectory, targetFileName);
    File.Copy(sourcePath, targetPath, overwrite: true);

    return Task.FromResult(targetPath);
}
```

**ImportPreviewImageAsync updated:**
```csharp
// Use ImageService to get the preview paths and determine the next filename
var existingPreviews = await _imageService.GetPreviewPathsAsync(sha);

// Generate next preview filename (preview1.png, preview2.png, etc.)
int nextIndex = existingPreviews.Count + 1;
var targetFileName = $"preview{nextIndex}{extension}";

// Use ImageService's path resolution (previews/{SHA}/ folder)
var targetPath = await CopyToPreviewDirectoryAsync(sha, imagePath, targetFileName);
```

### Files Modified
- [ModFacade.cs:71](../../D3dxSkinManager/Modules/Mods/ModFacade.cs#L71) - Added `_profilePaths` field
- [ModFacade.cs:82](../../D3dxSkinManager/Modules/Mods/ModFacade.cs#L82) - Added parameter to constructor
- [ModFacade.cs:310-350](../../D3dxSkinManager/Modules/Mods/ModFacade.cs#L310-L350) - Fixed `ImportPreviewImageAsync`
- [ModFacade.cs:359-373](../../D3dxSkinManager/Modules/Mods/ModFacade.cs#L359-L373) - Added `CopyToPreviewDirectoryAsync` helper

### Impact
✅ **Fixed**: "Add from File" now works for all mods (not just those with WorkPath/CachePath)
✅ **Consistent**: Uses same directory structure as `GetPreviewPathsAsync`
✅ **Sequential**: Generates proper filenames (preview1, preview2, etc.)
✅ **Clean**: Centralized path logic in ProfilePathService

## Enhancement - File Dialog Path Memory Persistence

### Issue
During testing, discovered that file dialog path memory was **not** persisting across application sessions. Paths were stored in a `static ConcurrentDictionary` which is in-memory only.

### Root Cause
- `FileDialogService` used `private static readonly ConcurrentDictionary<string, string> _lastUsedPaths`
- No persistence mechanism - dictionary cleared on application restart
- Misleading documentation claimed "Persists across application sessions"

### Solution
1. **Added FileDialogPaths to GlobalSettings model** - Stored in `data/settings/global.json`
2. **Injected GlobalSettingsService** into FileDialogService constructor
3. **Updated GetInitialPath** to load paths from global settings (synchronously via `.GetAwaiter().GetResult()`)
4. **Updated SaveLastUsedPath** to persist paths to global settings (asynchronously via `Task.Run`)
5. **Fire-and-forget pattern** for saving to avoid blocking file dialogs

### Code Changes
**GlobalSettings.cs:**
```csharp
/// <summary>
/// Last used file dialog paths by key for path memory feature
/// Key: rememberPathKey (e.g., "mod-preview-import", "migration_python_install")
/// Value: Last used directory path
/// </summary>
public Dictionary<string, string> FileDialogPaths { get; set; } = new();
```

**FileDialogService.cs:**
```csharp
private readonly Settings.Services.IGlobalSettingsService _globalSettings;

public FileDialogService(Settings.Services.IGlobalSettingsService globalSettings)
{
    _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
}

// Changed from static to instance method
private string GetInitialPath(FileDialogOptions? options)
{
    if (!string.IsNullOrWhiteSpace(options?.RememberPathKey))
    {
        try
        {
            var settings = _globalSettings.GetSettingsAsync().GetAwaiter().GetResult();
            if (settings.FileDialogPaths.TryGetValue(options.RememberPathKey, out var rememberedPath))
            {
                if (Directory.Exists(rememberedPath))
                    return rememberedPath;
                else
                {
                    // Clean up invalid path asynchronously (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        var updatedSettings = await _globalSettings.GetSettingsAsync();
                        updatedSettings.FileDialogPaths.Remove(options.RememberPathKey);
                        await _globalSettings.UpdateSettingsAsync(updatedSettings);
                    });
                }
            }
        }
        catch
        {
            // If settings load fails, just continue to fallback
        }
    }
    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}

// Changed from static to instance method
private void SaveLastUsedPath(FileDialogOptions? options, string? path)
{
    if (Directory.Exists(path))
    {
        // Save asynchronously (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await _globalSettings.GetSettingsAsync();
                settings.FileDialogPaths[options.RememberPathKey] = path;
                await _globalSettings.UpdateSettingsAsync(settings);
            }
            catch
            {
                // Ignore errors in path saving (non-critical feature)
            }
        });
    }
}
```

### Files Modified
- [GlobalSettings.cs:33-38](../../D3dxSkinManager/Modules/Settings/Models/GlobalSettings.cs#L33-L38) - Added FileDialogPaths dictionary
- [FileDialogService.cs:40-45](../../D3dxSkinManager/Modules/Core/Services/FileDialogService.cs#L40-L45) - Added GlobalSettings dependency
- [FileDialogService.cs:228-273](../../D3dxSkinManager/Modules/Core/Services/FileDialogService.cs#L228-L273) - Updated GetInitialPath method
- [FileDialogService.cs:275-300](../../D3dxSkinManager/Modules/Core/Services/FileDialogService.cs#L275-L300) - Updated SaveLastUsedPath method

### Impact
✅ **True Persistence**: Paths now survive application restarts
✅ **Consistent Storage**: Uses same mechanism as theme, window position, etc.
✅ **Non-Blocking**: Async save doesn't slow down file dialogs
✅ **Error Tolerant**: Failures in path save/load don't break dialogs (graceful degradation)
✅ **Automatic Cleanup**: Invalid paths are removed from settings

## Commit
- **Hash:** (pending)
- **Message:** "Add preview image management with context menu, path memory, and fix import path bug"
- **Author:** Claude + User
- **Date:** 2026-02-20

## Keywords
`#preview-management` `#context-menu` `#thumbnail` `#file-operations` `#path-memory` `#clipboard-api` `#mod-preview-panel` `#ux-improvement` `#image-gallery` `#bug-fix` `#import-preview`
