# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> Detailed changes are preserved in git history.

---

## [Unreleased]

### Added - 2026-03-08 - Active Mods View with Orphaned Mod Detection ⭐⭐⭐
**Summary**: Added "Show Loaded Mods" feature with cache-first scanning, orphaned mod detection, and IMemoryCache optimization for performance.

#### Active Mods View ("Show Loaded Mods" Button)
**Impact**: ✅ Users can now view all currently loaded mods in one click with instant subsequent loads
**Features**:
- Scans cache folder first, then matches with database
- Detects orphaned mods (in cache but not in database) for cleanup
- Displays orphaned mods as "Unmanaged [SHA]" with i18n support (EN/CN)
- Simplified context menu for orphaned mods (only "Open Cache Folder" and "Delete Cache")
- IMemoryCache caching with automatic invalidation on cache changes

**Backend Changes**:
- ModQueryService.cs: Added `GetActiveModsAsync()` with IMemoryCache caching
- ModQueryService.cs: Profile-specific cache key: `ActiveMods_{profileId}`
- ModQueryService.cs: Cache invalidated on CACHE_CHANGED event from ModCacheWatcher
- ModFacade.cs: Added GET_ACTIVE_MODS IPC endpoint
- ModInfo.cs: Added `IsOrphaned` property for frontend handling

**Frontend Changes**:
- CategoryPanel.tsx: Added "Show All Mods" and "Show Loaded Mods" icon buttons
- CategoryPanel.css: Split status bar into left (unclassified) and right (buttons) sections
- ModList.tsx: Orphaned mod display with i18n formatting
- ModList.tsx: Simplified context menu for orphaned mods (2 options only)
- modsStore.ts: Added ModListViewMode state ('category' | 'unclassified' | 'all' | 'loaded')
- categoryOperations.ts: Added loadAllMods() and loadLoadedMods()
- modOperations.ts: Updated refresh to respect view mode
- modService.ts: Added getActiveMods() IPC method

**Performance Optimization**:
- First call: Scans cache folder (slow) and stores in IMemoryCache
- Subsequent calls: Returns cached result instantly (fast)
- Cache automatically invalidated when mods are loaded/unloaded/deleted (FileSystemWatcher)

**Translation Updates**:
- English: "Unmanaged [{{sha}}]"
- Chinese: "未托管 [{{sha}}]"

**Documentation**:
- AI_GUIDE.md: Added IMemoryCache caching pattern section
- AI_GUIDE.md: Added FileSystemWatcher pattern section
- BACKEND.md: Added IMemoryCache usage pattern with code examples
- BACKEND.md: Updated ModQueryService entry with caching details

**Commits**: 9b810c4, 29a9b35

### Added - 2026-03-08 - Configurable Minimum Window and Panel Widths ⭐
**Summary**: Added minimum width constraints for main window (800x600) and category panel (240px) with dynamic percentage calculation.

**Features**:
- Main window cannot be resized smaller than 800x600
- Category panel maintains minimum 240px width
- Dynamic percentage calculation preserves minimum widths during window resize

**Backend Changes**:
- ApplicationHost.cs: Set Form.MinimumSize to 800x600

**Frontend Changes**:
- useResizablePanels.ts: Made minimum widths configurable (minCategoryWidth, minModListWidth)
- useResizablePanels.ts: Dynamic percentage calculation ensuring minimums
- ModHierarchicalView.css: Added CSS min-width constraints

**Commit**: c8ef023

### Fixed - 2026-03-07 - Hybrid File/Folder Selection Dialog ⭐
**Summary**: Fixed mod import workflow file selection dialog to properly support selecting both archive files and folders.

#### Hybrid File/Folder Selection for Mod Import
**Impact**: ✅ Mod import dialog now correctly allows selecting either archive files (.zip, .7z, .rar) OR folders
**Problem**: File selection dialog couldn't select folders from explorer - clicking "Open" did nothing when navigating to a folder
**Root Cause**: `OpenFileDialog` with "Folder Selection" placeholder had file extension appended by Windows (e.g., "Folder Selection.zip"), preventing folder path extraction
**Solution**: Detect placeholder filename with or without filter extension and extract parent directory
**Backend Changes**:
- SystemFileDialogService.cs:206-214: Added logic to check both `fileName == "Folder Selection"` and `fileNameWithoutExt == "Folder Selection"` to handle filter-appended extensions
- SystemFileDialogService.cs:177-244: Simplified hybrid dialog using `OpenFileDialog` with proper placeholder detection instead of complex COM interop
**Frontend Changes**: No changes required - existing configuration already correct

### Fixed - 2026-03-07 - Multiple Bug Fixes and Improvements ⭐⭐⭐
**Summary**: Fixed 7 critical issues: Python migration multi-environment support, category order preservation, dropzone overlay recovery, browser drop prevention, unclassified count management, mod deletion events, and removed unnecessary UI prompts.

#### 1. Python Migration - Multiple Environment Support
**Impact**: ✅ Migration now checks ALL environment folders for configuration, not just the first one
**Problem**: Only checked configuration for first/active environment folder in `home/`, ignoring other valid environments
**Root Cause**: `MigrationStep1AnalyzeSource` only called `_configParser.ParseAsync()` once with `analysis.ActiveEnvironment`
**Solution**: Loop through all environments and parse each configuration
**Backend Changes**:
- MigrationAnalysis.cs:22: Added `EnvironmentConfigurations` dictionary to store configs for all environments
- MigrationStep1AnalyzeSource.cs:222-246: Now loops through all detected environments, parses their configs, stores in dictionary
- MigrationStep1AnalyzeSource.cs:231-235: Logs which environments have valid configurations for debugging

#### 2. Category Tree Order Preservation on Save
**Impact**: ✅ Category priority/order no longer resets when saving category name, description, or thumbnail changes
**Problem**: Category tree order appeared to reset when saving category metadata modifications
**Root Cause**: Priority field could be lost or reset during update operations
**Solution**: Added explicit priority preservation with logging during metadata updates
**Backend Changes**:
- CategoryService.cs:266-278: Added safeguard to store and restore original priority, added verbose logging

#### 3. Category Creation - No Default Description
**Impact**: ✅ New categories no longer have auto-generated "Category: {name}" description
**Problem**: Creating a new category automatically added default description "Category: {name}"
**Solution**: Removed default description fallback, now uses `null` if not provided
**Backend Changes**:
- CategoryService.cs:396: Changed from `description ?? $"Category: {name}"` to `description`

#### 4. Category Thumbnail Confirmation Dialog Removed
**Impact**: ✅ No confirmation dialog when changing category thumbnails
**Problem**: Unnecessary confirmation dialog appeared when saving category with changed thumbnail
**Solution**: Removed modal confirmation logic, use thumbnail directly from form data
**Frontend Changes**:
- useCategoryTreeOperations.tsx:137-138: Simplified to use `thumbnailToUse = data.thumbnail` directly, removed confirmation modal

#### 5. Clear Search on Category Selection
**Impact**: ✅ Mod search query automatically clears when selecting a different category
**Problem**: Search query persisted when switching categories, showing filtered results from previous category
**Solution**: Clear search query in category selection handler
**Frontend Changes**:
- ModHierarchicalView.tsx:134-135: Added `setSearchQuery('')` in `handleCategorieselect` callback

#### 6. Unclassified Mods Count - Store Management
**Impact**: ✅ Unclassified count now managed in centralized store with automatic event-based updates
**Problem**: Unclassified count was local component state with manual loading, not reactive to backend events
**Solution**: Moved to Zustand store, added event subscriptions, removed manual loading
**Frontend Changes**:
- modsStore.ts:36,82,133,264-267: Added `unclassifiedCount` state, setter action, initial value, implementation
- categoryOperations.ts:167-180: Added `loadUnclassifiedCount()` function that updates store
- ModProvider.tsx:118,189: Load unclassified count on initialization and category tree updates
- ModHierarchicalView.tsx:34: Now subscribes to store `unclassifiedCount`, removed local state and manual loading
- useMods.ts:54-55: Exposed `loadUnclassifiedCount()` operation

#### 7. Mod Deletion Event Emission
**Impact**: ✅ Frontend now receives DELETED event when mod is deleted from database
**Problem**: `ModEvents.DELETED` was never emitted, frontend didn't know when mods were deleted
**Root Cause**: `ModMetadataService.DeleteAsync()` didn't emit event after deletion
**Solution**: Added event emission in service layer following architecture pattern (services emit events, not facades)
**Backend Changes**:
- ModMetadataService.cs:130-131: Added `_eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { Sha = sha })` after successful deletion

#### 8. Dropzone Overlay Focus Recovery
**Impact**: ✅ Dropzone overlay automatically recovers state when application window regains focus (applies to both main and secondary windows)
**Problem**: Overlay wouldn't recover after window lost and regained focus, mouse tracking timer could stop
**Root Cause**: No mechanism to detect form activation and restore overlay state
**Solution**: Added parent form activation monitoring with timer health checks
**Backend Changes**:
- DropZoneOverlay.cs:40,85-91: Added `_parentForm` field and attached to `Form.Activated` event
- DropZoneOverlay.cs:118-144: Added `OnParentFormActivated()` handler that verifies timer is running, restarts if stopped, forces visibility recalculation
- DropZoneOverlay.cs:351-356: Proper cleanup detaches form activation handler

#### 9. Browser Drop Behavior Prevention (Selective)
**Impact**: ✅ External file drops no longer open in browser, React internal drag-and-drop still works (applies to both main and secondary windows)
**Problem 1**: Files dropped in WebView would open in browser (new tab behavior)
**Problem 2**: Need to preserve React internal drag-and-drop for UI interactions (e.g., dragging mods between categories)
**Solution**: Detect external file drops via `dataTransfer.types` and prevent ONLY those, allow React HTML element drags
**Backend Changes**:
- WebViewInitializer.cs:157,168-213: Added `PreventDefaultDropBehavior()` that injects JavaScript to check `dataTransfer.types.indexOf('Files')` and only prevent external files

**Architecture**: All dropzone fixes automatically apply to both main window and secondary windows (screen capture, etc.) through shared `WebViewInitializer` and `DropZoneManager` usage

### Fixed - 2026-03-07 - Mod Preview Panel UI Issues After Extraction ⭐⭐
Fixed keybinding button visibility, preview panel flash on delete, and unnecessary preview reloads.
**Impact**: ✅ Keybinding button now appears immediately after mod extraction, no flash when deleting, previews don't reload unnecessarily
**Problem 1**: Keybinding button didn't appear after extracting a mod until mod was reselected
**Problem 2**: Preview panel flashed to empty state when deleting the currently selected mod
**Problem 3**: Preview images reloaded unnecessarily when mod load status changed
**Root Cause 1**: `optimisticLoadUpdate` and `optimisticUnloadUpdate` updated arrays but not `selectedMod`, so `hasCache` wasn't set on selected mod
**Root Cause 2**: `removeMod()` immediately cleared `selectedMod` causing instant empty state without loading feedback
**Root Cause 3**: Preview effect had `mod?.isLoaded` in dependencies, triggering reloads on every load/unload despite previews being unchanged
**Solution**: Update `selectedMod` in optimistic updates, add preview loading state during delete, remove unnecessary effect dependency
**Frontend Changes**:
- modsStore.ts:221-252: Added `selectedMod` update in `optimisticLoadUpdate` to set `isLoaded: true, hasCache: true` for selected mod
- modsStore.ts:233-239: Added logic to update `selectedMod` and unloaded mods when they're the currently selected mod
- modsStore.ts:254-269: Added `selectedMod` update in `optimisticUnloadUpdate` to set `isLoaded: false` for selected mod
- modOperations.ts:129-164: Added preview loading state (`setPreviewLoading(true)`) during delete operation to show spinner instead of empty state
- modOperations.ts:135-137,153-155,158-160: Show preview loading before delete, clear after completion or on error
- ModPreviewPanel.tsx:44-53: Removed `mod?.isLoaded` from effect dependencies as load status doesn't affect preview images
**Architecture**: Optimistic updates now maintain full consistency across all store fields (mods, categoryFilteredMods, AND selectedMod)

