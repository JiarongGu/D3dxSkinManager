# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> For detailed changes, see `changelogs/YYYY-MM/` folders.
> See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) for guidelines.

---

## [Unreleased]

### Added - 2026-02-21 - Complete Internationalization (i18n) System ⭐⭐⭐⭐⭐
Implemented comprehensive bilingual support (English + Chinese) with react-i18next and custom backend integration. 507 translation keys per language, 16 components fully internationalized, flat JSON structure for easy maintenance.
**Key Features**: react-i18next with custom C# backend, auto-language loading from settings, language switcher in Settings panel, flat JSON (easy searching), 507 keys per language (EN/CN 100% parity), 16/35+ components completed
**Components**: ModList, ModListPanel, ModPreviewPanel, ModEditDialog (3 sections), Launch tabs (Game/D3DMigoto), ToolsView, PluginsView, AboutDialog, KeyboardShortcutsDialog, AppHeader, AppStatusBar, SettingsView
**Architecture**: Backend: LanguageService (load/save language files), Frontend: i18n.ts (custom backend), I18nInitializer (loads on startup), languageService.ts (IPC client)
**Translation Files**: `Languages/en.json` + `cn.json` (auto-copied to `data/languages/`)
**Impact**: ✅ Full bilingual support, easy to add more languages, professional Chinese translations
**Docs**: `docs/features/INTERNATIONALIZATION.md`, `docs/how-to/ADD_I18N_TO_COMPONENT.md`

### Added - 2026-02-21 - Category-Based Mod Loading with Comprehensive Error Handling ⭐⭐⭐⭐⭐
Implemented automatic category-based unloading and comprehensive error handling system with user-friendly messages for all error scenarios.
**Key Features**: Auto-unload conflicting mods, error code system (backend + frontend), folder-in-use detection, user-friendly error messages, unknown error handling
**Impact**: ✅ No mod conflicts, clear error guidance, all error paths covered
**Details**: [changelogs/2026-02/2026-02-21-category-based-loading-error-handling.md](changelogs/2026-02/2026-02-21-category-based-loading-error-handling.md)

### Added - 2026-02-21 - Operation Notification System ⭐⭐⭐⭐⭐
Implemented complete backend → frontend push notification system for real-time operation progress tracking. Long-running operations (e.g., mod loading) now report progress (0-100%) to status bar and full operation monitor screen (Ctrl+Shift+O).
**Key Features**: IProgressReporter pattern, push notifications via IPC, React Context state management, status bar integration, operation history (last 50)
**Impact**: ✅ Real-time progress visibility, better UX for long operations
**Details**: [changelogs/2026-02/2026-02-21-operation-notification-system.md](changelogs/2026-02/2026-02-21-operation-notification-system.md)

### Refactored - 2026-02-21 - Simplified Declarative Drag & Drop API + Service Layer ⭐⭐⭐⭐⭐
Completely refactored `useDragDrop` with ultra-clean declarative API and added proper service layer abstraction. Hook automatically extracts data (from `dataTransfer` OR `onData`), manages styling, and uses object parameters. Backend calls now go through `classificationService` instead of direct `photinoService` calls.
**New API Example**:
```tsx
useDragDrop(
  {
    eventType: 'application/mod-sha',
    nodeSelector: '.tree-node',
    allow: 'node',
    onData: ({ target }) => target.textContent?.trim() || '', // Extract from DOM
    onDrop: ({ data, type }) => {
      // data = extracted from onData (replaces dataTransfer)
      console.log('Dropped onto:', data);
    }
  },
  {
    eventType: 'application/tree-node-id',
    nodeSelector: '.tree-node',
    allow: 'all',
    gapThreshold: 0.15,
    classes: { node: 'custom' }, // Optional CSS
    onDrop: ({ data, type }) => {
      // data = from dataTransfer (no onData provided)
      console.log(type === 'gap' ? 'Reorder' : 'Make child');
    }
  }
)
```
**Key Features**:
- **Unified data param**: `data` from dataTransfer OR onData (replaces, not adds)
- **Object parameters**: `{ data, type, target, event }` - clean & self-documenting
- **Auto data extraction**: Both dataTransfer AND DOM extraction handled automatically
- **Rest parameters**: Just pass handler objects directly (no arrays/wrappers)
- **Custom CSS classes**: Optional `classes` per handler for different visual feedback
**Benefits**: ~75% less boilerplate, zero manual data extraction, consistent UX, type-safe
**Bundle Size**: 471.05 kB
**Components Updated**: ClassificationTree, UnclassifiedItem (fully migrated to new API)
**Handler Simplification**:
  - `handleNodeReorder(dragNodeId, dropNodeId, dropToGap)` - Clean 3-param function instead of Ant Design mock objects
  - `handleModClassify(modSha, nodeId)` - Direct params instead of event extraction
  - Removed `handleContainerDrop`/`handleContainerDragOver` (no longer needed)
  - Removed redundant `draggedNodeId` state and `handleDragStart`/`handleDragEnd`
