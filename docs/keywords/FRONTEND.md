# Frontend Keywords Index

> **Purpose:** React components, hooks, services, and TypeScript types
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Last Updated:** 2026-02-20

---

## Main Application

- **App** → `src/App.tsx:23`
  - Main application component with theme support
  - AppWithProviders → Root component with ThemeProvider
  - App → ConfigProvider with Ant Design theme algorithms
  - AppContent → Main content with hooks
  - useModData, useModFilters, useModActions hooks
  - Updated: 2026-02-18 - Added theme system

- **AppInitializer** → `src/components/layout/AppInitializer.tsx`
  - Application startup and initialization logic

---

## Context Providers

- **ThemeContext** → `src/shared/context/ThemeContext.tsx`
  - ThemeProvider (context provider) → `:42`
  - useTheme (hook) → `:84`
  - Theme modes: light, dark, auto
  - System theme detection and auto-switching
  - localStorage persistence
  - data-theme attribute management
  - Created: 2026-02-18

- **ProfileContext** → `src/shared/context/ProfileContext.tsx`
  - ProfileProvider (context provider) → `:48`
  - useProfile (hook) → `:94`
  - Profile state management and IPC integration
  - Created: 2026-02-18

---

## Layout Components

- **AppHeader** → `src/components/layout/AppHeader.tsx:8`
  - Application header with branding

- **AppSider** → `src/components/layout/AppSider.tsx:14`
  - Navigation sidebar with menu (5 tabs)
  - Props: selectedTab, onTabChange
  - Updated: 2026-02-17 - Added Tools and Plugins tabs

- **AppStatusBar** → `src/components/layout/AppStatusBar.tsx:26`
  - Status bar with progress, color-coded messages, Help/Suggestions links
  - Props: userName, serverStatus, modsLoaded, modsTotal, statusMessage, statusType, progressPercent, progressVisible, onHelpClick, onSuggestionsClick
  - Updated: 2026-02-17 Phase 3 - Added progress bar, color coding, action buttons
  - Updated: 2026-02-18 - Theme-aware colors

---

## Common Components

- **GradingTag** → `src/components/common/GradingTag.tsx:7`
  - Color-coded grading badge component
  - Props: grading

- **StatusIcon** → `src/components/common/StatusIcon.tsx:7`
  - Load status indicator (loaded/unloaded)
  - Props: isLoaded

- **ModThumbnail** → `src/components/common/ModThumbnail.tsx:8`
  - Thumbnail image with fallback icon
  - Props: thumbnailPath, alt

- **Menu Components** → `src/shared/components/menu/`
  - **ContextMenu** → `ContextMenu.tsx:42` - Low-level menu with manual positioning
    - Props: items, visible, position, onClose
    - Smart positioning with viewport edge detection
    - Smooth vertical animations (top-down/bottom-up)
    - Theme-aware styling, scroll/click-outside close
  - **PopupMenu** → `PopupMenu.tsx:36` - Simple right-click menu wrapper
    - Props: items, children, onClose
    - Automatically manages position from mouse events
    - Best for static menu items
  - **usePopupMenu** → `usePopupMenu.ts:66` - Hook for complex menu scenarios
    - Returns: { visible, position, show, hide, getTriggerProps }
    - Best for tracking context (which item was clicked)
    - Used with ContextMenu for dynamic menus
  - Documentation: [README.md](../D3dxSkinManager.Client/src/shared/components/menu/README.md)
  - Created: 2026-02-20 - Replaced Ant Design Dropdowns

- **DragDropZone** → `src/components/common/DragDropZone.tsx:18`
  - Drag & drop zone for files and folders
  - Props: onFilesDrop, onFolderDrop, accept, children, disabled, showOverlay
  - File type filtering, visual feedback overlay
  - Automatic categorization (images vs archives)
  - Created: 2026-02-17

- **TooltipSystem** → `src/components/common/TooltipSystem.tsx`
  - Annotation system with level-based tooltip display
  - AnnotationProvider (context provider) → `:45`
  - useAnnotation (hook) → `:63`
  - AnnotatedTooltip (component) → `:84`
  - annotations (content library) → `:130-305`
  - Annotation levels: all, more, less, off
  - Tooltip levels: 1 (basic), 2 (detailed), 3 (expert)
  - localStorage persistence
  - Created: 2026-02-17 Phase 7