### Fixed - 2026-03-06 - DropZone Race Condition Prevention ⭐⭐
Fixed race conditions in dropzone visibility management by implementing bidirectional state checking between frontend and backend.
**Impact**: ✅ Dropzone overlay now correctly shows/hides without flickering or getting stuck in wrong state
**Problem 1**: Frontend sent visibility updates based on element state, backend independently tracked mouse position, causing conflicts
**Problem 2**: Rapid mouse movements and element state changes created race conditions with conflicting show/hide commands
**Problem 3**: Backend would show overlay when mouse left, even if frontend detected element was occluded
**Solution**: Implemented dual-state tracking in backend and bidirectional checks in frontend
**Frontend Changes**:
- useDropZone.ts:134-199: Added helper functions `showZone()`, `hideZone()`, `checkElementVisibility()`, `syncZoneVisibility()` to eliminate code duplication
- useDropZone.ts:387-403: Updated mouse enter/leave handlers to call `syncZoneVisibility()` for consistent state checking
- useDropZone.ts:354-389: Frontend now rechecks element visibility and occlusion on every mouse event before sending commands
**Backend Changes**:
- DropZoneOverlay.cs:97-98: Added `_mouseIsInside` and `_requestsVisible` state tracking variables
- DropZoneOverlay.cs:188-214: Implemented `UpdateVisibility()` method that shows overlay only when frontend requests AND mouse is outside
- DropZoneOverlay.cs:100-125: Updated `CheckOverlayVisibility()` timer to detect mouse position changes and sync state
- DropZoneOverlay.cs:225-247: Modified Show/Hide methods to update `_requestsVisible` flag and call `UpdateVisibility()`
- DropZoneManager.cs:149-165: Removed mouse position checks, trusts frontend's visibility decisions
**Root Cause**: Separate visibility logic in frontend (element state) and backend (mouse position) created conflicting decisions
**Architecture**: Backend now combines both states: visible = (_requestsVisible && !_mouseIsInside), preventing all race conditions

### Fixed - 2026-03-06 - Category Sub-Category Creation and Thumbnail Path Resolution ⭐⭐
Fixed critical bug preventing sub-category creation due to empty string parentId normalization and relative thumbnail path issues.
**Impact**: ✅ Sub-categories now work correctly, inherited thumbnails resolve properly
**Problem 1**: Empty string `""` for root category parentId wasn't normalized to `NULL` in database, causing orphaned categories
**Problem 2**: Frontend sent `""` for root categories instead of `undefined`, backend didn't convert to `null` before INSERT
**Problem 3**: Inherited parent thumbnails (stored as relative paths) failed to resolve to absolute paths during hash calculation
**Problem 4**: Category name uniqueness check was case-insensitive and in-memory instead of database-level and case-sensitive
**Solution**: Added empty string normalization, absolute path resolution for thumbnails, case-sensitive database checks
**Backend Changes**:
- CategoryService.cs:333-337: Added `if (string.IsNullOrWhiteSpace(parentId)) parentId = null;` normalization in CreateAsync
- CategoryService.cs:162-166: Added same normalization in UpdateParentAsync for move operations
- CategoryService.cs:357-358,239-240,415-416: Added `_pathHelper.ToAbsolutePath(thumbnailPath)` before hash calculation in CreateAsync, UpdateCategoryAsync, UpdateThumbnailAsync
- CategoryService.cs:339-344: Changed from in-memory `allCategories.Any(c => c.Name.Equals(name, OrdinalIgnoreCase))` to database `GetByNameAsync(name)` with case-sensitive check
- CategoryService.cs:223-230: Updated UpdateCategoryAsync to use direct database check instead of loading all categories
- CategoryRepository.cs:188: Removed `COLLATE NOCASE` from GetByNameAsync for case-sensitive name matching
- CategoryService.cs:67,77: Injected ILogHelper, replaced all Console.WriteLine with `_logger.Warn()` and `_logger.Verbose()`
**Root Cause**: CategoryScreen.tsx uses `""` for root categories (line 293), backend wasn't normalizing to `null`, database stored `ParentId = ""` instead of `NULL`, sub-category lookups failed
**Side Effects Fixed**: Orphaned categories with empty parentId now properly identified as root categories after normalization

### Changed - 2026-03-06 - Centralized Mod Sorting with CategoryName Population ⭐⭐
Refactored mod sorting from scattered SQL JOINs to centralized in-memory sorting using cached category names for consistent ordering across all mod queries.
**Impact**: ✅ All mod lists now sort by category name → mod name consistently, leverages CategoryService cache
**Problem 1**: Sorting logic duplicated across multiple repository methods with SQL JOINs
**Problem 2**: Each query built its own category name map, wasting memory and CPU
**Problem 3**: No single source of truth for sorting behavior
**Problem 4**: PopulateCategoryNamesBulkAsync built temporary map instead of using CategoryService's cached GetCategoryNameAsync
**Solution**: Removed SQL sorting, always populate CategoryName via cached service, centralized sort in ModQueryService
**Backend Changes**:
- ModRepository.cs: Removed `GetAllSortedAsync()` and `FilterAsync()` methods with SQL JOINs
- ModRepository.cs:261: Simplified GetByMultipleCategoriesAsync to remove JOIN, just `SELECT * FROM Mods WHERE Category IN (...)`
- ModQueryService.cs:380-401: Refactored PopulateCategoryNamesBulkAsync to call `_categoryService.GetCategoryNameAsync()` for each mod (uses cached map)
- ModQueryService.cs:273-279: Added centralized `SortMods(mods)` that sorts by `CategoryName ?? Category` then `Name` (synchronous, no async)
- ModQueryService.cs:101-103,146-148,158-160,210-212,239-241: Updated all query methods to call PopulateCategoryNamesBulkAsync then SortMods
- ModQueryService.cs:234-235,260-261: Removed obsolete `mod.Category.Equals("Unknown")` checks from unclassified mod filters
**Architecture Benefits**:
- **Single Source of Truth**: SortMods is the only place sorting logic exists
- **Leverages Cache**: Uses CategoryService's cached category ID→Name map (shared across queries)
- **Cleaner Code**: No SQL JOINs, simpler repository queries
- **Consistent Behavior**: All mod lists use identical sort: CategoryName (case-insensitive) → ModName (case-insensitive)
**Performance**: First query builds cache, all subsequent queries reuse cached map (very fast!)

### Changed - 2026-03-06 - Generic Window System with ProfileContext Integration ⭐⭐
Refactored window configuration system from capture-specific to generic multi-window support with ProfileContext integration and thread-safe ConcurrentDictionary management.
**Impact**: ✅ Enables multiple window types (capture, debug, tools) with independent position/size storage, eliminates profileId parameter passing, prevents race conditions
**Problem 1**: Hard-coded `Capture` field in ProfileConfiguration limited to single window type
**Problem 2**: ProfileId passed as parameters throughout SecondaryWindowService instead of using ProfileContext
**Problem 3**: `CreateCaptureWindowAsync` in generic SecondaryWindowService created circular dependency with ScreenCaptureService
**Problem 4**: List-based `_openWindows` caused race conditions and couldn't differentiate window types for toggle operations
**Problem 5**: Direct file I/O in SecondaryWindowService bypassed ProfileService locking, causing config.json conflicts
**Solution**: Implemented generic Windows dictionary in ProfileConfiguration, ProfileContext injection, separated concerns between services, ConcurrentDictionary for thread-safe window tracking
**Backend Changes**:
- ProfileConfiguration.cs: Removed `Capture` field, added `Dictionary<string, WindowConfiguration> Windows` with `WindowConfiguration { X, Y, Width, Height }`
- ProfileService.cs: Added `UpdateWindowConfigurationAsync(profileId, windowName, x, y, width, height)` for generic window updates
- ProfileFacade.cs:278-314: Fixed `UpdateProfileConfigAsync` to load existing config before updating to preserve all fields (Capture, Work, MigotoVersion)
- SecondaryWindowService.cs: Injected `IProfileContext`, removed profileId parameters, exposed public `CreateSecondaryWindowAsync(windowName, title, width, height, htmlPage)`
- SecondaryWindowService.cs: Changed `List<(Form, Session, ProfileId, WindowName)>` → `ConcurrentDictionary<string, WindowEntry>` for thread-safe window tracking by name
- SecondaryWindowService.cs: Added window-specific methods `HasWindow(windowName)`, `CloseWindow(windowName)` replacing profile-based methods
- SecondaryWindowService.cs: Removed direct file I/O, now uses `ProfileService.UpdateWindowConfigurationAsync()` for all config operations
- ScreenCaptureService.cs: Moved `CreateCaptureWindowAsync()` from SecondaryWindowService, calls generic `CreateSecondaryWindowAsync()` and adds capture-specific overlay cleanup
- ScreenCaptureService.cs:224-233: Updated `ToggleCaptureControlPanel` to use `HasWindow("capture")` and `CloseWindow("capture")` instead of profile-based checks
**Architecture Benefits**:
- **Generic Windows**: Support any window type without code changes (e.g., "capture", "debug", "tools")
- **No Circular Dependencies**: ScreenCaptureService → SecondaryWindowService (one direction)
- **ProfileContext-Scoped**: Services get profileId from IProfileContext injection
- **Thread-Safe**: ConcurrentDictionary prevents race conditions in window management
- **Config Preservation**: All config updates preserve existing fields via ProfileService
**Config Format**:
```json
{
  "windows": {
    "capture": { "x": 1600, "y": 800, "width": 300, "height": 210 },
    "debug": { "x": 100, "y": 100, "width": 400, "height": 600 }
  }
}
```
**Documentation**: Updated PROFILE_SYSTEM.md and SCREEN_CAPTURE_TOOL.md with generic window system architecture

