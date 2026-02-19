# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> For detailed changes, see `changelogs/YYYY-MM/` folders.
> See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) for guidelines.

---

## [Unreleased]

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

#### 2026-02-19 - Mod Count Display ⭐
- Added mod counts to classification tree: "Category (15)"
- Real-time filtering updates
- **Impact**: Better navigation

#### 2026-02-18 - Centralized Mod Management ⭐⭐
- Created `ModManagementService` for mod operations
- Single source of truth for mod state
- **Impact**: Cleaner architecture, easier maintenance

#### 2026-02-18 - Settings Persistence Fixed ⭐⭐⭐
- Fixed deadlock in `UpdateSettingAsync`
- Settings now save correctly from UI
- **Impact**: Critical bug fix

#### 2026-02-18 - Test Suite Added ⭐⭐⭐
- 118+ unit tests across 8 test files
- Coverage for core, mods, migration, settings
- **Impact**: Confident refactoring, catches regressions

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

**Current Line Count**: ~110 lines (Target: < 200 lines)
**Last Cleanup**: 2026-02-20
**Next Cleanup**: 2026-03-01