- **MultiTagInput** → `src/shared/components/common/MultiTagInput.tsx`
  - Multi-tag input with autocomplete dropdown
  - Props: value, onChange, availableTags, onOpenTagSelector, placeholder, maxTagTextLength
  - Features: Type to add tags, autocomplete suggestions, create new tags on save
  - Comma separator support, max tag length validation (default 50 chars)
  - Button to open tag selector dialog
  - Responsive tag display with maxTagCount
  - Created: 2026-02-18

### Compact Component Library

> **Location:** `src/shared/components/compact/`
> **Purpose:** Standardized components for consistent sizing and styling
> **Import:** `import { CompactButton, CompactCard } from 'shared/components/compact'`

- **CompactButton** → `compact/CompactButton.tsx`
  - Standardized button component for consistent sizing
  - Flat design in dark theme (no shadows, uses border-color and brightness changes)
  - Variants: CompactPrimaryButton, CompactTextButton, CompactLinkButton, CompactDangerButton
  - Props: All standard Button props + size variants

- **CompactSpace** → `compact/CompactSpace.tsx`
  - Consistent spacing wrapper component

- **CompactCard** → `compact/CompactCard.tsx`
  - Standardized card component

- **CompactDivider** → `compact/CompactDivider.tsx`
  - Consistent divider styling

- **CompactText** → `compact/CompactText.tsx`
  - Includes: CompactTitle, CompactParagraph, CompactText
  - Standardized typography components

- **CompactAlert** → `compact/CompactAlert.tsx`
  - Consistent alert component styling

- **CompactSection** → `compact/CompactSection.tsx`
  - Section wrapper with consistent padding/spacing

**Reorganization:** 2026-02-18 - Moved from `common/` to `compact/` folder + added index.ts

### Slide-In Screens

- **SlideInScreen** → `src/shared/components/common/SlideInScreen.tsx`
  - Application-style slide-in panel component with blur backdrop
  - **IMPORTANT - Animation Architecture:**
    - The ENTIRE CONTAINER (including backdrop and panel) slides in/out together as one unit
    - Animation is applied to `.slide-in-screen-container` element
    - Slide-in: Applied automatically via CSS on mount
    - Slide-out: Applied when `.closing` class is added to container
    - Both backdrop and panel are children of container and move together
  - SlideInScreen (component) → `:21`
    - Props: id, title, children, width, level, onClose
    - State: isClosing (triggers slide-out animation)
    - handleClose → `:32` - Sets isClosing, waits 200ms for animation, then calls onClose
  - SlideInScreenManager (renders all active screens) → `:99`
  - CSS animations:
    - `.slide-in-screen-container` - slideInFromRight animation on mount
    - `.slide-in-screen-container.closing` - slideOutToRight animation on close
    - Blur backdrop and panel slide together, NOT separately
  - Features: Multi-level stacking, ESC key support, blur backdrop indicator
  - Updated: 2026-02-18 - Added slide-out animation with proper container-level animation

---

## Module Components

### Mods Module

- **ModsView** → `src/modules/mods/components/ModsView.tsx`
  - Main mods management view

- **ModHierarchicalView** → `src/modules/mods/components/ModHierarchicalView.tsx`
  - Three-panel hierarchical layout (Classification → Mod List → Preview)
  - Main orchestrator component with state management and business logic
  - Integrates: ClassificationPanel, ModListPanel, ModPreviewPanel
  - Features: Classification tree, unclassified mods, mod search/filter, drag-drop import
  - Context menus, batch operations, mod editing
  - Updated: 2026-02-20 - Organized into panel-based architecture

#### Panel Components (3-Panel Architecture)

> **Architecture:** ModHierarchicalView uses 3 independent panel folders for better organization
> **Location:** `src/modules/mods/components/[PanelName]/`

- **ClassificationPanel** → `src/modules/mods/components/ClassificationPanel/`
  - Left panel for classification tree and unclassified mods
  - **ClassificationPanel.tsx** - Main panel component
  - **ClassificationTree.tsx** - Hierarchical tree component
  - **ClassificationTreeContext.tsx** - Tree operations context
  - **useClassificationTreeOperations.tsx** - Tree manipulation hook
  - **useModCategoryUpdate.ts** - Custom hook for mod category updates via drag-and-drop (2026-02-20)
  - **ClassificationContextMenu.tsx** - Right-click context menu
  - **UnclassifiedItem.tsx** - Unclassified mods indicator (drag-and-drop support added 2026-02-20)
  - **ClassificationScreen.tsx** - Add/edit classification slide-in screen
  - **TreeNodeConverter.tsx** - Converts ClassificationNode to Ant Design DataNode
  - Features: Hierarchical classification, search with count indicators, context menu operations, drag-and-drop category updates
  - Refactored: 2026-02-20 - Extracted into panel folder, added drag-and-drop support