### Changed - 2026-03-05 - Screen Capture: Toggle Control Panel & Profile Switch Cleanup ⭐
Refactored screen capture control panel from "show" to "toggle" behavior and implemented automatic window cleanup on profile switch.
**Impact**: ✅ Improved UX with single-button toggle for control panel, automatic cleanup prevents orphaned windows
**Problem 1**: Control panel could only be opened, not closed programmatically - users had to manually close window
**Problem 2**: Switching profiles left control panel windows open from previous profile
**Problem 3**: `ISecondaryWindowService` is profile-scoped but cleanup attempted to access from global service provider
**Solution**: Implemented toggle logic with profile-aware window tracking, added cleanup via ProfileServiceRouter on profile switch events
**Backend Changes**:
- SecondaryWindowService.cs: Added `HasWindowForProfile()` and `CloseWindowForProfile()` methods to ISecondaryWindowService interface
- ScreenCaptureService.cs: Renamed `ShowCaptureControlPanel` → `ToggleCaptureControlPanel` with toggle logic (check if exists, close if open, open if closed)
- ToolFacade.cs: Updated IPC routing from `SCREEN_CAPTURE_SHOW_CONTROL_PANEL` → `SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL`
- ProfileServiceRouter.cs: Added `CloseAllSecondaryWindows()` method to iterate all profile-scoped services and close windows
- ApplicationHost.cs: Subscribed to `PROFILE:SWITCHED` event to call `ProfileServiceRouter.CloseAllSecondaryWindows()`
- IpcHandler.cs:242-266: Fixed threading bug - moved all `CoreWebView2` access inside UI thread invocation to prevent `InvalidOperationException`
**Frontend Changes**:
- toolService.ts: Renamed `showControlPanel` → `toggleControlPanel`, updated message type to `SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL`
- ToolsView.tsx: Updated to call `api.tool.toggleControlPanel()` instead of `showControlPanel()`
**Architecture Fix**: Properly handles profile-scoped services - uses ProfileServiceRouter to access ISecondaryWindowService across all profiles instead of attempting global service provider access
**Threading Fix**: All WebView2 property access now properly marshalled to UI thread, preventing "CoreWebView2 can only be accessed from the UI thread" errors
**Documentation**: Added comprehensive `docs/features/SCREEN_CAPTURE_TOOL.md` covering architecture, threading model, IPC messages, and troubleshooting

### Changed - 2026-03-05 - Configuration: Work Directory Replaces Mod Cache Configuration ⭐⭐
Refactored profile configuration from `modCache` to `work` directory to correctly represent the work directory architecture (parent of Mods folder). Fixed critical bug where external work directory configuration was not being applied.
**Impact**: ✅ Configuration now accurately reflects the documented architecture where work directory is the parent containing the Mods subfolder. External work directory mode now functions correctly.
**Problem 1**: The `modCache` configuration incorrectly pointed directly to the Mods folder, when it should target the parent work directory
**Problem 2**: The `WorkDirectory` property in ProfilePathService was hardcoded to always return internal path, ignoring external configuration
**Solution**: Renamed configuration from `modCache` → `work` throughout the stack, made `WorkDirectory` property dynamic with caching like `CacheModsDirectory`
**Backend Changes**:
- Renamed `ModCacheConfiguration.cs` → `WorkDirectoryConfiguration.cs` with updated documentation
- ProfileConfiguration.cs: Changed `ModCache` property → `Work` property
- ProfilePathService.cs: Made `WorkDirectory` property dynamic with cache support (reads from config like `CacheModsDirectory`)
- ProfilePathService.cs:148-200: Updated `LoadCacheDirectoryAsync()` to cache both work directory AND cache mods directory
- ProfilePathService.cs:88-92: Updated `InvalidateCacheDirectory()` to clear both cache keys
- ProfileFacade.cs:167-187: Added computed `InternalWorkDirectory` to GET_CONFIG response for UI display
- ProfileFacade.cs: Changed IPC payload parameters from `modCacheMode/modCacheDirectory` → `workMode/workDirectory`
- WorkDirectoryConfiguration.cs: Added `InternalWorkDirectory` property for computed internal path (not persisted)
**Frontend Changes**:
- profileService.ts: Renamed `ModCacheConfiguration` → `WorkDirectoryConfiguration` interface, added `internalWorkDirectory` field
- settingsStore.ts: Renamed all state (`modCacheMode` → `workMode`, `modCacheDirectory` → `workDirectory`, `internalModCachePath` → `internalWorkPath`)
- settingsOperations.ts: Updated to load `internalWorkDirectory` from backend response and set in store
- SettingsView.tsx: Updated component handlers and form fields to use work directory terminology
**Localization Changes**:
- en.json/cn.json: Updated all `settings.profile.modCache.*` → `settings.profile.work.*` keys
- English: "Mod Cache Directory" → "Work Directory" with tooltip clarifying parent of Mods folder
- Chinese: "模组缓存目录" → "工作目录" with updated tooltip
**Architecture Alignment**: Configuration now matches documented structure: `work/Mods/{SHA}/` instead of directly pointing to Mods folder

### Added - 2026-03-05 - Mod Management: Multi-Select for Bulk Category Updates ⭐⭐
Implemented multi-select functionality in mod list with Ctrl+Click, Shift+Click, and bulk drag-drop to category tree.
**Impact**: ✅ Users can now select multiple mods and move them to categories in bulk, significantly improving workflow efficiency for organizing large mod collections
**Problem**: Users could only move mods to categories one at a time, making it tedious to organize multiple mods
**Solution**: Added multi-selection with keyboard modifiers, visual feedback, and bulk backend operations for efficient category updates
**Frontend Changes - ModListPanel.tsx**:
- Added local state for multi-selection: `selectedModShas` (array), `anchorSha` (for range selection)
- Implemented `handleModClick` with three modes: regular click (single select), Ctrl+Click (toggle individual), Shift+Click (range select)
- Multi-selection automatically clears when category/object changes
- Passes selection count to status bar
**Frontend Changes - ModList.tsx**:
- Updated props to accept `selectedModShas` array and pass mouse event to parent
- Enhanced drag behavior: single mod uses `application/mod-sha`, multi-select uses `application/mod-shas` with JSON array
- Visual classes: `.mod-list-item-selected` (primary), `.mod-list-item-multi-selected` (same style as primary)
**Frontend Changes - ModList.css**:
- Multi-selected items use identical styling to primary selection (same blue highlight and border)
- No opacity or filter differences - clean, consistent visual experience
**Frontend Changes - ModListStatusBar.tsx**:
- Shows "X Mods selected" when multiple mods selected (takes priority over active mod display)
- Added i18n support with translations for selection count
**Frontend Changes - CategoryTree.tsx**:
- Added third drag/drop handler for `application/mod-shas` event type
- Parses JSON array of mod SHAs and calls `handleBulkModClassify`
**Frontend Changes - Category Operations**:
- `useCategoryTreeOperations.tsx`: Added `handleBulkModClassify` for multiple mods
- `useModCategoryUpdate.ts`: Added `updateModsCategory` function with bulk IPC call
- `categoryOperations.ts`: Updated `batchUpdateCategories` to use new bulk IPC method
- `useMods.ts`: Added `updateModsCategory` operation
**Frontend Changes - IPC Service (modService.ts)**:
- Added `batchUpdateCategory` method sending `BATCH_UPDATE_CATEGORY` message
- Returns count of successfully updated mods
**Backend Changes - ModMetadataService.cs**:
- Added `BatchUpdateCategoryAsync` method to interface and implementation
- Iterates through mod SHAs, unloads loaded mods before category change
- Returns count of successfully updated mods with comprehensive logging
**Backend Changes - ModFacade.cs**:
- Added `BatchUpdateCategoryAsync` public method calling metadata service
- Emits `MOD.CATEGORY_UPDATED` event with bulk payload for reactive updates
- Added IPC routing for `BATCH_UPDATE_CATEGORY` message type
- Added private handler method for IPC request parsing
**i18n Translations**:
- English (en.json): "{{count}} Mods selected", "No active mod"
- Chinese (cn.json): "已选择 {{count}} 个模组", "无激活模组"
**Key Design Decisions**:
- Multi-selection state is local (not in global store) - temporary for selection workflow only
- Preview always shows first selected mod only (primary selection)
- Backend bulk operation more efficient than N individual IPC calls
- Same visual style for all selections provides clean, consistent UX
- Event-based reactive updates maintain UI consistency across components
**Files Changed**: 15 files (11 frontend, 2 backend, 2 i18n)
**Code Changes**: +450 lines frontend, +90 lines backend, +4 translation keys

### Enhanced - 2026-03-04 - Help System: Redesigned with SlideInScreen & Updated Content ⭐⭐
Complete redesign of help window with modern sliding screen interface and comprehensive content updates reflecting current architecture.
**Impact**: ✅ Users get accurate, well-organized help documentation in a modern UI that matches the app's design system
**Problem**: Help window used outdated modal design and contained stale information about removed/renamed features
**Solution**: Migrated to SlideInScreen with vertical tabs, rewrote all content based on current architecture documentation
**Frontend Changes - Help Module (NEW)**:
- Created dedicated `modules/help/` module (moved from core/components/windows)
- Module structure: `components/HelpWindow.tsx`, `HelpWindow.css`, `index.ts`
**Frontend Changes - HelpWindow.tsx**:
- Redesigned with vertical sidebar navigation (9 sections) instead of horizontal tabs
- Sections: Quick Start, Profiles, Mod Management, Category System, Import Queue, Tag Management, Game Launch, Tools & Utilities, Tips & Best Practices
- Removed outdated content: "Launch Setup", incorrect navigation paths, old hierarchical organization references
- Added new content: Profile management, GUID-based category system, workflow-based import queue, tag management tool
- Updated all descriptions to match current features: debounced preview panel, external cache support, tree-based categories
- Removed deprecated Alert props (message/description → inline JSX)
**Frontend Changes - HelpWindow.css**:
- Vertical nav sidebar (200px width) with active state highlighting
- BEM naming: `.help-window-layout`, `.help-window-nav`, `.help-window-nav-item`, `.help-window-content-area`
- Theme support with CSS variables for colors
- Custom scrollbar styling for both nav and content areas
- Responsive design with media queries for smaller screens
**Frontend Changes - App.tsx**:
- Changed from modal-based (`visible` state) to SlideInScreen (`openScreen()`)
- Removed `helpWindowVisible` state variable
- Updated `handleHelpClick` to use `openScreen({ title, content, width })`
**Key Design Decisions**:
- SlideInScreen for consistency with other tools (Profile Manager, Tag Management)
- Vertical tabs for better space utilization and clearer section organization
- Content verified against `docs/AI_GUIDE.md` and architecture documentation
- No emoji usage (following project style)
**Files Changed**: 4 files (1 new module, 3 updated)
**Code Changes**: +709 lines (new help content), -132 lines (old modal code)

### Improved - 2026-03-04 - i18n: Cleaned Up 147 Unused Translation Keys (22% reduction) ⭐
Removed 147 unused translation keys from both English and Chinese language files using comprehensive codebase analysis.
**Impact**: ✅ Smaller translation files, easier maintenance, faster translation loading
**Problem**: 665 translation keys but only 518 were actually used in the codebase (22% waste)
**Root Cause**: Features were removed/renamed but translation keys remained (addMod dialog, unused launch.game options, old category UI)
**Solution**: Created memory-based search script to find all key usage in source code, removed confirmed unused keys
**Cleanup Process**:
1. Loaded all 175 TypeScript/JavaScript source files into memory (~0.77 MB)
2. Checked each translation key for presence anywhere in source code (not just t() calls)
3. Identified 147 keys with zero references in codebase
4. Removed keys from both en.json and cn.json
**Keys Removed (Examples)**:
- `addMod.*` - Old add mod dialog (replaced by workflow system)
- `launch.game.*` - Unused game launch configuration options
- `category.screen.*` - Some old category management UI keys
- Migration error mapping keys - Unused granular error messages
**Backend Changes - Languages/en.json**:
- Reduced from 665 to 518 keys (147 removed)
**Backend Changes - Languages/cn.json**:
- Reduced from 664 to 518 keys (146 removed)
- Fixed BOM (Byte Order Mark) handling in cleanup script
**Verification**:
- Build succeeded with no errors
- All remaining 518 keys are confirmed used in source code
**Files Changed**: 2 files (both language JSONs)
**Reduction**: 22.1% smaller translation files

