# Changelog — March 2026

> Archived from `docs/CHANGELOG.md` on 2026-07-05. Entries below are verbatim.

### Changed - 2026-03-09 - Unified Error Handling with OperationException ⭐⭐⭐
**Summary**: Replaced ModException and WorkflowException with single OperationException using Code + Parameters pattern for consistent error handling across the entire stack.

#### Unified Exception Pattern
**Impact**: ✅ Single source of truth for error handling, consistent naming, full i18n support
**Features**:
- Single `OperationException` for all operations (mod, workflow, file, etc.)
- Consistent Code + Parameters pattern (backend and frontend aligned)
- camelCase JSON serialization: `{"code":"ERROR_CODE","parameters":{...}}`
- Unified i18n pattern: `errors.{CODE}` for all error translations
- Added `translateErrorMessage()` helper for displaying stored errors

**Backend Changes**:
- OperationException.cs: New unified exception in Core/Exceptions
- OperationException: Properties `Code` (string) + `Parameters` (Dictionary<string, string>)
- OperationException.GetStructuredMessage(): Uses JsonHelper.Serialize() for camelCase
- BaseFacade.cs: Updated IPC error response to use `{ code, parameters }`
- ModCacheService.cs: Migrated to OperationException
- ModDeletionService.cs: Migrated to OperationException
- ModLifecycleService.cs: Migrated to OperationException
- ModImportWorkflowHandler.cs: Migrated to OperationException
- Deleted: ModException.cs, WorkflowException.cs, WorkflowErrorHelper.cs

**Frontend Changes**:
- ErrorDetails: Changed interface to `{ code, parameters }` (was `{ errorCode, data }`)
- OperationError: Updated class to match backend structure
- errorHandler.ts: Updated handleError() to use new property names
- errorHandler.ts: Added translateErrorMessage() for stored error strings
- ModImportWorkflowTable.tsx: Refactored to use translateErrorMessage()

**Documentation Updates**:
- AI_GUIDE.md: Updated error handling section with unified pattern
- BACKEND.md: Updated ModException → OperationException reference
- Version bump: AI_GUIDE v3.4 → v3.5

**Migration Notes**:
- MigrationError kept separate (different purpose: batch operation result DTO)
- All error codes use unified `errors.{CODE}` i18n pattern
- Backend serializes with camelCase for frontend compatibility

### Added - 2026-03-08 - Active Mods View with Orphaned Mod Detection ⭐⭐⭐
**Summary**: Added "Show Loaded Mods" feature with cache-first scanning, orphaned mod detection, and IMemoryCache optimization for performance.

#### Active Mods View ("Show Loaded Mods" Button)
**Impact**: ✅ Users can now view all currently loaded mods in one click with instant subsequent loads
**Features**:
- Scans cache folder first, then matches with database
- Detects orphaned mods (in cache but not in database) for cleanup
- Displays orphaned mods as "Unmanaged [ID]" with i18n support (EN/CN)
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
- English: "Unmanaged [{{id}}]"
- Chinese: "未托管 [{{id}}]"

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
- ModMetadataService.cs:130-131: Added `_eventBus.EmitAsync(ModuleNames.MOD, ModEvents.DELETED, new { Id = id })` after successful deletion

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
**Architecture Alignment**: Configuration now matches documented structure: `work/Mods/{ID}/` instead of directly pointing to Mods folder

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
- Enhanced drag behavior: single mod uses `application/mod-id`, multi-select uses `application/mod-ids` with JSON array
- Visual classes: `.mod-list-item-selected` (primary), `.mod-list-item-multi-selected` (same style as primary)
**Frontend Changes - ModList.css**:
- Multi-selected items use identical styling to primary selection (same blue highlight and border)
- No opacity or filter differences - clean, consistent visual experience
**Frontend Changes - ModListStatusBar.tsx**:
- Shows "X Mods selected" when multiple mods selected (takes priority over active mod display)
- Added i18n support with translations for selection count
**Frontend Changes - CategoryTree.tsx**:
- Added third drag/drop handler for `application/mod-ids` event type
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