**Service Layer Improvements**:
  - Added `classificationService.moveNode()` - Clean API for moving nodes
  - Added `classificationService.updateNode()` - Update node names
  - Added `classificationService.deleteNode()` - Delete nodes
  - All operations now use service layer instead of direct IPC calls

### Fixed - 2026-02-21 - Classification Tree "Drop Into" Much Easier to Trigger ⭐⭐⭐⭐
Fixed difficult-to-trigger "drop into" mode when dragging nodes to make children in classification tree. Implemented native DOM drag detection that bypasses Ant Design's restrictive thresholds (25% edges). Now uses 15% edges / 70% middle for drop zones - much easier to create child nodes!
**Solution**: Created generic `useDragDrop` hook using native DOM events (same technique as mod drops, which worked well)
**Thresholds**: Top 15% = reorder above | Middle 70% = make child (was 50%) | Bottom 15% = reorder below
**New Hook**: [useDragDrop.ts](../D3dxSkinManager.Client/src/shared/hooks/useDragDrop.ts) - Reusable for any drag/drop scenario!

### Fixed - 2026-02-21 - Status Bar Mod Count Updates ⭐⭐⭐⭐
Fixed status bar not updating when mods are loaded/unloaded or categories changed. Unified mod state management by moving `ModsProvider` to app-level and replacing duplicate `useModData` hook with `useModsContext`. Status bar now accurately reflects real-time mod state from the single source of truth.
**Architecture Change**: `ModsProvider` now wraps entire app (not just mod view), enabling all components to access live mod state
**Files Removed**: `modules/core/hooks/useModData.ts` (duplicate, replaced by `useModsContext`)
**Bundle Size**: 470.71 KB (improved from 470.99 KB)

### Refactored - 2026-02-20 - Delayed Loading Pattern Replaces Complex Verification ⭐⭐⭐⭐
Replaced complex `useOptimisticUpdate` verification with simpler `useDelayedLoading` pattern for loading operations. Eliminated UI flicker for fast operations (<100ms) while reducing code by ~250 lines and bundle size by ~1KB. Removed duplicate load/unload functions from `useModData`. Operations now show loading spinner only if they take longer than threshold - best of both worlds: instant feedback + user feedback for slow operations.
**Impact**: Clearer code architecture, faster builds, no flicker, maintained UX quality
**Details**: [2026-02-20-delayed-loading-refactoring.md](changelogs/2026-02/2026-02-20-delayed-loading-refactoring.md)

### Added - 2026-02-20 - Archive 7z Support & Optimistic Update Fixes ⭐⭐⭐⭐
Added native 7z archive support using SevenZipSharp library. Fixed critical optimistic update bugs: UI not updating on load/unload, category change not unloading mods, and tree count mismatches. Implemented detailed mismatch logging and proper tree count calculation with ancestor detection.
**Details**: [2026-02-20-archive-7z-support-optimistic-updates-fixes.md](changelogs/2026-02/2026-02-20-archive-7z-support-optimistic-updates-fixes.md)