### Added - 2026-03-03 - TagManagementTool: Full CRUD Tag Management UI ⭐⭐
Complete redesign of tag management tool with proper CRUD operations, compact design, and external pagination.
**Impact**: ✅ Users can now create, edit (name + color), and delete tags in a dedicated tool interface
**Problem**: Tags could only be managed in the small TagManagementDialog, no dedicated tool for bulk tag operations
**Solution**: Built TagManagementTool component with proper UI/UX following design system patterns
**Frontend Changes - TagManagementTool.tsx**:
- Implemented full CRUD: Create (with FormDialog), Edit (with FormDialog), Delete (with ConfirmDialog)
- Search functionality with real-time filtering
- External pagination (20 items/page, customizable: 10/20/50/100)
- Removed inline color editing (colors now edited in proper Edit dialog)
- Compact table design with proper background colors
- BEM naming convention for CSS classes
**Frontend Changes - TagManagementTool.css**:
- Clean layout: header with search + actions, alert, table with distinct background, external pagination
- Table styling: distinct header/body backgrounds using theme variables
- Compact pagination styling (24px height, 12px font)
- BEM class structure: `.tag-management-tool-container`, `.tag-management-tool-header`, etc.
**Frontend Changes - useTagManagement.ts** (NEW):
- Created shared hook for tag CRUD operations
- Handles tag creation, update, deletion with proper error handling
- Debounced color updates (500ms)
**Frontend Changes - migration.types.ts** (NEW):
- Moved migration types from migrationService to shared/types
- Includes: MigrationStage, MigrationProgress, MigrationResult, MigrationError, etc.
- Fixed eventBus import to use shared types
**Frontend Changes - ToolsView.css**:
- Updated tool cards to use `var(--color-bg-elevated)` for better visual distinction
**Key Features**:
- Create: FormDialog with name + color picker, duplicate validation
- Edit: FormDialog to modify name and/or color, supports renaming (deletes old, creates new)
- Delete: ConfirmDialog with warning about mod associations
- Search: Filter tags by name in real-time
- Pagination: External compact pagination below table
- Responsive: Proper space usage with distinct backgrounds