- **ModListPanel** → `src/modules/mods/components/ModListPanel/`
  - Center panel for mod list and search
  - **ModListPanel.tsx** - Main panel with search bar and empty states
  - **ModList.tsx** - List/card view of mods with actions
  - Features: Search bar, empty state handling, mod selection
  - Displays filtered mods based on classification/object selection
  - Refactored: 2026-02-20 - Extracted into panel folder

- **ModPreviewPanel** → `src/modules/mods/components/ModPreviewPanel/`
  - Right panel for selected mod preview
  - **ModPreviewPanel.tsx** - Main panel with mod details
  - **ModPreviewContext.tsx** - Preview state management
  - **FullScreenPreview.tsx** - Full-screen image viewer
  - **FullScreenPreview.css** - Theme-aware fullscreen preview styling
  - Features: Large preview image, metadata display, fullscreen view
  - Dark overlay (rgba(0,0,0,0.92)), flat design, theme-aware
  - Refactored: 2026-02-20 - Extracted into panel folder, improved fullscreen styling

#### Supporting Components

- **ModActionButtons** → `src/modules/mods/components/ModActionButtons.tsx`
  - Load/Unload/Delete action buttons
  - Props: mod, onLoad, onUnload, onDelete
  - Used by ModList component

### Settings Module

- **SettingsView** → `src/modules/settings/components/SettingsView.tsx`
  - Settings tab with theme, logLevel, annotationLevel, thumbnailAlgorithm
  - **FIXED 2026-02-18:** Now properly saves all settings to backend
  - handleLogLevelChange → `:63-76` (calls settingsService.updateGlobalSetting)
  - handleAnnotationLevelChange → `:49-61` (calls settingsService.updateGlobalSetting)
  - handleThemeChange → `:78` (calls ThemeContext.setTheme)
  - handleThumbnailAlgorithmChange → `:67-77`

### Launch Module

- **GameLaunchTab** → `src/modules/launch/components/GameLaunchTab.tsx`
  - Game launch UI with configuration

### Tools Components

- **ToolsView** → `src/components/tools/ToolsView.tsx:19`
  - Tools tab with cache management, tag management, and utilities
  - Features: Clear caches, cache browser, tag editor, mod order management

### Plugins Components

- **PluginsView** → `src/components/plugins/PluginsView.tsx:20`
  - Plugins tab displaying all 26 plugins with enable/disable controls
  - Features: Plugin table, details modal, status indicators

---

## Dialog Components

### Mod Dialogs

- **ModEditDialog** → `src/modules/mods/components/ModEditDialog/`
  - Single mod editing dialog with modular section components
  - Main file: `index.tsx` (orchestrator)
  - **BasicInfoSection** → `BasicInfoSection.tsx` - Name and description fields
  - **MetadataSection** → `MetadataSection.tsx` - Author, category, age rating fields
  - **TagsSection** → `TagsSection.tsx` - Tags input with MultiTagInput and ModTagSelectorDialog
  - **ModTagSelectorDialog** → `ModTagSelectorDialog.tsx` - Tag selector specifically for mod editing
  - Props: visible, mod, onSave, onCancel
  - Form fields: Name, Description, Age Rating (G/P/R/X), Author, Category, Tags
  - Uses MultiTagInput for tag editing with autocomplete
  - Read-only SHA hash display
  - Age Rating System: G (General), P (Parental Guidance), R (Restricted), X (Adults Only)
  - Moved to mods module: 2026-02-18
  - Refactored into smaller components: 2026-02-18

- **ModTagSelectorDialog** → `src/modules/mods/components/ModEditDialog/ModTagSelectorDialog.tsx`
  - Tag selector dialog for mod editing workflow
  - Used by ModEditDialog's TagsSection and BatchEditDialog
  - Props: visible, availableTags, selectedTags, onConfirm, onCancel
  - Features: Search/filter, checkbox selection, Select All/Deselect All, selected count
  - Uses slide-in dialog pattern
  - Created: 2026-02-18
  - Renamed from TagSelectorDialog and moved to ModEditDialog folder: 2026-02-18