### Added - 2026-02-20 - Reusable Optimistic Update Hook ⭐⭐⭐⭐
Created a powerful, reusable custom React hook for optimistic UI updates with smart verification pattern.
**Hook Features**:
- **Generic pattern**: Works with any data type via TypeScript generics
- **Smart verification**: Configurable delay (default 50ms), only refreshes on state mismatch
- **Custom comparison**: Optional comparison function for precise mismatch detection
- **Auto error recovery**: Automatic revert on operation failure
- **Cancellable**: Can cancel pending verifications
- **Development logging**: Detailed logs in dev mode for debugging
**Specialized variants**:
- `useModOptimisticUpdate()`: Mod-specific with isLoaded state comparison logic
**Implementation**: [useOptimisticUpdate.ts](../D3dxSkinManager.Client/src/modules/mods/hooks/useOptimisticUpdate.ts) - Reusable optimistic update pattern
**Refactored operations using this hook**:
- Load/Unload mod operations - instant UI feedback with smart verification
- Drag-drop classification moves - smooth category updates without jarring refresh
**Benefits**: Cleaner code, consistent UX pattern, easier to add optimistic updates to new features

### Added - 2026-02-20 - Multi-Point Verification for Classification Updates ⭐⭐⭐⭐
Implemented comprehensive 3-point verification system that checks mod category, mod list, AND tree counts independently.
**Smart Verification System**:
- **Point 1**: Verify the specific mod's category was updated correctly
- **Point 2**: Verify the entire mod list has no category mismatches
- **Point 3**: Verify classification tree counts are accurate (calculated backend values)
- **Selective refresh**: Only refreshes what's actually wrong (mod list OR tree, never both unnecessarily)
**Performance Benefits**:
- **Instant UI update**: Category changes immediately on drag-drop
- **Parallel verification**: All 3 checks run simultaneously after 50ms
- **No unnecessary refreshes**: If all 3 match, nothing refreshes - silky smooth!
- **Detailed logging**: Console shows exactly which check failed and what mismatched
**Implementation**:
- [ModsContext.tsx:746-858](../D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx#L746-L858) - Multi-point verification with selective refresh
- Uses `Promise.all()` to fetch mod list and tree in parallel for fast verification
**UX Impact**: Drag-drop is instant, tree counts update correctly, and UI only refreshes when there's an actual problem

### Added - 2026-02-20 - Optimistic UI with Smart Verification ⭐⭐⭐
Implemented instant, smooth UI updates with intelligent backend verification - best of both worlds!
**Performance Improvements**:
- **Instant feedback**: UI updates immediately on button click (no waiting for backend)
- **Smart verification**: Fetches entire mod list after 50ms, only refreshes UI if state differs
- **No unnecessary updates**: If backend state matches optimistic update, no UI change (smooth!)
- **Error recovery**: Automatic revert if operation fails
- **Multi-view sync**: Updates propagate to all views (main list, classification filtered, selected mod)
**UX Benefits**: Buttery smooth experience with safety net - instant feedback + guaranteed consistency
**Implementation**: [ModsContext.tsx:657-727](../D3dxSkinManager.Client/src/modules/mods/context/ModsContext.tsx#L657-L727) - Using `useModOptimisticUpdate` hook

### Added - 2026-02-20 - Enhanced UI for Load/Unload Operations ⭐⭐
Improved visual design and usability of mod load/unload operations across all views with better consistency.
**UI Improvements**:
- Redesigned loaded indicator: Clean green "LOADED" tag next to mod name (consistent with Ant Design)
- Increased action button icons from 14px to 18px for better visibility
- Enhanced ModActionButtons: Larger buttons (size="medium") with min-width 80px
- Improved ModPreviewPanel tags: Larger icons (16px) and better padding
- Fixed vertical alignment: All buttons and content properly centered
- Added tooltips to action buttons for better UX
**Impact**: More prominent loaded state visibility, better visual consistency, and easier interaction with mod controls
**Files**: [ModList.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModListPanel/ModList.tsx), [ModActionButtons.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModActionButtons.tsx), [ModPreviewPanel.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/ModPreviewPanel.tsx)

### Fixed - 2026-02-20 - Load Mod Complete Implementation with UI Status Display ⭐⭐⭐
Fixed three critical bugs preventing Load Mod from working correctly:
1. **Archive extraction error**: "Cannot determine compressed stream type" for extensionless archives
2. **Database schema error**: "no such column: IsLoaded" trying to update non-existent column
3. **UI display bug**: isLoaded/isAvailable always false - missing PopulateStatusFlagsBulk calls

**Solutions**:
- Created dedicated ArchiveService with magic byte detection (ZIP, 7Z, RAR v4/v5, TAR, GZIP, BZIP2)
- Auto-updates ModInfo.Type during extraction if missing/incorrect
- Stream-based extraction handles extensionless files
- Made SetLoadedStateAsync a no-op (IsLoaded determined dynamically from file system)
- Added PopulateStatusFlagsBulk to all methods returning ModInfo (GetByClassification, GetUnclassified, GetByObject, Search, GetById)

**Impact**: Load Mod function fully working with correct UI status display
**New Service**: [ArchiveService.cs](../D3dxSkinManager/Modules/Core/Services/ArchiveService.cs) - Archive detection & extraction
**Fixed**: [ModFacade.cs](../D3dxSkinManager/Modules/Mods/ModFacade.cs) - All query methods now populate status flags

### Added - 2026-02-20 - Preview Image Management with Context Menu ⭐⭐⭐
Added comprehensive preview image management with right-click context menu for thumbnail assignment, file operations, and image import with persistent path memory. Fixed ImportPreviewImageAsync to use correct `previews/{SHA}/` directory structure. Enhanced file dialog path memory to persist across sessions via GlobalSettings.
**Key Changes**: Set thumbnail, delete preview, open in explorer, copy path, add from file (persistent path memory via global.json), clipboard paste detection, fixed import path bug
**Details**: [changelogs/2026-02/2026-02-20-preview-image-management.md](changelogs/2026-02/2026-02-20-preview-image-management.md)

### Added - 2026-02-20 - Windows Gallery Image Navigation & CSS Refactoring ⭐⭐⭐
Implemented modern image navigation with hover-triggered buttons, refactored all inline styles to organized CSS, added comprehensive light/dark theme support.
**Key Changes**: Previous/Next navigation on hover, image counter (1/5), ~150 lines inline styles → CSS classes, bluish-gray buttons for light theme
**Details**: [changelogs/2026-02/2026-02-20-image-navigation-css-refactoring.md](changelogs/2026-02/2026-02-20-image-navigation-css-refactoring.md)

### Changed - 2026-02-20 - Drag-and-Drop Refinements & Ant Design Deprecation Fixes ⭐⭐
Refined drag-and-drop feature with complete refresh flow, fixed Ant Design deprecation warnings, centered loading spinners, and refactored duplicate code.
**Key Changes**: Unclassified drops now supported, auto-unload on category change, List component replaced with custom div implementation
**Details**: [changelogs/2026-02/2026-02-20-drag-drop-refinements-deprecation-fixes.md](changelogs/2026-02/2026-02-20-drag-drop-refinements-deprecation-fixes.md)

### Added - 2026-02-20 - Drag-and-Drop Mod Classification ⭐⭐

Implemented drag-and-drop functionality to change mod categories by dragging mods from ModList to ClassificationTree nodes.

**Features**:
- Drag any mod from ModList panel
- Drop onto any ClassificationTree node to change category
- Visual feedback: **Entire tree node item** highlights on hover during drag (matches clickable area)
- Success message shows which mod was moved
- Backend: New `UPDATE_CATEGORY` IPC endpoint
- Frontend: [modService.updateCategory()](../D3dxSkinManager.Client/src/modules/mods/services/modService.ts:151-160)

**Implementation**:
- Backend: [ModFacade.UpdateCategoryAsync()](../D3dxSkinManager/Modules/Mods/ModFacade.cs:248-262)
- Frontend handlers: [useClassificationTreeOperations.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/useClassificationTreeOperations.tsx:301-344)
- Drag events: [ModList.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModListPanel/ModList.tsx:258-266)
- Drop zones: [ClassificationTree.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/ClassificationTree.tsx:98-166) (event delegation to `.ant-tree-node-content-wrapper`)

**Benefits**: Faster mod organization, intuitive UI workflow, reduces need for edit dialogs

### Added - 2026-02-20 - Menu Component System (ContextMenu, PopupMenu, usePopupMenu) ⭐⭐⭐

Created a custom menu system to replace Ant Design Dropdowns with better control over animations, positioning, and theme consistency.

**New Components**:
- Frontend: [ContextMenu](../D3dxSkinManager.Client/src/shared/components/menu/ContextMenu.tsx) - Low-level context menu with manual position control
  - Smart positioning with viewport edge detection
  - Smooth vertical expand animations (top-down/bottom-up)
  - Theme-aware styling with proper hover states
  - Closes on scroll or outside click
  - Supports icons, danger state, disabled state, dividers
- Frontend: [PopupMenu](../D3dxSkinManager.Client/src/shared/components/menu/PopupMenu.tsx) - Simple wrapper for right-click menus
  - Automatically manages position from mouse events
  - Best for simple right-click menus with static items
- Frontend: [usePopupMenu](../D3dxSkinManager.Client/src/shared/components/menu/usePopupMenu.ts) - Hook for complex menu scenarios
  - Provides `show(event)`, `hide()`, `visible`, `position` state
  - Best for tracking context (which item was clicked)
  - Used in ModList for mod-specific menus

**Replaced Ant Design Dropdowns**:
- Classification tree context menu → ContextMenu with usePopupMenu hook
- Mod list 3-dot menu → Right-click menu with usePopupMenu hook (removed 3-dot button)
- Profile switcher dropdown → ContextMenu with manual positioning

**UI Updates**:
- 3-panel layout backgrounds: dark-light-dark pattern
  - Left panel (Classification): `var(--color-bg-spotlight)` - dark
  - Center panel (ModList): `var(--color-bg-elevated)` - light
  - Right panel (Preview): `var(--color-bg-container)` - dark
- Center panel (ModList): Fixed width 450px, right panel takes remaining space
- Empty states: All use `Empty.PRESENTED_IMAGE_SIMPLE` icon for consistency
- Empty message centering: Proper flex centering in ModListPanel

**Benefits**: Consistent animations across the app, better theme integration, reduced bundle size (removed Ant Design Dropdown animations), improved user experience with smooth positioning

**Documentation**: [Menu Components README](../D3dxSkinManager.Client/src/shared/components/menu/README.md)

### Changed - 2026-02-20 - ModHierarchicalView Panel Architecture ⭐⭐

Organized ModHierarchicalView into 3 independent panel folders for better code organization and maintainability.

**Changes**:
- Frontend: Created [ClassificationPanel/](../D3dxSkinManager.Client/src/modules/mods/components/ClassificationPanel/) folder with all classification components
  - ClassificationPanel.tsx, ClassificationTree.tsx, ClassificationTreeContext.tsx
  - useClassificationTreeOperations.tsx, ClassificationContextMenu.tsx
  - UnclassifiedItem.tsx, ClassificationScreen.tsx, TreeNodeConverter.tsx
- Frontend: Created [ModListPanel/](../D3dxSkinManager.Client/src/modules/mods/components/ModListPanel/) folder with mod list components
  - ModListPanel.tsx (panel with search bar and empty states)
  - ModList.tsx (list/card view with actions)
- Frontend: Created [ModPreviewPanel/](../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/) folder with preview components
  - ModPreviewPanel.tsx, ModPreviewContext.tsx
  - FullScreenPreview.tsx, FullScreenPreview.css
- Updated all import paths across panel components
- Updated [ModHierarchicalView.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModHierarchicalView.tsx) to import from new panel folders

**FullScreenPreview Styling**:
- Dark overlay (rgba(0,0,0,0.92)) for both light and dark themes
- Flat design: Removed shadows, sharp edges
- Theme-aware styling with CSS variables
- Obvious button hover effects in light theme (blue accent)
- Reduced button shadows for cleaner look

**Benefits**: Better code organization, clearer domain boundaries, easier navigation, independent panel folders, scalable architecture

### Changed - 2026-02-20 - ModView Architecture with Context ⭐⭐

Refactored mod preview system with dedicated context for better state management and automatic preview loading.

**Changes**:
- Frontend: Created [ModView folder](../D3dxSkinManager.Client/src/modules/mods/components/ModView/) with dedicated components
- Frontend: Added [ModViewContext.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModView/ModViewContext.tsx) for managing mod view state
- Frontend: Moved and updated [ModPreviewPanel.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModView/ModPreviewPanel.tsx) to use context
- Frontend: Added Carousel component for multiple preview images
- Frontend: Improved [FullScreenPreview.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModView/FullScreenPreview.tsx) - fixed flashing, better design
- Frontend: Updated [ModHierarchicalView.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModHierarchicalView.tsx) to use ModViewProvider
- Context automatically loads preview paths via IPC when mod is selected
- Preview paths fetched from `GET_PREVIEW_PATHS` IPC endpoint

**FullScreenPreview Improvements**:
- Fixed flashing issue by disabling modal transitions
- Added loading spinner during image load
- Added smooth fade-in effect when image loads
- Improved close button design with hover effect
- Added hint text at bottom ("Click image or press ESC to close")
- Better error handling for failed image loads
- Moved to ModView folder for domain consistency

**Benefits**: Automatic preview loading, better separation of concerns, cleaner state management, support for multiple preview images, smooth UX without flashing

### Fixed - 2026-02-20 - Nullable ThumbnailPath Handling

Fixed preview images not displaying due to NULL ThumbnailPath values causing database read failures.

**Root Cause**: `ModRepository.MapToModInfo()` used `GetString()` on nullable ThumbnailPath field, which fails on NULL values

**Changes**:
- Backend: Updated [ModRepository.cs:334-335](../D3dxSkinManager/Modules/Mods/Services/ModRepository.cs#L334-L335) to use IsDBNull check
- Added proper nullable handling before reading ThumbnailPath from SQLite database

**Benefits**: Preview images now display correctly even when ThumbnailPath is NULL in database

### Fixed - 2026-02-20 - Mod Preview Display Architecture

Fixed mod preview display to work with new dynamic preview file structure.

**Changes**:
- Backend: Added `GET_PREVIEW_PATHS` IPC method to [ModFacade.cs](../D3dxSkinManager/Modules/Mods/ModFacade.cs)
- Backend: Injected `IImageService` into ModFacade for preview path retrieval
- Frontend: Added `getPreviewPaths()` method to [modService.ts](../D3dxSkinManager.Client/src/modules/mods/services/modService.ts)
- Frontend: Updated [ModPreviewPanel.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel.tsx) to use thumbnailPath only
- Frontend: Updated context menu in [ModList.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModList.tsx) and [ModTable.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModTable.tsx)
- Removed deprecated `previewPath` property from ModInfo TypeScript interface
- Updated menu labels to "Open Preview in Explorer"

**Benefits**: Preview images now stored dynamically in `previews/{SHA}/` folders, proper architecture with IPC support, more flexible preview management

### Changed - 2026-02-20 - UI Improvements (Badge & Empty State)

Improved UI consistency with static badge and centered empty state.

**Changes**:
- Frontend: Updated [UnclassifiedItem.css](../D3dxSkinManager.Client/src/modules/mods/components/UnclassifiedItem/UnclassifiedItem.css) - Disabled Badge animations
- Frontend: Updated [ModPreviewPanel.tsx](../D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel.tsx) - Centered empty state vertically
- Added CSS rules to disable Ant Design Badge default animations (animation, transition, transform)
- Empty state now uses flexbox for perfect centering (horizontal + vertical)

**Benefits**: Static badge appearance, properly centered empty state, better visual consistency

### Added - 2026-02-20 - Window State Persistence ⭐⭐

Implemented automatic window size and position persistence with dedicated service architecture.

**Changes**:
- Backend: Added `WindowSettings` nested class to [GlobalSettings.cs](../D3dxSkinManager/Modules/Settings/Models/GlobalSettings.cs)
- Backend: Created [WindowStateService](../D3dxSkinManager/Modules/Settings/Services/WindowStateService.cs) with DI registration
- Backend: Updated [Program.cs](../D3dxSkinManager/Program.cs) to use WindowStateService via dependency injection
- Properties saved: X, Y, Width, Height, Maximized state
- Screen bounds validation prevents window from disappearing when screen resolution changes
- Minimum window size enforced (800x600)
- State saved via WindowClosingHandler before window closes

**Benefits**: Clean separation of concerns, testable service architecture, better user experience, handles multi-monitor setups

**JSON Structure**: Settings stored in nested `window` object in `data/settings/global.json`

### Changed - 2026-02-20 - Image Loading with Custom Scheme Handler ⭐⭐

Replaced ImageServerService HTTP server with Photino's RegisterCustomSchemeHandler for improved image loading.

**Changes**:
- Backend: Created [CustomSchemeHandler](../D3dxSkinManager/Modules/Core/Services/CustomSchemeHandler.cs) service with DI registration
- Backend: Registered in [CoreServiceExtensions](../D3dxSkinManager/Modules/Core/CoreServiceExtensions.cs) for dependency injection
- Backend: Updated [Program.cs](../D3dxSkinManager/Program.cs) to resolve from DI container
- Frontend: Created `toAppUrl()` helper in [imageUrlHelper.ts](../D3dxSkinManager.Client/src/shared/utils/imageUrlHelper.ts)
- Updated components: ModPreviewPanel, ModThumbnail, TreeNodeConverter
- Removed ImageServerService (HTTP server on localhost:5555 no longer needed)

**Benefits**: Native scheme handler (no HTTP server), DI-based architecture, simpler URL format (`app://encoded_path`), better testability

**Impact**: Image loading now uses custom scheme instead of HTTP localhost

### Changed - 2026-02-20 - Keywords Index Routing System ⭐⭐

Implemented routing system for KEYWORDS_INDEX.md to improve lookup performance and scalability.

**Changes**:
- Main index now routing hub (178 lines)
- Created domain-specific files: BACKEND.md, FRONTEND.md, DOCUMENTATION.md, HOW_TO.md
- Total: ~1,900 lines distributed vs ~1,100 lines in single file
- Each domain file < 600 lines for fast loading

**Benefits**: Faster lookups (load only relevant domain), token efficient, scalable with sub-folder support

**Files Created**:
- `docs/keywords/BACKEND.md` (388 lines)
- `docs/keywords/FRONTEND.md` (580 lines)
- `docs/keywords/DOCUMENTATION.md` (258 lines)
- `docs/keywords/HOW_TO.md` (489 lines)

**Updated**:
- [AI_GUIDE.md](AI_GUIDE.md) - Added routing system documentation
- [maintenance/KEYWORDS_INDEX_ROUTING_PROPOSAL.md](maintenance/KEYWORDS_INDEX_ROUTING_PROPOSAL.md) - Updated to implementation status

### Fixed - 2026-02-20 - Migration Archive Storage ⭐⭐

Fixed migration to store archives WITHOUT extensions, matching Python version format. Added smart format detection.

**Impact**: ✅ 173 tests pass, consistent with Python version
**Details**: [changelogs/2026-02/2026-02-20-migration-archive-storage-fix.md](changelogs/2026-02/2026-02-20-migration-archive-storage-fix.md)

---

## February 2026

### Overview
Major refactoring month focusing on code quality, migration service, and architecture improvements. 15+ significant changes.

**Full Details**: [changelogs/2026-02/february-archive.md](changelogs/2026-02/february-archive.md)

### Highlights

#### 2026-02-19 - Code Quality Refactoring ⭐⭐⭐
- Removed 40+ `any` type usages, improved type safety
- Standardized error handling across 4 critical files
- Converted 35+ components to Compact variants
- **Impact**: ✅ Builds succeed, type-safe IPC communication

#### 2026-02-19 - Work Directory Refactoring ⭐⭐⭐
- Separated GameDirectory from WorkDirectory
- Renamed `work_mods/` → `work/`
- Support for external work directories
- **Impact**: Clear separation of concerns

#### 2026-02-19 - Preview System Refactored ⭐⭐⭐
- Dynamic folder scanning (`previews/{SHA}/`)
- Removed single preview path limitation
- Multiple preview support
- **Impact**: Better UX, cleaner architecture

_Additional February changes archived in [february-archive.md](changelogs/2026-02/february-archive.md)_

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

- **February 2026**: [changelogs/2026-02/february-archive.md](changelogs/2026-02/february-archive.md)
- **Detailed Changes**: See `changelogs/YYYY-MM/` folders
- **Management Guide**: [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md)

---

**Current Line Count**: 382 lines (Target: < 200 lines) ⚠️ NEEDS ARCHIVING
**Last Cleanup**: 2026-02-19
**Next Cleanup**: NOW (overdue)