### Enhanced - 2026-03-02 - DropZone Overlay: Simplified Auto-Hide Architecture ⭐⭐⭐
Simplified DropZone overlay to auto-hide on mouse enter, eliminating 70% of complex event forwarding code while maintaining full functionality.
**Impact**: ✅ Cleaner codebase, better UX (no wasted clicks), simplified maintenance, improved performance
**Problem**: Complex mouse event forwarding (JavaScript injection, hover effects, scrollbar detection) added ~500 lines of code and complexity
**Root Cause**: Trying to make overlay transparent to mouse events while staying visible required forwarding all events and mimicking browser behavior
**New Approach**: Hide overlay immediately when mouse enters (not during file drag) → WebView handles everything naturally
**Backend Changes - DropZoneOverlay.cs**:
- Removed all JavaScript injection code (~240 lines): JS_HANDLE_HOVER_AND_CURSOR, JS_CLEANUP_HOVER, JS_FIND_SCROLLABLE_ELEMENTS, JS_INJECT_HOVER_STYLES, JS_DISPATCH_MOUSE_EVENT_TEMPLATE
- Removed P/Invoke for SendMessage
- Removed Windows message constants (WM_MOUSEMOVE, WM_LBUTTONDOWN, etc.)
- Removed ForwardMouseMessageToWebView, HandleMouseMove, DispatchMouseEvent methods
- Removed InjectHoverStyles, CleanupHoverStyles, UpdateCursorFromStyle methods
- Removed UpdateScrollableRects, IsOverScrollbar methods
- Removed IsDraggingFiles method
- Simplified WndProc to just call base.WndProc (no event handling)
- Added auto-hide on OnMouseEnter: Sets Visible=false immediately when mouse enters overlay
- Simplified CheckOverlayVisibility: Only restores when cursor leaves overlay area
- File reduced from ~787 lines to ~233 lines (70% reduction)
**Backend Changes - DropZoneManager.cs**:
- Removed auto-disposal callback (no longer needed with simplified overlay)
**Frontend Changes - ModsProvider.tsx**:
- Added METADATA_UPDATED event subscription to refresh mod list when metadata changes
- Added CATEGORY_UPDATED event subscription to refresh both mod list and category tree when mod category changes
**Frontend Changes - ModListPanel.css**:
- Refactored drop zone structure: Fixed-height parent with scrollable child
- Drop message now uses position:absolute overlay covering fixed parent
- Drop message stays centered in visible area regardless of scroll position
**How It Works Now**:
1. Overlay visible by default (ready for file drags from File Explorer)
2. Mouse enters → OnMouseEnter fires → Overlay hides immediately (Visible=false)
3. User interacts with WebView underneath (clicks, drags HTML elements, scrolls) - NO wasted clicks!
4. Mouse leaves → Timer detects cursor left area → Overlay restores (Visible=true)
5. File drag from Explorer → Overlay captures drag events normally (AllowDrop=true)
**Architecture Improvements**:
- Simpler: No complex event forwarding, JavaScript injection, or hover mimicry
- Better UX: No wasted clicks - first interaction always works
- Consistent: Same pattern used for scrollbar hiding
- Maintainable: Less code, fewer edge cases, clearer logic
- Performant: No JavaScript execution, no async deadlock risks
**Files Changed**: 4 files (2 backend C#, 2 frontend TypeScript/CSS)
**Code Reduction**: ~550 lines removed (JavaScript injection, event forwarding, hover effects, scrollbar detection)

### Enhanced - 2026-03-01 - DropZone Overlay: Complete Mouse Event Forwarding & JavaScript Organization ⭐⭐⭐
Complete implementation of transparent overlay that forwards all mouse events to WebView2 with full hover effects, dynamic cursor changes, and scrollbar interaction support.
**Impact**: ✅ Drag-drop works without topmost window requirement, all mouse events forwarded correctly, hover effects fully functional, scrollbar interaction working, clean JavaScript organization
**Problem**: DropZone overlay blocked mouse interactions with WebView2 content (clicks, hovers, scrolling)
**Root Cause**: WS_EX_TRANSPARENT makes overlay visually transparent but captures all mouse events, preventing WebView2 interaction
**Backend Changes - DropZoneOverlay.cs**:
- Mouse Event Forwarding: WndProc captures WM_MOUSEMOVE/LBUTTONDOWN/LBUTTONUP/MOUSEWHEEL and dispatches JavaScript events
- Hover Effects: Implemented `__overlay_hover` CSS class system that copies all `:hover` rules from stylesheets
- Dynamic Cursor: JavaScript queries `window.getComputedStyle(element).cursor` and updates overlay cursor to match underlying element
- Wheel Scrolling: Fixed coordinate conversion (WM_MOUSEWHEEL uses screen coordinates), manually scrolls scrollable elements via `scrollTop`
- Scrollbar Detection: JavaScript finds all scrollable elements, calculates scrollbar rectangles, temporarily hides overlay when over scrollbar
- Visibility Timer: 100ms timer checks cursor position and restores overlay when mouse leaves scrollbar area
- JavaScript Organization: All scripts moved to constants (JS_HANDLE_HOVER_AND_CURSOR, JS_CLEANUP_HOVER, JS_FIND_SCROLLABLE_ELEMENTS, JS_INJECT_HOVER_STYLES, JS_DISPATCH_MOUSE_EVENT_TEMPLATE)
- Cleanup: Removed unused Win32 P/Invoke (SendMessage, WindowFromPoint, ScreenToClient, POINT), removed unused DropZoneEvents (CLICK, MOUSE_MOVE, MOUSE_DOWN, MOUSE_UP, DOUBLE_CLICK, MOUSE_WHEEL)
**Backend Changes - DropZoneManager.cs**:
- Auto-Disposal: Added onHide callback to DropZoneOverlay that calls UnregisterZone() when overlay is hidden
- Lifecycle Management: When overlay.Hide() is called, zone is automatically unregistered and disposed to prevent memory leaks
**Frontend Changes - eventBus.ts**:
- Removed CLICK from DropZoneEventType enum (no longer emitted by backend)
- Removed CLICK event payload type from EventPayloadMap
- Final event types: DRAG_ENTER, DRAG_LEAVE, FILE_DROP, MOUSE_ENTER, MOUSE_LEAVE
**Frontend Changes - useDropZone.ts**:
- Removed click event subscription (clicks now handled directly via JavaScript event forwarding)
- Simplified hook to only handle drag-drop and hover state CSS classes
**Technical Solutions**:
- Coordinate Conversion: WM_MOUSEWHEEL uses screen coords → convert via PointToClient before dispatching
- CSS :hover Mimicry: Browser's :hover can't be triggered by JavaScript → inject CSS that copies `:hover` selectors to `.__overlay_hover`
- Scrollbar Interaction: Overlay blocks native scrollbar → detect scrollbar areas and temporarily hide overlay
- Event Bubbling: Dispatch MouseEvent/WheelEvent with bubbles:true to trigger React event handlers
- Async Deadlock Prevention: Never use .GetAwaiter().GetResult() in WndProc → use pixel-based detection instead
**Architecture Improvements**:
- Backend-centric: All mouse event forwarding logic in C#, JavaScript only used for DOM queries and event dispatch
- Clean Separation: DropZoneOverlay handles overlay lifecycle, DropZoneManager handles zone registration
- Event-driven: Mouse events forwarded to WebView2, backend events notify frontend of drag state
- Automatic Cleanup: Zones self-unregister when hidden, no manual cleanup needed
**Files Changed**: 4 files (2 backend C#, 2 frontend TypeScript)
**Pattern**: Low-level Windows message handling, JavaScript injection for DOM manipulation, CSS rule copying for hover effects, timer-based visibility management

### Fixed - 2026-03-02 - External Cache & Category Refresh: Path Configuration & Event-Driven Updates ⭐⭐⭐
Complete fix for external mod cache directory configuration and parent category mod list refresh after load/unload operations.
**Impact**: ✅ External cache directory now works correctly, parent categories show all sub-category mods after load/unload, no duplicate code, type-safe constants
**Problem 1 - External Cache Not Working**: Case-sensitive string comparison in ProfilePathService prevented external cache mode detection
**Root Cause**: ProfilePathService.cs:136 checked for `"External"` (capital E) but backend saves as lowercase `"external"`
**Problem 2 - Parent Category Shows No Mods**: After mod load/unload, parent category list showed nothing instead of all sub-category mods
**Root Cause**: Frontend re-filtering logic only matched exact category IDs, didn't include descendant categories like backend does
**Problem 3 - Unclassified Category Not Refreshing**: Event handler used wrong ID constant `'__uncategorized__'` instead of `'__unclassified__'`
**Backend Changes**:
- ModCacheConfiguration.cs: Added IsExternal() method with case-insensitive comparison (StringComparison.OrdinalIgnoreCase) *(Note: Later refactored to WorkDirectoryConfiguration on 2026-03-05)*
- ProfilePathService.cs:136: Replaced string comparison with config?.ModCache?.IsExternal() call *(Note: Later changed to config?.Work?.IsExternal() on 2026-03-05)*
**Frontend Changes**:
- category.types.ts: Added CATEGORY_IDS constant object with UNCLASSIFIED: '__unclassified__'
- modsStore.ts: Removed incorrect frontend re-filtering logic (lines 200-232), now relies on backend for filtering
- ModsProvider.tsx: Added event subscriptions for MOD.LOADED and MOD.UNLOADED to refresh category-filtered mods
- ModsProvider.tsx: Consolidated duplicate event handlers into single handleModStateChange() function
- categoryOperations.ts: Fixed typo '__uncategorized__' → CATEGORY_IDS.UNCLASSIFIED
- Replaced 8+ hardcoded string literals across 6 files with CATEGORY_IDS.UNCLASSIFIED constant:
  - ModsProvider.tsx, categoryOperations.ts, ModHierarchicalView.tsx (4 instances)
  - CategoryPanel.tsx, useCategoryTreeOperations.tsx
**Architecture Improvements**:
- Backend-centric: Frontend no longer processes data, only displays what backend provides
- Event-driven: MOD.LOADED/UNLOADED → refresh category-filtered mods from backend
- Backend's GetModsByCategoryAsync already includes all descendant categories via GetAllDescendantIdsAsync
- Type-safe constants prevent future typos and inconsistencies
**Files Changed**: 9 files (2 backend C#, 7 frontend TypeScript)
**Pattern**: Encapsulation in model classes, event-driven cache refresh, DRY principle, type-safe constants

### Fixed - 2026-03-01 - External Cache & Production Security: Path Fixes, Component Renaming & Browser Feature Blocking ⭐⭐⭐
Complete fix for external cache folder path not being respected, consistent component naming, and production security enhancements.
**Impact**: ✅ External cache folder now works correctly, cleaner component naming, disabled browser features in production
**Problem 1 - External Cache Not Working**: ModImportService used system temp folder instead of profile temp directory
**Root Cause**: ModImportService.ImportAsync() called `Path.GetTempPath()` directly, bypassing ProfilePathService configuration
**Backend Changes**:
- ModImportService: Injected IProfilePathService, replaced `Path.GetTempPath()` with `_profilePathService.TempDirectory` (line 84)
- ModImportService: Added explanatory comment about respecting external cache configuration
- WebViewInitializer: Disabled context menus in production (`AreDefaultContextMenusEnabled = isDevelopment`)
- WebViewInitializer: Added ConfigureKeyboardShortcutBlocking() using JavaScript injection
- Blocked shortcuts: Ctrl+F/G/H/J/P/S/U/0/+/-, F12, Ctrl+Shift+I (preserves editing shortcuts)
- JavaScript injection uses AddScriptToExecuteOnDocumentCreatedAsync for automatic execution on page load
**Frontend Changes - Component Renaming**:
- WorkflowQueueTable → ModImportWorkflowTable (file, component, props, CSS classes)
- ModImportQueueScreen → ModImportWorkflowScreen (file, component, CSS classes)
- Updated index.ts exports, ModHierarchicalView.tsx imports
- All CSS class names updated: `workflow-queue-table` → `mod-import-workflow-table`, `mod-import-queue-screen` → `mod-import-workflow-screen`
**Documentation Updates**:
- TESTING_GUIDE.md: Replaced `Path.GetTempPath()` examples with test project directory approach
- TESTING_GUIDE.md: Added using System.Reflection; import for Assembly.GetExecutingAssembly()
- TESTING_GUIDE.md: Added explanatory comments about avoiding system temp in tests
**Consistency Improvements**:
- Component names now follow `ModImportWorkflow` prefix pattern
- CSS class names follow BEM convention with proper component prefixes
- Testing examples demonstrate correct temporary file handling patterns
**Security Enhancements**:
- Production mode: Context menus disabled, DevTools inaccessible, browser shortcuts blocked
- Development mode: All features enabled for debugging
- Detection: `.dev` file presence or `ASPNETCORE_ENVIRONMENT=Development`
**Files Changed**: 8 files (4 backend C#, 3 frontend TypeScript, 1 documentation)
**Pattern**: Service injection for path management, JavaScript injection for browser feature blocking, consistent naming conventions

### Enhanced - 2026-03-01 - Workflow System: Batch Operations, Selection Improvements & UX Polish ⭐⭐⭐
Enhanced workflow queue with batch operations for bulk actions, improved selection behavior, and user-friendly terminology updates.
**Impact**: ✅ Users can batch delete/resume workflows, select all workflow states, clearer UI terminology
**Backend Changes**:
- WorkflowRepository: Added DeleteBatchAsync, GetByIdsAsync with parameterized SQL IN clauses
- WorkflowFacade: Added BATCH_DELETE_WORKFLOWS, BATCH_RESUME_WORKFLOWS endpoints with partial failure handling
- BatchOperationResult: Returns detailed results with successful/failed items for user feedback
- Cleanup Integration: Batch delete calls handler.CancelAsync() for proper temp file cleanup
**Frontend Changes**:
- workflowService: Added batchDeleteWorkflows, batchResumeWorkflows methods with BatchOperationResult interface
- ModImportQueueScreen: Implemented batch confirm and batch delete handlers with proper error handling
- WorkflowQueueTable: Fixed selection to allow all workflow states (removed Completed/Failed/Cancelled restrictions)
- WorkflowQueueTable: Fixed WaitingForInput state - separated Confirm and Edit buttons for simultaneous access
- CompactButton: Unified disabled styles across all variants with consistent gray appearance
**UX Improvements**:
- Terminology: "Import Queue" → "Mod Imports" for more user-friendly language
- Empty state: "No imports in progress" → "No mods being imported"
- Button text: "Open Import Queue" → "Mod Imports"
- Empty table: Enhanced placeholder styling with transparent background and softer text colors
- Conditional spinner: Only animates when tasks are actively processing (not when idle or all waiting)
- Border polish: Removed action bar border, reduced border-radius for consistent visual style
**I18n Updates**:
- en.json: Updated modManagement.title.importQueue → modManagement.title.modImports
- en.json: Updated button, empty state, and hint text for friendlier tone
- cn.json: Updated Chinese translations to match new terminology
- HelpWindow.tsx: Updated "Import Queue" → "Mod Imports" in documentation
**Theme Support**:
- All hardcoded colors converted to CSS variables (--color-bg-elevated, --color-border-secondary, etc.)
- Full light/dark theme compatibility for workflow screens
**Files Changed**: 15 files across backend (C#), frontend (TypeScript), i18n, documentation
**Pattern**: Batch operations with parameterized SQL, partial failure handling, user-friendly terminology
**Documentation**: Updated WORKFLOWS.md with batch operations pattern, updated BACKEND.md with new endpoints

### Fixed - 2026-03-01 - Category System: Event-Driven Cache Invalidation & UI Polish ⭐⭐⭐⭐
Complete fix for category tree count display issues, event-driven architecture improvements, and category UI enhancements.
**Impact**: ✅ Category counts now update correctly after operations, no duplicate events, better UX with proper icons and "Unclassified" labels
**Problem**: Category tree counts showed stale data after drag-drop operations due to race condition between manual refresh and event-based refresh
**Root Cause Analysis**:
- Frontend manually called refreshCategoryTree() immediately after operations
- Manual call hit backend BEFORE cache was invalidated, getting stale cached data
- Backend event emission arrived later, but frontend already showed wrong counts
- CategoryEventHandler was registered in DI but never instantiated (lazy loading)
**Architecture Changes**:
- **Event-Driven Flow**: MOD.CATEGORY_UPDATED → CategoryEventHandler → InvalidateTreeCache → CATEGORY.CATEGORY_TREE_UPDATED
- **Eager Initialization**: CategoryEventHandler now initialized on startup in ProfileServiceRouter
- **Removed Duplicate Events**: CategoryFacade no longer emits events (service layer handles it)
- **Single Source of Truth**: All cache invalidation through CategoryService.InvalidateTreeCache()
**Backend Changes**:
- CategoryService: Added IProfileEventBus injection, InvalidateTreeCache() now emits CATEGORY_TREE_UPDATED
- CategoryEventHandler: New service subscribing to MOD.CATEGORY_UPDATED, invalidates cache
- CategoryServiceExtensions: Added InitializeCategoryEventHandler() for eager initialization
- ProfileServiceRouter: Calls InitializeCategoryEventHandler() after building service provider
- CategoryFacade: Removed duplicate event emissions, removed IProfileEventBus dependency
- CategoryEvents.cs: New file defining CATEGORY_TREE_UPDATED event constant
- MigrationFacade: Fixed event reference from ModEvents to CategoryEvents
**Frontend Changes**:
- categoryOperations.ts: Removed manual refreshCategoryTree() call, relies on event-driven refresh
- ModList.tsx: Always shows category tag, displays "Unclassified" for mods without category
- ModPreviewPanel.tsx: Changed icon from FileTextOutlined to FolderOutlined, always shows category
- TreeNodeConverter.tsx: Changed leaf node icon from UserOutlined to FileOutlined for visual distinction
- TreeNodeConverter.css: Reduced icon container margins from 10px to 4px for better visual alignment
- CategoryScreen.tsx: Added debouncing (500ms) for category name validation, sub-category defaults (parent name prefix, inherit thumbnail)
- CategoryContextMenu.tsx: Reordered menu (Sub-Category, Root-Category, -, Edit, Delete), added i18n
**Testing**:
- CategoryEventHandlerTests: 5/5 tests passed ✅
- CategoryServiceCacheTests: 4/4 tests passed ✅
- CategoryCacheInvalidationIntegrationTests: 3/3 tests passed ✅
**I18n**:
- en.json: Added category.tree.addSubCategory, addRootCategory, edit, delete, unclassified
- cn.json: Added Chinese translations for all new keys
**UI/UX Improvements**:
- Category tree icons: Parent nodes show folder (open/closed), leaf nodes show file icon
- Icon spacing reduced for better visual alignment with thumbnails
- Sub-category creation defaults: Name starts with "{parentName}-", thumbnail inherited
- Context menu reordered for better clarity
- "Unclassified" label for mods without category instead of showing GUID or hiding
**Performance**: Debounced name validation reduces IPC calls by ~90% during typing
**Files Changed**: 31 files across backend (C#), frontend (TypeScript), tests, and i18n
**Pattern**: Event-driven cache invalidation, eager service initialization, single responsibility principle

### Refactored - 2026-03-01 - Workflow System: Download Manager UI & SQLite Persistence ⭐⭐⭐⭐
Complete workflow system refactoring with download manager style UI, SQLite persistence, and improved user experience with background processing.
**Impact**: ✅ Workflows persist across restarts, better UX with instant feedback, metadata pre-filling, background compression
**Architecture Changes**:
- **SQLite Persistence**: Workflows now persist in profile-scoped SQLite database (following ModRepository pattern)
- **Download Manager UI**: Replaced modal wizard with table-based queue interface showing all active imports
- **Background Processing**: Compression happens in background while user edits metadata
- **Metadata First**: Extract metadata immediately, pre-fill form with detected values
- **Context-Based Metadata**: Metadata fields moved into WorkflowContext (no separate ModImportMetadata type)
**Backend Changes**:
- WorkflowRepository: Converted from in-memory Dictionary to SQLite with raw ADO.NET
- ModImportWorkflowHandler: Refactored to use IFileHelper for testability
- New Flow: ExtractMetadata → CompressFolder (background) → WaitingForUserConfirmation → ImportMod
- IPC Handlers: Added UPDATE_WORKFLOW_CONTEXT, replaced PROVIDE_METADATA with CONTINUE_WORKFLOW
- Profile-scoped storage: Each profile has isolated workflow database
**Frontend Changes**:
- ModImportQueueScreen: Download manager style with WorkflowQueueTable
- WorkflowQueueTable: Table view with progress bars, inline metadata editing, status indicators
- useWorkflowQueue: Loads workflows from database on mount, real-time event subscriptions
- Removed ModManagementScreen: Legacy wrapper removed, functionality moved to ModHierarchicalView
- FolderImportButton: Triggers workflow creation
**User Experience**:
- Metadata form shows immediately with pre-filled values (folder name, file count)
- User can edit metadata while compression happens in background
- Workflows persist across application restarts
- Clear completed button to remove finished workflows
- Progress bars show workflow step progress
**IFileHelper Integration**:
- Added methods: FileExists(), DirectoryExists(), DeleteFileAsync(), GetFiles()
- All File/Directory access in ModImportWorkflowHandler now uses IFileHelper for unit testing
**Files Changed**: 20+ files across backend (C#), frontend (TypeScript), and documentation
**Pattern**: SQLite persistence, download manager UI, background processing, testable services with IFileHelper
**Documentation**: Updated WORKFLOW_ARCHITECTURE.md, AI_GUIDE.md with new flow and examples

### Refactored - 2026-02-26 - Classification → Category: Major Module Refactoring ⭐⭐⭐⭐⭐
Complete refactoring from "Classification" to "Category" terminology across the entire codebase, separating Category module from Mods module, implementing IMemoryCache for performance, and fixing migration system.
**Impact**: ✅ Clearer terminology, better module separation, improved performance with caching, all tests passing (24/24)
**Module Changes**:
- **Separated Category Module**: Extracted from `Modules/Mods` to `Modules/Category`
  - New namespace: `D3dxSkinManager.Modules.Category`
  - Services: `CategoryService`, `CategoryRepository`, `CategoryFacade`
  - Models: `CategoryInfo`
  - Tests: `CategoryServiceTests` (11 unit tests), `CategoryRepositoryTests` (14 integration tests)
- **Renamed Mods → Mod**: Singular naming convention across all modules
  - `D3dxSkinManager.Modules.Mod` (was `Modules.Mods`)
  - All modules now use singular names: Mod, Category, Plugin, Tool, Setting
**Performance Improvements**:
- **IMemoryCache Integration**: Replaced manual cache with Microsoft.Extensions.Caching.Memory
  - Profile-specific cache keys: `CategoryTree_{profileId}`
  - Automatic cache invalidation on updates (no more manual `RefreshTreeAsync`)
  - 5-minute sliding expiration
  - Singleton cache shared across profile-scoped services
- **Removed RefreshTreeAsync**: Cache invalidation handles tree updates automatically
**Frontend Changes**:
- Translation keys: `t('Category.xxx')` → `t('category.xxx')`
- CSS classes: `.Category-*` → `.category-*`
- Service methods: `createNode` → `createCategory`, `updateNode` → `updateCategory`
- Variable names: All "node" references changed to "category"
**Backend Changes**:
- Method renames: `MapToNode` → `MapToCategory`, `MoveNodeAsync` → `MoveCategoryAsync`
- Comments updated: "node" → "category" throughout
- CreateAsync pattern: Now accepts pre-generated GUIDs for better transaction control
**Migration Fixes**:
- Fixed directory path: `"Category"` → `"classification"` (matches Python source)
- Fixed variable naming: `categoryNames` properly used as `List<string>`
- Fixed parent ID assignment when category already exists
- Parser: `IPythonCategoryFileParser` reads `classification/` directory
**Test Coverage**:
- CategoryServiceTests: 11 unit tests (GUID generation, name uniqueness, CRUD operations, cache invalidation)
- CategoryRepositoryTests: 14 integration tests (SQLite operations, hierarchical queries, batch operations)
- All 24 tests passing with proper async/await, FluentAssertions, and mocking
**Files Changed**: 100+ files across backend (C#), frontend (TypeScript), tests, and documentation
**Pattern**: Singular module naming, IMemoryCache for caching, pre-generated GUIDs, comprehensive test coverage
**Documentation**: Updated CATEGORY_SYSTEM.md, MIGRATION_PARSER_ARCHITECTURE.md, architecture docs

### Added - 2026-02-25 - Strong Typing: Module-Matched Event Types & Removed Legacy Operation System ⭐⭐⭐⭐
Enhanced event subscription with module-to-event-type matching, preventing mismatched module/event combinations at compile-time. Removed obsolete operation notification system (replaced by TaskQueue).
**Impact**: ✅ Impossible to use wrong event types with modules, removed ~400 lines of unused code, cleaner architecture
**TypeScript Type Safety**:
- **ModuleEventTypeMap**: Maps each module to its valid event type enum
- **useEventSubscription**: Now enforces `T extends ModuleEventTypeMap[M]` - event type MUST match the module
- **Compile-time errors**: Prevents `Module.MOD` with `TaskQueueEventType`, prevents string literals
- **IntelliSense support**: IDE autocomplete shows only valid event types for each module
**Removed Legacy System**:
- Deleted `OperationContext.tsx` - replaced by TaskQueue system
- Deleted `operation.types.ts` - no longer needed
- Deleted `OperationMonitorScreen.tsx` - functionality in TaskQueue
- Deleted `OPERATION_NOTIFICATION_SYSTEM.md` - obsolete documentation
- Removed `OperationProvider` from App.tsx
- Removed operation-based status bar props (operationName, activeOperationCount, onProgressClick)
- Removed operation monitor keyboard shortcut (Ctrl+Shift+O)
**Type Safety Examples**:
```typescript
// ✅ CORRECT: Module.MOD requires ModEventType
useEventSubscription(Module.MOD, ModEventType.REFRESHED, () => { ... });

// ❌ WRONG: String literal - TypeScript error
useEventSubscription(Module.MOD, 'REFRESHED', () => { ... });

// ❌ WRONG: Mismatched module/type - TypeScript error
useEventSubscription(Module.MOD, TaskQueueEventType.PROGRESS, () => { ... });
```
**Files**: eventBus.ts (+ModuleEventTypeMap), useEventSubscription.ts, App.tsx, OPERATION_NOTIFICATION_SYSTEM.md (deleted), OperationContext.tsx (deleted), operation.types.ts (deleted), OperationMonitorScreen.tsx (deleted)
**Pattern**: Compile-time type safety over runtime checks, TaskQueue system for progress tracking
**Documentation**: Updated AI_GUIDE.md with ModuleEventTypeMap examples

### Refactored - 2026-02-25 - Event System: Module + Type Pattern & Handler-Centric Performance Cache ⭐⭐⭐⭐⭐
Completely refactored event system to use Module + Type pattern (matching IpcRequest structure), implemented handler-centric lazy caching for optimal performance, and removed all CUSTOM_EVENT usage for explicit type safety.
**Impact**: ✅ Consistent architecture across IPC/Events, O(1) cached lookups, thread-safe with ConcurrentDictionary, full TypeScript type safety
**Architecture Changes**:
- **Module + Type Pattern**: Events now have separate `Module` and `Type` fields (e.g., `MOD` + `LOADED` instead of `"MOD_LOADED"`)
- **No Module Prefixes**: Event type constants have NO module prefix (`LOADED` not `MOD_LOADED`, `PROGRESS` not `TASK_PROGRESS`)
- **Handler-Centric Cache**: `ConcurrentDictionary<HandlerId, ConcurrentDictionary<EventId, bool>>` for lazy evaluation
- **CUSTOM_EVENT Removed**: Every event now has explicit module and type
**Backend Changes**:
- EventMessage: Refactored from `{EventType, EventName, Data}` to `{Id, Module, Type, Payload, Timestamp}`
- EventBus: Changed from 1-parameter to 2-parameter registration `RegisterHandler(modulePattern, typePattern, handler)`
- EventBus: Handler-centric cache with no invalidation on registration, single TryRemove on unregistration
- ModuleNames.cs: Created centralized module name constants (CORE, MOD, TASK_QUEUE, DROP_ZONE, etc.)
- Event constants: Removed module prefixes from all event types (ModEvents.LOADED, TaskQueueEvents.PROGRESS, DropZoneEvents.CLICK)
- IpcCommunicationHandler.SendNotification: Changed signature to `(module, type, payload)` from `(type, data)`
- EventBusIpcBridge: Updated to call SendNotification with module and type at top level
- DropZoneOverlay: Updated all 6 event emissions to use Module + Type pattern
- ProfileEvents, MigrationEvents, ToolsEvents: Replaced CUSTOM_EVENT with explicit event types
- Removed: ContextEvents.cs (duplicate), PluginEvents.cs (plugins use dynamic names)
**Frontend Changes**:
- Event interface: Changed from `{type, data}` to `{module, type, payload}`
- Module enum: Added separate Module enum with all module names
- Event type enums: Separate enums per module (SystemEventType, ModEventType, TaskQueueEventType, etc.)
- EventPayloadMap: Type-safe payload mapping for compile-time checking
- eventBus.subscribe: Changed to 2-parameter `(module, type, handler)` from `(type, handler)`
- bridgeService: Updated to extract `{module, type, payload}` from top level (not nested in data)
- All components: Updated to use Module + specific event type enums
**Performance Optimization**:
- Cache Structure: Handler → (Event → matches: bool) enables lazy evaluation
- First emit: Pattern matching + cache store per handler
- Subsequent emits: O(1) cache lookup per handler
- Registration: Create empty cache (no iteration/invalidation)
- Unregistration: Single TryRemove operation
- Thread-safe: All operations use ConcurrentDictionary
**Files**: EventBus.cs, EventMessage.cs, EventEmitter.cs, ModuleNames.cs, EventBusIpcBridge.cs, IpcCommunicationHandler.cs, DropZoneOverlay.cs, ModEvents.cs, TaskQueueEvents.cs, ProfileEvents.cs, MigrationEvents.cs, ToolsEvents.cs, SettingsEvents.cs, ModFacade.cs, TaskQueueService.cs, ProfileFacade.cs, MigrationFacade.cs, ToolsFacade.cs, eventBus.ts, bridgeService.ts, useEventSubscription.ts
**Pattern**: Module + Type consistency across IPC/Events, lazy handler-centric caching, explicit types over CUSTOM_EVENT
**Documentation**: Updated AI_GUIDE.md with complete event system patterns, EventBus performance details, Module + Type examples

### Refactored - 2026-02-25 - ModFacade Cleanup: Remove Obsolete Message Types and Add Clipboard Check ⭐⭐⭐
Removed legacy pre-classification and unused message types, migrated to modern classification tree system, and added clipboard image validation to prevent errors.
**Impact**: ✅ 25.3% ModFacade size reduction (1,208→902 lines), cleaner architecture, better UX for paste operations
**Removed Message Types**:
- `GET_BY_OBJECT`, `GET_OBJECT_NAMES` - Legacy pre-classification methods (replaced by hierarchical classification tree)
- `REFRESH_CLASSIFICATION_TREE` - Moved to direct service dependency in MigrationFacade (eliminates facade-to-facade coupling)
- `REORDER_CLASSIFICATION_NODE` - Unused message handler
**Added Features**:
- `CHECK_CLIPBOARD_HAS_IMAGE` - Backend clipboard validation using STA threading
- Disabled "Paste from Clipboard" menu item when no image present (prevents error logs)
**Backend Changes**:
- ImageService: Added `CheckClipboardHasImageAsync()` method
- MigrationFacade: Injected `IClassificationService` directly instead of calling `ModFacade.RefreshClassificationTreeAsync()`
- Removed obsolete methods from `IModFacade` interface
- Removed 3 obsolete unit tests (~52 lines)
**Frontend Changes**:
- BatchEditDialog: Migrated from `modService.getObjectNames()` to `classificationService.getClassificationTree()` with `getAllLeafNodes()`
- ModPreviewPanel: Added async clipboard check on context menu open, disable paste when clipboard empty
- modService: Removed `getModsByObject()` and `getObjectNames()`, added `checkClipboardHasImage()`
**Architecture Improvement**: Direct service dependencies where appropriate (MigrationFacade → ClassificationService) instead of facade-to-facade calls
**Files**: ModFacade.cs, ImageService.cs, MigrationFacade.cs, ModFacadeTests.cs, modService.ts, BatchEditDialog/index.tsx, ModPreviewPanel.tsx
**Pattern**: Thin facade for IPC routing, rich service layer for business logic, STA threading for Windows clipboard access

### Added - 2026-02-25 - Comprehensive Tag Management System with Color Customization ⭐⭐⭐⭐
Implemented a complete tag management system with master Tags table, color customization, real-time synchronization, and intelligent color pre-generation.
**Impact**: ✅ Professional tag organization, theme-aware styling, optimized performance (eliminated N+1 queries), consistent UX
**Architecture**:
- **Two-table system**: Tags table (master with colors) + Mods.Tags (JSON array references)
- **Centralized color management**: `tagColorsMap` state shared across MultiTagInput and TagManagementDialog
- **Pre-generation**: Random colors assigned when typing new tags, saved to database on mod save
- **Bulk loading**: `PopulateTagMetadataBulkAsync()` loads all tag colors with mod list (eliminates N+1 queries)
**Backend** (C#):
- New `Tag` model with name, color, timestamps
- `TagRepository` with CRUD operations, search, usage tracking
- `ModFacade.PopulateTagMetadataBulkAsync()` for bulk tag metadata loading
- `ModInfo.TagsWithMetadata` property for pre-loaded tag colors
- IPC handlers: `GET_ALL_TAGS`, `UPSERT_TAG`, `DELETE_TAG`
**Frontend** (React/TypeScript):
- **TagManagementDialog**: Visual tag selector with color picker, delete, theme-aware borders, consistent UI (always show controls)
- **TagChip**: Reusable colored tag component with default fallback styling
- **MultiTagInput**: Autocomplete with instant color feedback, pre-generates colors for new tags
- **Color palette**: 10 theme-compatible colors matching backend
- **Real-time sync**: Color changes in dialog immediately reflected in input
- **Smart save**: Only saves to database for existing tags (deferred save for new tags)
**UX Improvements**:
- Debounced color saves (500ms) to reduce database writes
- Tags show "+x more" when exceeding display limit
- Compact grid layout for tag management dialog
- Border radius reduced from 10px to 4px (less round, more modern)
- Theme-aware borders: light (#d9d9d9) / dark (#424242) for unselected state
- Deleted tags automatically removed from autocomplete
**Files**: TagRepository.cs, ModFacade.cs, ModInfo.cs, TagMetadata.cs, TagManagementDialog.tsx/css, TagChip.tsx/css, MultiTagInput.tsx/css, ModList.tsx/css, ModEditScreen.tsx, TagsSection.tsx, BatchEditDialog/index.tsx, modService.ts, SettingsView.tsx, mod.types.ts, en.json
**Dependencies**: lodash-es (for debounce utility with tree-shaking)
**Pattern**: Frontend manages color palette; backend stores colors; centralized state via tagColorsMap

### Refactored - 2026-02-25 - Remove Debug Console Logs from Frontend ⭐⭐
Cleaned up all debug console.log statements throughout the frontend codebase.
**Impact**: ✅ Cleaner console output, better production readiness, preserved intentional logging
**Changes**:
- Removed 37 debug console.log statements from 12 files
- Preserved console.error, console.warn, console.info for production logging
- Preserved logger.ts implementation (uses console.log internally)
**Files**: useDropZone.ts (13 logs), AppInitializer.tsx (8 logs), ClassificationScreen.tsx (5 logs), CompactUpload.tsx (3 logs), GameLaunchTab.tsx (2 logs), and 7 others
**Pattern**: Use logger utility for structured logging instead of raw console.log

### Fixed - 2026-02-25 - ModEditScreen Form Initialization and Dropdown Styling ⭐⭐
Fixed form not populating with initial values when editing mods and improved tag dropdown styling.
**Impact**: ✅ Form fields now properly initialize with mod values, tag dropdown has better dark theme appearance
**Changes**:
- ModEditFormContent now accepts `mod` as prop for initialization (follows `undefined` convention, not `null`)
- Simplified initialization logic - triggers when `mod` prop is available
- Removed unnecessary `useStableRef` usage
- Enhanced MultiTagInput dropdown: darker background, subtle selection highlight, 1px gaps between items
- Removed debug console.log statements
**Pattern**: useSlideInScreen captures content once - pass initial values as props, manage changing state internally
**Files**: ModEditScreen.tsx, TagsSection.tsx, MultiTagInput.css
**Docs**: Updated AI_GUIDE.md Slide-In Screen Pattern section with clarification on passing initial value props

### Fixed - 2026-02-24 - Classification-Filtered Mod List Refresh After Deletion ⭐⭐⭐
Fixed mod list not refreshing properly when deleting a mod while viewing a classification. Now refreshes both the main mod list and the classification-filtered mods list.
**Impact**: ✅ Deleted mods immediately disappear from both lists, UI stays in sync with backend state
**Component**: ModsContext.tsx - deleteMod now refreshes classification-filtered mods when a classification is selected
**Pattern**: Follows same refresh logic as handleModsRefreshAfterCategoryChange in ModHierarchicalView

### Fixed - 2026-02-24 - Migration & Classification Integrity Improvements ⭐⭐⭐⭐
Fixed critical migration issues and improved data integrity for classification tree.
**Impact**: ✅ Idempotent migration, proper ID-based references, orphaned node handling
**Migration Changes**:
- Step 3 now checks database for existing classifications by name (won't create duplicates)
- Step 5 queries database for classification ID by object name (no in-memory mapping)
- Mods now store classification IDs instead of names for referential integrity
- Auto-detection rules use classification IDs
- Both steps are idempotent (safe to re-run)
**Classification Service**:
- Orphaned classifications (invalid parentId) now treated as root nodes
- Unclassified mods detection includes invalid category IDs
- Added `draggable` attribute to ModList items for drag-and-drop
**Frontend**:
- Fixed CompleteStep.css colors for light theme (was using hardcoded white)
- EventBusIpcBridge now uses BeginInvoke (non-blocking UI thread marshaling)
**Files**: MigrationStep3MigrateClassifications.cs, MigrationStep5MigrateModArchives.cs, ClassificationService.cs, ModQueryService.cs, CompleteStep.css, IpcCommunicationHandler.cs
**Removed**: NotificationService (replaced with EventBus pattern)
**Docs**: Updated MIGRATION_ARCHITECTURE.md and TROUBLESHOOTING.md with idempotency and orphaned node handling

### Documentation - 2026-02-23 - Massive Documentation Cleanup ⭐⭐⭐⭐⭐
Aggressive optimization for AI code generation efficiency.
**Impact**: ✅ 70%+ reduction, focused purely on code generation patterns
**Removed:** 7 folders, 32+ obsolete/redundant files
**Optimized:** WORKFLOWS (803→329), DEVELOPMENT (843→179), DESIGN_DECISIONS (874→372),
OPERATION_NOTIFICATION (880→292), GUIDELINES (780→388)
**Final:** 43 files (was 75+), estimated <10K lines (was ~35K)

### Refactored - 2026-02-23 - Complete Data Layer null → undefined Migration ⭐⭐⭐⭐
Comprehensively migrated entire frontend data layer from `null` to `undefined` for absent values while preserving React's `null` for component semantics. This addresses JavaScript/TypeScript best practices where `undefined` is the natural "absence of value".
**Impact**: ✅ Type-safe data layer, clearer semantics, eliminates null vs undefined confusion
**Scope**:
- Services (11 files): classificationService, profileService, modService, languageService, profileConfigService, launchService, migrationService, imageUrlHelper, notification, baseModuleService, etc.
- Hooks (4 files): useDelayedLoading, useDragDrop, useOptimisticUpdate, useStableRef
- Contexts (3 files): ProfileContext, OperationContext, AppInitializer
- Components (9 files): ModsContext, ClassificationTree, ModList, ModHierarchicalView, ClassificationPanel operations, etc.
- I18n system: Updated to use undefined for missing translations
**Total**: 26 files with 146 insertions and 122 deletions
**Key improvements**:
- baseModuleService now auto-converts backend `null` to frontend `undefined`
- All service return types: `Promise<T | null>` → `Promise<T | undefined>`
- All data state: `Data | null` → `Data | undefined`
- Preserved `null` for: React component returns (`return null;`), DOM refs (`useRef<HTMLElement>(null)`), conditional rendering
- Backend compatibility maintained through automatic null/undefined conversion
**Guideline**: AI_GUIDE.md clearly distinguishes - `undefined` for data layer, `null` for React/DOM requirements

### Fixed - 2026-02-23 - Classification Tree Empty Container Context Menu
Fixed right-click context menu not appearing on empty classification tree container.
**Impact**: ✅ Users can now add root classifications when tree is empty via right-click
**Component**: ClassificationTree.tsx - Changed null to empty string for consistency

### Improved - 2026-02-23 - Classification Category Name Display ⭐⭐⭐
Mod list now displays human-readable category names instead of GUIDs. Optimized refresh logic to only update when necessary using node relationship checks.
**Impact**: ✅ User-friendly category display, efficient updates, follows useDelayedLoading pattern
**Backend**: ModFacade.PopulateCategoryNamesBulkAsync maps IDs to names
**Frontend**: Smart refresh - only when name changes AND (current node OR descendant)

### Fixed - 2026-02-23 - First Boot & Context Menu Issues ⭐⭐
Fixed three critical UX issues: Lazy<T> re-entrancy error on first boot, context menu not appearing on empty classification panel, and context menu not working on empty whitespace in tree.
**Impact**: ✅ Application boots successfully on first run, context menus work consistently
**Backend**: ProfileService - Created internal CreateProfileInternalAsync() to break circular dependency
**Frontend**: ClassificationTree - Fixed visibility logic (null vs undefined) and moved handler to outer container

### Simplified - 2026-02-23 - Logging System Architecture & DI Container ⭐⭐⭐⭐
Simplified logging initialization and removed unnecessary ServiceContainer wrapper. AppEnvironment reads log level from GlobalSettingsService on startup, eliminating complex initialization logic. Replaced ServiceContainer with direct ServiceCollection usage.
**Impact**: ✅ Cleaner architecture, removed ~80 lines of unnecessary code, simpler DI setup
**Components**: AppEnvironment.cs (simplified ReadLogLevel), removed ServiceContainer.cs, ApplicationHost uses ServiceCollection directly
**Frontend**: Improved log level option labels (All → Debug → Info → Warn → Error → Off)

### Implemented - 2026-02-23 - Global Settings Log Level Integration ⭐⭐⭐
Integrated backend C# log level control with global settings. GlobalSettingsService now applies log level changes to LogHelper immediately when updated from UI.
**Impact**: ✅ Dynamic log level control from settings, no restart needed, supports all/debug/info/warning/error/off
**Components**: GlobalSettingsService.cs, GlobalSettings.cs
**How it works**: Settings UI → SettingsFacade → GlobalSettingsService.UpdateSettingAsync → LogHelper.MinimumLevel updated

### Improved - 2026-02-22 - Simplified Centralized Logging ⭐⭐⭐⭐
Reorganized logging to use centralized `data\logs` directory with simple log level-based files. Daily rotation, uses GlobalPathService for path management.
**Impact**: ✅ Simple and maintainable, easier troubleshooting, level-based log files with daily rotation
**Structure**: `data\logs\{level}-{date}.log` (debug, info, warning, error) plus combined `all-{date}.log`
**Documentation**: [architecture/LOGGING_ARCHITECTURE.md](architecture/LOGGING_ARCHITECTURE.md)

### Added - 2026-02-22 - Log Level Configuration ⭐⭐⭐
Implemented configurable log levels based on environment. Development shows Info+, Production shows Warning+. Debug logs filtered by default to reduce console noise.
**Impact**: ✅ Cleaner console output, configurable via D3DX_LOG_LEVEL env var, automatic environment detection
**Components**: LogHelper.cs, AppEnvironment.cs, ApplicationBootstrapper.cs

### Optimized - 2026-02-22 - WinForms UI Performance Improvements ⭐⭐⭐⭐
Implemented double buffering, GPU acceleration enhancements, performance monitoring. Created OptimizedForm class, enhanced WebView2 settings, added IPerformanceMonitor service.
**Impact**: ✅ Smoother UI rendering, no flicker, better responsiveness, performance metrics tracking
**Components**: OptimizedForm.cs, ApplicationHost.cs, WebViewInitializer.cs, PerformanceMonitor.cs

### Fixed - 2026-02-22 - Comprehensive Code Quality Improvements ⭐⭐⭐⭐
Fixed Console.WriteLine usage (5 Infrastructure files → ILogger), NotImplementedException (2 files → graceful returns), frontend services (3 services → extend BaseModuleService).
**Impact**: ✅ Consistent logging, no runtime exceptions, uniform service architecture
**Details**: [changelogs/2026-02/2026-02-22-comprehensive-code-review.md](changelogs/2026-02/2026-02-22-comprehensive-code-review.md)

### Refactored - 2026-02-22 - Frontend Architecture Improvements & Critical Fixes ⭐⭐⭐⭐⭐
Major frontend refactoring based on comprehensive code review (152 files analyzed). Fixed critical architectural issues and anti-patterns.
**Fixed**: SettingsService and SettingsFileService now extend BaseModuleService, removed window.location.reload anti-pattern, added i18n to ProfileSwitcher
**Impact**: ✅ Consistent service architecture, proper state management, no page reloads on profile switch
**Components**: settingsService.ts, settingsFileService.ts, ProfileSwitcher.tsx

### Cleaned - 2026-02-22 - Frontend Code Cleanup & Obsolete Archive Removal ⭐⭐⭐
Comprehensive frontend review and cleanup. Removed unused demo component (174 lines), deprecated Photino type aliases, and 6 obsolete archive files/folders (~2,053 lines).
**Impact**: ✅ Cleaner codebase, reduced maintenance burden, faster navigation
**Removed**: SlideInScreenDemo.tsx, PhotinoMessage/PhotinoResponse aliases, 4 archive docs, 2 archive folders

### Updated - 2026-02-22 - Documentation Cleanup: Photino → WebView2 References ⭐⭐⭐
Comprehensive documentation update across all files. Updated 19+ documentation files to reflect WebView2 migration: changed Photino.NET references to WebView2, photinoService → bridgeService, IPC transport details.
**Impact**: ✅ Accurate documentation for new architecture
**Files**: README.md, QUICKSTART.md, CURRENT_ARCHITECTURE.md, HOW_TO.md, BACKEND.md, FRONTEND.md, DEVELOPMENT.md, PROJECT_OVERVIEW.md, PROJECT_STRUCTURE.md, GUIDELINES.md, WORKFLOWS.md, INTERNATIONALIZATION.md

### Optimized - 2026-02-22 - CustomSchemeHandler & File Dialog Performance ⭐⭐⭐⭐
Optimized CustomSchemeHandler with LRU cache (500 items), content type caching, 4KB buffer streaming. Fixed SystemFileDialogService 2-5 second delay by reusing main UI thread.
**Impact**: ✅ Faster image loading, instant file dialogs, no memory leaks
**Components**: CustomSchemeHandler.cs, LruCache utility, SystemFileDialogService.cs

### Migrated - 2026-02-22 - Photino.NET → WinForms + WebView2 ⭐⭐⭐⭐⭐
Complete migration from Photino.NET to WinForms + WebView2 architecture. New composition layer, middleware pipeline with Lazy<T> caching, profile-scoped services, GPU acceleration.
**Impact**: ✅ Better Windows integration, performance optimizations, standard WebView2 API
**Details**: [docs/technical/winforms-webview2-migration.md](technical/winforms-webview2-migration.md)

### Added - 2026-02-21 - Classification Management with SHA-256 Thumbnail Deduplication ⭐⭐⭐⭐⭐
Complete "Add Classification" feature with thumbnail support, SHA-256 deduplication, IPC-based validation, and file lock detection. FileTransferService for reusable file copying.
**Impact**: ✅ Create classifications with thumbnails, automatic deduplication, duplicate prevention, file lock error handling
**Details**: [changelogs/2026-02/2026-02-21-classification-thumbnail-management.md](changelogs/2026-02/2026-02-21-classification-thumbnail-management.md)

### Added - 2026-02-21 - Complete Internationalization (i18n) System ⭐⭐⭐⭐⭐
Implemented comprehensive bilingual support (English + Chinese) with react-i18next. 507 translation keys per language, 16 components internationalized, flat JSON structure.
**Impact**: ✅ Full bilingual support, easy to add more languages
**Docs**: [features/INTERNATIONALIZATION.md](features/INTERNATIONALIZATION.md), [how-to/ADD_I18N_TO_COMPONENT.md](how-to/ADD_I18N_TO_COMPONENT.md)

### Added - 2026-02-21 - Category-Based Mod Loading with Error Handling ⭐⭐⭐⭐⭐
Auto-unload conflicting mods, comprehensive error code system (backend + frontend), user-friendly error messages for all scenarios.
**Impact**: ✅ No mod conflicts, clear error guidance
**Details**: [changelogs/2026-02/2026-02-21-category-based-loading-error-handling.md](changelogs/2026-02/2026-02-21-category-based-loading-error-handling.md)

### Added - 2026-02-21 - Operation Notification System ⭐⭐⭐⭐⭐
Complete backend → frontend push notification system for real-time progress tracking (0-100%). Status bar integration + operation monitor screen (Ctrl+Shift+O).
**Impact**: ✅ Real-time progress visibility, operation history (last 50)
**Details**: [changelogs/2026-02/2026-02-21-operation-notification-system.md](changelogs/2026-02/2026-02-21-operation-notification-system.md), [features/OPERATION_NOTIFICATION_SYSTEM.md](features/OPERATION_NOTIFICATION_SYSTEM.md)

### Refactored - 2026-02-21 - Declarative Drag & Drop API + Service Layer ⭐⭐⭐⭐⭐
Completely refactored `useDragDrop` with clean declarative API. Auto data extraction, object parameters, ~75% less boilerplate. Added `classificationService` abstraction layer.
**Impact**: ✅ Type-safe, cleaner code, consistent UX
**Details**: [changelogs/2026-02/2026-02-21-drag-drop-api-improvements.md](changelogs/2026-02/2026-02-21-drag-drop-api-improvements.md)

### Fixed - 2026-02-21 - Classification Tree "Drop Into" Easier to Trigger ⭐⭐⭐⭐
Fixed difficult-to-trigger "drop into" mode. Implemented native DOM drag detection with 15% edges / 70% middle zones (was 25%/50%).
**Impact**: ✅ Much easier to create child nodes
**Details**: [changelogs/2026-02/2026-02-21-classification-tree-drag-drop-fix.md](changelogs/2026-02/2026-02-21-classification-tree-drag-drop-fix.md)

### Fixed - 2026-02-21 - Status Bar Mod Count Updates ⭐⭐⭐⭐
Fixed status bar not updating on load/unload or category changes. Unified mod state by moving `ModsProvider` to app-level. Removed duplicate `useModData` hook.
**Impact**: ✅ Real-time mod state from single source of truth
**Bundle Size**: 470.71 KB

### Refactored - 2026-02-20 - Delayed Loading Pattern ⭐⭐⭐⭐
Replaced complex `useOptimisticUpdate` verification with simpler `useDelayedLoading`. Eliminated UI flicker, reduced code by ~250 lines and bundle size by ~1KB.
**Impact**: ✅ Clearer architecture, faster builds, no flicker
**Details**: [changelogs/2026-02/2026-02-20-delayed-loading-refactoring.md](changelogs/2026-02/2026-02-20-delayed-loading-refactoring.md), [features/DELAYED_LOADING_UX_PATTERN.md](features/DELAYED_LOADING_UX_PATTERN.md)

---

## February 2026 - Archived

**Summary**: 30+ changes including drag-drop system, image navigation, archive support, menu components, preview management, window state persistence, and migration fixes.

**See Full Details**: [changelogs/2026-02/february-2026-complete.md](changelogs/2026-02/february-2026-complete.md)

**Highlights**:
- ⭐⭐⭐⭐ Archive 7z Support & Optimistic Update Fixes
- ⭐⭐⭐⭐ Reusable Optimistic Update Hook
- ⭐⭐⭐ Menu Component System (ContextMenu, PopupMenu, usePopupMenu)
- ⭐⭐⭐ Preview Image Management with Context Menu
- ⭐⭐⭐ Windows Gallery Image Navigation & CSS Refactoring
- ⭐⭐⭐ Code Quality Refactoring (removed 40+ `any` types)
- ⭐⭐⭐ Work Directory Refactoring
- ⭐⭐⭐ Dynamic Preview System (`previews/{SHA}/`)
- ⭐⭐ Drag-and-Drop Mod Classification
- ⭐⭐ Window State Persistence
- ⭐⭐ Image Loading with Custom Scheme Handler
- ⭐⭐ Keywords Index Routing System
- ⭐⭐ Migration Archive Storage Fix

---

## January 2026

**Note**: Pre-project conversion month. See Git history for Python version changes.

---

## Version History

| Version | Date | Description |
|---------|------|-------------|
| v2.0 | 2026-02-19 | React conversion complete |
| v1.x | 2024-2025 | Python version (original) |

---

## Archive Navigation

- **February 2026**: [changelogs/2026-02/february-2026-complete.md](changelogs/2026-02/february-2026-complete.md)
- **Detailed Changes**: See `changelogs/YYYY-MM/` folders
- **Management Guide**: [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md)

---

**Current Line Count**: ~105 lines (Target: < 200 lines) ✅
**Last Cleanup**: 2026-02-21
**Next Cleanup**: 2026-03-01 (or when > 150 lines)