- **BatchEditDialog** → `src/modules/mods/components/BatchEditDialog/`
  - Batch editing dialog for multiple mods
  - Main file: `index.tsx`
  - **FieldRow** → `FieldRow.tsx` - Reusable checkbox + field row component
  - Props: visible, selectedMods, onSave, onCancel
  - Checkbox-based field selection (only update checked fields)
  - Field mask array for partial updates
  - Features: AutoComplete for author/category, MultiTagInput for tags, Age Rating (G/P/R/X)
  - Uses CompactButton components
  - Moved to mods module: 2026-02-18
  - Refactored: 2026-02-18

### Import Dialogs

- **ImportTagSelectorDialog** → `src/modules/mods/components/import/ImportTagSelectorDialog.tsx`
  - Tag selector dialog for import workflow
  - Used by import workflow (AddModUnit, BatchEditUnit)
  - Props: visible, availableTags, selectedTags, onConfirm, onCancel
  - Features: Search/filter, checkbox selection, Select All/Deselect All, selected count
  - Uses slide-in dialog pattern
  - Title: "Select Tags for Import"
  - Created: 2026-02-18
  - Separate from ModTagSelectorDialog for clear workflow separation

- **TagSelectDialog** → `src/modules/core/components/dialogs/TagSelectDialog.tsx:17`
  - Legacy multi-select tag dialog (still available but not actively used)
  - Props: visible, selectedTags, availableTags, onSave, onCancel
  - Features: 13 common predefined tags, custom tag input, Select All/Clear All
  - Note: Replaced by ImportTagSelectorDialog for import workflow
  - Created: 2026-02-17 Phase 5

### Configuration Dialogs

- **UnityArgsDialog** → `src/components/dialogs/UnityArgsDialog.tsx:17`
  - Unity game launch arguments configuration dialog
  - Props: visible, currentArgs, onSave, onCancel
  - Borderless window toggle, popup window mode, fullscreen mode
  - Screen dimensions (width×height) with spinboxes
  - Common resolutions helper panel
  - Parses existing args string and builds new args string
  - Created: 2026-02-17 Phase 8

### Preview Dialogs

- **FullScreenPreview** → `src/components/dialogs/FullScreenPreview.tsx:10`
  - Full-screen image preview modal
  - Props: visible, imageSrc, imageAlt, onClose
  - Black background (95% opacity) for optimal viewing
  - Image scales to 95vw×95vh maintaining aspect ratio
  - Click anywhere or ESC to close
  - Created: 2026-02-17 Phase 15.5

### Help & Info Dialogs

- **KeyboardShortcutsDialog** → `src/components/dialogs/KeyboardShortcutsDialog.tsx:14`
  - Modal dialog displaying all keyboard shortcuts
  - Grouped by context (Global, Mod Management, Import Window, Dialogs)
  - Table format with shortcut tags and descriptions
  - Collapsible sections with dividers
  - Created: 2026-02-17 Phase 13

- **AboutDialog** → `src/components/dialogs/AboutDialog.tsx:14`
  - App version and build information dialog
  - Technology stack display with Tags
  - Key features list, credits section
  - Resource links (GitHub, Docs, Issues)
  - MIT License footer
  - Created: 2026-02-17 Phase 14

---

## Window Components

- **AddModWindow** → `src/components/windows/AddModWindow.tsx:37`
  - Import/Add Mod window with task queue table
  - Props: visible, tasks, onConfirm, onCancel, onEditTask, onRemoveTask, onBatchEdit, processing
  - Features: Task queue table, row selection, statistics footer, bulk operations
  - Task statuses: pending, processing, success, error, skipped
  - Created: 2026-02-17 Phase 6

- **AddModUnit** → `src/components/windows/AddModUnit.tsx:17`
  - Single import task editing dialog
  - Props: visible, task, onSave, onCancel, onOpenTagSelector
  - Form fields: Name, Object, Description, Author, Grading, Tags
  - File info card with source path and type
  - Preview thumbnail display
  - Created: 2026-02-17 Phase 6

- **BatchEditUnit** → `src/components/windows/BatchEditUnit.tsx:16`
  - Batch editing for multiple import tasks
  - Props: visible, selectedTasks, onSave, onCancel, onOpenTagSelector
  - Checkbox-based field selection
  - Field mask array for partial updates
  - Alert showing fields count and tasks count
  - Created: 2026-02-17 Phase 6

- **HelpWindow** → `src/components/windows/HelpWindow.tsx:20`
  - Comprehensive help documentation window
  - 4-tab interface: Quick Start, Features, Troubleshooting, Tips & Tricks
  - Collapsible panels for each feature
  - Alert components for visual emphasis
  - Common issues and solutions
  - Best practices and workflow tips
  - Created: 2026-02-17 Phase 14

- **WarehouseView** → `src/components/warehouse/WarehouseView.tsx:23`
  - Mod warehouse browsing and download component
  - Two-panel layout: Mod list table (left) + Preview panel (right)
  - Download progress tracking with status indicators
  - Status indicators: 已下载 (Downloaded), 正在下载... (Downloading)
  - Real-time search and filtering by name/category/author
  - Filter by Object/Category (Character, Weapon, UI)
  - Open in browser button for external mod links
  - Created: 2026-02-17 Phase 10

---

## Custom Hooks

- **useModData** → `src/hooks/useModData.ts:8`
  - Data fetching and loading
  - Returns: { mods, loading, objects, authors, loadMods, loadFilters }
  - loadMods → `:16`
  - loadFilters → `:28`

- **useModFilters** → `src/hooks/useModFilters.ts:8`
  - Filter state and logic
  - Returns: { filters, filteredMods, loading, updateFilter, clearFilters, handleSearch, hasActiveFilters }
  - filteredMods (computed) → `:14-36`
  - handleSearch → `:38-53`
  - updateFilter → `:55-57`
  - clearFilters → `:59-65`
  - hasActiveFilters → `:67-70`

- **useModActions** → `src/hooks/useModActions.ts:6`
  - Mod operations (load, unload, delete)
  - Returns: { handleLoadMod, handleUnloadMod, handleDeleteMod }
  - handleLoadMod → `:7-15`
  - handleUnloadMod → `:17-25`
  - handleDeleteMod → `:27-45`

- **useTheme** → `src/shared/context/ThemeContext.tsx:84`
  - Theme management hook
  - Returns: { theme, effectiveTheme, setTheme }
  - theme → Current theme mode (light/dark/auto)
  - effectiveTheme → Resolved theme (light/dark)
  - setTheme → Update theme preference
  - Created: 2026-02-18

- **useProfile** → `src/shared/context/ProfileContext.tsx:94`
  - Profile management hook
  - Returns: Profile state and operations
  - Created: 2026-02-18

---

## Services

### IPC Communication

- **photinoService** → `src/services/photino.ts:60`
  - sendMessage → `:91-128`
  - initializeMessageReceiver → `:68-86`
  - simulateBackendResponse → `:133-162` (dev mode)
  - getMockMods → `:164-191` (dev mode)
  - activeProfileId → Profile integration for all IPC messages
  - Updated: 2026-02-18 - Added profile context integration

### API Services

- **modService** → `src/services/modService.ts:10`
  - getAllMods → `:25`
  - loadMod → `:32`
  - unloadMod → `:39`
  - getLoadedMods → `:46`
  - importMod → `:53`
  - deleteMod → `:60`
  - getModsByObject → `:67`
  - getObjectNames → `:74`
  - getAuthors → `:81`
  - getTags → `:88`
  - searchMods → `:95`
  - getModBySha → `:102`
  - updateMetadata → `:131-146`
  - updateCategory → `:151-160` (NEW: Drag-and-drop category update)
  - batchUpdateMetadata → `:165`

- **settingsService** → `src/modules/settings/services/settingsService.ts`
  - getGlobalSettings() → `:24-30`
  - updateGlobalSetting(key, value) → `:46-52` (sends UPDATE_FIELD IPC message)

### File Dialog Services

- **fileDialogService** → `src/services/fileDialogService.ts`
  - openFileDialog: Select file with filters (title, defaultPath, filters)
  - openFolderDialog: Select directory
  - saveFileDialog: Save file dialog with default name
  - openFile: Open file in default application
  - openFileExplorer: Open file explorer at path (with highlight)
  - exportMod: Export mod to destination path
  - FileDialogOptions interface: { title, defaultPath, filters }
  - FileDialogResult interface: { success, filePath, error }
  - All functions ready for Photino.NET IPC integration
  - Created: 2026-02-17 Phase 11

---

## Utilities

- **imageUrlHelper** → `src/shared/utils/imageUrlHelper.ts`
  - toAppUrl(path) → Converts file paths to `app://` scheme URLs for custom scheme handler
  - toAppUrls(paths) → Batch conversion of paths array
  - Handles data URIs, HTTP URLs, and file paths
  - Created: 2026-02-20

- **grading.utils.ts** → `src/utils/grading.utils.ts`
  - getGradingColor → `:3-11`
  - getGradingLabel → `:13-21`
  - gradingOptions → `:23-28`

- **logger.ts** → `src/utils/logger.ts`
  - Logger class with level-based filtering
  - LogLevel enum → `:6-14` (ALL, TRACE, DEBUG, INFO, WARN, ERROR, FATAL, OFF)
  - LogLevelName type → `:16`
  - setLevel(level) → `:30-37` (also saves to localStorage)
  - getLevel() → `:42-44`
  - getLevelName(level) → `:49-51`
  - getCurrentLevelName() → `:56-58`
  - trace(message, ...args) → `:77-81`
  - debug(message, ...args) → `:86-90`
  - info(message, ...args) → `:95-99`
  - warn(message, ...args) → `:104-108`
  - error(message, ...args) → `:113-117`
  - fatal(message, ...args) → `:122-126`
  - getLevelOptions() → `:131-144` (static method for UI)
  - Singleton instance exported as `logger`
  - Created: 2026-02-17 Phase 16.2

- **KeyboardShortcutManager** → `src/utils/KeyboardShortcutManager.ts`
  - Global keyboard shortcut system with context-aware shortcuts
  - ShortcutConfig interface: key, modifiers, description, callback
  - register/unregister shortcuts, setContext for context-aware behavior
  - handleKeyDown with input field protection
  - formatShortcut for display (e.g., "Ctrl + F")
  - SHORTCUTS constants: FOCUS_SEARCH, SAVE, CANCEL, SUBMIT, etc.
  - Created: 2026-02-17 Phase 13

---

## TypeScript Types

- **mod.types.ts** → `src/types/mod.types.ts`
  - ModInfo interface → `:1-15`
  - GradingLevel type → `:17`
  - ModFilters interface → `:19-23`
  - ModStatistics interface → `:25-29`

- **message.types.ts** → `src/types/message.types.ts`
  - MessageType union type → `:1-12`
  - PhotinoMessage interface → `:14-18`
  - PhotinoResponse interface → `:20-25`

---

## Styles

- **Theme Colors CSS** → `src/styles/theme-colors.css`
  - Centralized color system with CSS custom properties
  - 50+ CSS variables for complete theme control
  - Light and dark theme definitions
  - Component-specific color overrides
  - Automatic Ant Design component styling
  - Created: 2026-02-18

- **Main Styles** → `src/App.css`
  - Global application styles
  - Animation overrides (0.05s linear for performance)

- **Visual Enhancements** → `src/styles/visual-enhancements.css`
  - Comprehensive CSS for hover effects, transitions, animations
  - Button hover with transform and shadow
  - Table row hover, card hover, input focus effects
  - Modal fade-in, dropdown slide-down animations
  - Status color coding: success/error/warning/processing/normal
  - Count indicator styling with rounded corners
  - Thumbnail hover effects with scale transform
  - Custom scrollbar styling
  - Context menu enhancements
  - Created: 2026-02-17 Phase 12

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| react | 19.2.4 | UI library |
| typescript | 4.9.5 | Type safety |
| antd | 6.3.0 | UI components |
| axios | 1.13.5 | HTTP client |

---

## Naming Conventions

- **PascalCase** for React components: `App.tsx`, `ModTable.tsx`, `GradingTag.tsx`
- **camelCase** for hooks: `useModData.ts`, `useModFilters.ts`, `useModActions.ts`
- **camelCase** for services: `modService.ts`, `photino.ts`
- **lowercase.type** for types: `mod.types.ts`, `message.types.ts`
- **camelCase** for utils: `grading.utils.ts`
- **Folders:** `components/`, `hooks/`, `types/`, `utils/`, `services/`

---

**Line Count:** ~550 lines
**Parent:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Note:** If this file exceeds 500 lines, consider splitting into:
- `FRONTEND_COMPONENTS.md` (components only)
- `FRONTEND_HOOKS_SERVICES.md` (hooks, services, utils)
