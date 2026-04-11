# Frontend Keywords Index

> **Purpose:** React components, hooks, services, and TypeScript types
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Last Updated:** 2026-02-21

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

- **OperationContext** → **REMOVED** (Replaced by Workflow/EventBus system)
  - Removed: 2026-02-25
  - Replacement: Use Workflow system for progress tracking
  - See: [WORKFLOW_ARCHITECTURE.md](../architecture/WORKFLOW_ARCHITECTURE.md)

---

## State Management (Zustand Stores)

### ModProvider

**Location:** `src/modules/mod/ModProvider.tsx`

**Purpose:** Top-level provider for mod module state management and event subscriptions

**Architecture Changes (2026-03-07):**
- **Event consolidation**: Reduced from 8+ specific event subscriptions to 2 debounced handlers
- **Simplified event flow**: Backend consolidates events → Frontend reacts to consolidated events
- **Work path detection**: Automatically refreshes mods when profile work directory changes

**Event Subscriptions:**
```typescript
// 1. MOD_LIST_UPDATED (debounced 20ms)
//    Consolidates: LOADED, UNLOADED, DELETED, IMPORTED, METADATA_UPDATED, CATEGORY_UPDATED, CACHE_CHANGED, REFRESHED
//    Triggers: refreshMods() + loadStatistics()

// 2. CATEGORY_TREE_UPDATED (debounced 20ms)
//    Consolidates: MOD.CATEGORY_UPDATED, MOD.IMPORTED, MOD.DELETED
//    Triggers: refreshCategoryTree()
```

**Key Features:**
- Debounced event handlers prevent event storms (20ms debounce)
- Statistics loading integrated into MOD_LIST_UPDATED handler
- Locked category persistence (loads/saves from profile config)
- Work path change detection for automatic refresh

**Removed (2026-03-06):**
- ~~Individual event subscriptions (LOADED, UNLOADED, etc.)~~
- ~~Separate statistics event subscription~~
- ~~Optimistic update handlers~~

### modsStore

**Location:** `src/modules/mod/store/modsStore.ts`

**Architecture:** Simplified flat store structure (removed slices pattern)

**State Structure (Updated 2026-03-07):**
```typescript
interface ModsState {
  // Data
  mods: ModInfo[] | undefined;              // Current filtered mods (renamed from CategoryFilteredMods)
  selectedMod: ModInfo | undefined;         // Currently selected mod
  selectedCategory: CategoryInfo | undefined; // Selected category (preserved during reset)

  // Loading States
  modLoading: boolean;                      // Mod operation loading (NEW: specific for mod ops)
  categoryLoading: boolean;                 // Category operation loading

  // UI State
  expandedKeys: React.Key[];                // Expanded tree nodes (preserved during reset)
  lockedCategories: string[];               // Locked expanded categories (NEW: persisted)

  // Preview State
  previewPaths: string[];                   // Preview image paths (NEW)
  previewCacheTimestamp: number;            // Cache buster for browser (NEW)

  // Workflow State
  importWorkflowScreenVisible: boolean;     // Import workflow screen visibility (renamed)
}
```

**Removed Fields (2026-03-06):**
- ~~`state.mods`~~ (full mod list) → Removed, backend handles filtering
- ~~`modsLoading`~~ → Renamed to `modLoading`
- ~~`error`~~ → Error handling moved to operations
- ~~`CategoryFilteredMods`~~ → Renamed to `mods`
- ~~`selectedObject`~~ → Never used
- ~~`modManagementMode`~~ → Only written, never read
- ~~`importTasks`~~ → Moved to workflow module
- ~~`importProcessing`~~ → Moved to workflow module
- ~~`taskIdCounter`~~ → Moved to workflow module
- ~~`selectedTaskIds`~~ → Moved to workflow module

**Removed Actions (2026-03-06):**
- ~~`setMods()`~~ → Replaced by backend event refresh
- ~~`setError()`~~ → Error handling moved to operations
- ~~`addMod()`~~ → Not needed with backend refresh
- ~~`updateTreeNodeLocal()`~~ → Category updates via backend
- ~~`toggleExpandedKey()`~~ → UI state simplified
- ~~`updateModsLocal()`~~ → Only used by removed batch operations
- ~~`optimisticLoadUpdate()`~~ → Optimistic updates eliminated
- ~~`optimisticUnloadUpdate()`~~ → Optimistic updates eliminated
- ~~`optimisticCategoryUpdate()`~~ → Optimistic updates eliminated

**Renamed Actions:**
- `openModManagementScreen()` → `openImportWorkflowScreen()`
- `closeModManagementScreen()` → `closeImportWorkflowScreen()`

**New Actions (2026-03-06):**
- `setLockedCategories(keys: string[])` - Load locked categories from config
- `addLockedCategory(key: string)` - Lock category
- `removeLockedCategory(key: string)` - Unlock category
- `bustPreviewCache()` - Increment timestamp to force preview reload

**Store Reset Behavior:**
```typescript
// Preserves state during profile changes:
reset: () => set((state) => {
  const preserved = {
    selectedCategory: state.selectedCategory,
    expandedKeys: state.expandedKeys,
    lockedCategories: state.lockedCategories
  };
  Object.assign(state, initialState);
  Object.assign(state, preserved);
})
```

### Deleted Slices (2026-03-06)

The following slice files were removed and merged into flat modsStore:

- ~~**modsSlice.ts**~~ (121 lines) - Merged into modsStore
- ~~**categorySlice.ts**~~ (81 lines) - Merged into modsStore
- ~~**uiSlice.ts**~~ (106 lines) - Merged into modsStore
- ~~**importSlice.ts**~~ - Moved to workflow module

### Deleted Selectors (2026-03-06)

Selector files removed - use store hooks directly:

- ~~**modSelectors.ts**~~ (96 lines) - Use `useModsStore((state) => state.mods)` directly
- ~~**categorySelectors.ts**~~ (127 lines) - Use store hooks directly

### Operations (mod/operations/)

**Location:** `src/modules/mod/operations/`

**modOperations.ts** - Mod CRUD operations
- `refreshMods(profileId)` - Refresh mod list (event-driven, no optimistic updates)
- `loadStatistics(profileId)` - Load mod statistics
- ~~`batchUpdateMetadata()`~~ - Removed (workflow handles batch operations)
- ~~`exportMods()`~~ - Removed (feature unused)
- ~~`resetModsState()`~~ - Removed (replaced by store.reset())
- ~~`unloadAllMods()`~~ - Removed (not implemented on backend)
- ~~`loadMultipleMods()`~~ - Removed (not implemented on backend)

**categoryOperations.ts** - Category operations
- `refreshCategoryTree(profileId)` - Refresh category tree (event-driven)
- i18n: All operations now use translation keys (5 keys added)

**Import System Removed (2026-03-06):**
- ~~`importTask.types.ts`~~ (53 lines) - Moved to workflow module
- ~~`importSlice.ts`~~ (96 lines) - Moved to workflow module
- ~~`importOperations.ts`~~ (165 lines) - Moved to workflow module

---

## Internationalization (i18n)

- **i18n Configuration** → `src/i18n/i18n.ts`
  - react-i18next setup with custom backend
  - Loads translations from C# LanguageService via IPC
  - Default language handling and fallbacks
  - Created: 2026-02-21

- **I18nInitializer** → `src/i18n/I18nInitializer.tsx`
  - Initializes i18next on app startup
  - Loads language from backend settings
  - Wraps app with I18nextProvider
  - Created: 2026-02-21

- **Language Types** → `src/shared/types/language.types.ts`
  - LanguageSettings interface
  - TranslationDictionary type
  - AVAILABLE_LANGUAGES constant
  - Created: 2026-02-21

- **Language Service** → `src/shared/services/languageService.ts`
  - getLanguage(code) - Load language from backend
  - getAvailableLanguages() - List available languages
  - languageExists(code) - Check language file exists
  - IPC integration with SETTING module
  - Created: 2026-02-21

- **Translation Files** → Backend: `D3dxSkinManager/Languages/`
  - en.json - English translations (507 keys)
  - cn.json - Chinese translations (507 keys)
  - Flat JSON structure for easy searching
  - Auto-copied to data/languages/ on build
  - Created: 2026-02-21

- **useTranslation Hook** → From react-i18next
  - Usage: `const { t, i18n } = useTranslation();`
  - t('namespace.key') - Get translation
  - t('namespace.key', { param }) - With interpolation
  - i18n.language - Current language code
  - i18n.changeLanguage() - Switch language

---

## Layout Components

- **AppHeader** → `src/components/layout/AppHeader.tsx:8`
  - Application header with branding

- **AppSider** → `src/components/layout/AppSider.tsx:14`
  - Navigation sidebar with menu (5 tabs)
  - Props: selectedTab, onTabChange
  - Updated: 2026-02-17 - Added Tools and Plugins tabs

- **AppStatusBar** → `src/components/layout/AppStatusBar.tsx:26`
  - Status bar with progress, color-coded messages, Help links
  - Props: userName, serverStatus, modsLoaded, modsTotal, statusMessage, statusType, progressPercent, progressVisible, onHelpClick
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

- **OperationMonitorScreen** → **REMOVED** (Replaced by Workflow system)
  - Removed: 2026-02-25
  - Replacement: Use WorkflowQueueTable/ModImportWorkflowScreen
  - See: [WORKFLOW_ARCHITECTURE.md](../architecture/WORKFLOW_ARCHITECTURE.md)

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

- **CategoryPanel** → `src/modules/mod/components/CategoryPanel/`
  - Left panel for category tree and unclassified mods
  - **CategoryPanel.tsx** - Main panel component
  - **CategoryTree.tsx** - Hierarchical tree component with **lock expanded feature**
    - **Lock Expanded**: Lock icon (🔒) on locked categories, unlock icon (🔓) on hover
    - Locked categories cannot be collapsed by clicking
    - Locked state persisted to profile config (survives app restarts)
    - Auto-validates on tree updates (removes invalid locks)
    - **Multi-Select Support**: Accepts bulk mod drops (application/mod-ids MIME type)
    - Added: 2026-03-05 (multi-select), 2026-03-07 (lock expanded)
  - **CategoryTreeContext.tsx** - Tree operations context
  - **useCategoryTreeOperations.tsx** - Tree manipulation hook
    - shouldRefreshModsForNodeUpdate() - Smart refresh logic (2026-02-23)
    - Bulk category update handler for multi-select (2026-03-05)
  - **useModCategoryUpdate.ts** - Custom hook for mod category updates via drag-and-drop
    - Single mod drag-drop (2026-02-20)
    - Bulk mod drag-drop for multi-selection (2026-03-05)
    - Auto-unloads loaded mods before category change
  - **CategoryContextMenu.tsx** - Right-click context menu
  - **UnclassifiedItem.tsx** - Unclassified mods indicator (drag-and-drop support added 2026-02-20)
  - **CategoryScreen.tsx** - Add/edit category slide-in screen
    - Thumbnail file picker with preview
    - IPC-based async validation for duplicate names
    - Ant Design form validation with Promise.reject/resolve pattern
    - Integration with useProfile for profileId access
    - Updated: 2026-02-21 (thumbnail support, validation)
  - **TreeNodeConverter.tsx** - Converts CategoryNode to Ant Design DataNode
    - Renders lock icons for locked categories (2026-03-07)
  - Features: Hierarchical categories, search with count indicators, context menu operations, drag-and-drop category updates, **lock expanded state**, **bulk category updates**
  - Refactored: 2026-02-20 - Extracted into panel folder, added drag-and-drop support

- **ModListPanel** → `src/modules/mod/components/ModListPanel/`
  - Center panel for mod list and search with **multi-select support**
  - **ModListPanel.tsx** - Main panel with search bar, empty states, and multi-select state management
    - Local multi-selection state with keyboard modifiers
    - **Ctrl+Click**: Toggle individual mod selection
    - **Shift+Click**: Select range from anchor to current
    - Multi-selection clears when category/object changes
    - Added: 2026-03-05 (multi-select)
  - **ModList.tsx** - List/card view of mods with actions
    - Enhanced drag/drop supporting single and bulk operations
    - Drag multi-selection: All selected mods move to dropped category
    - Multi-selected items use same CSS style as primary selection
    - Added: 2026-03-05 (multi-select drag-drop)
  - **ModListStatusBar.tsx** - Status bar showing selection count
    - Shows "{{count}} Mods selected" when multiple mods selected
    - Shows "No active mod" when no selection
    - i18n support (EN/CN)
    - Added: 2026-03-05
  - Features: Search bar, empty state handling, mod selection, **multi-select with Ctrl/Shift**, **bulk drag-drop**, **selection status bar**
  - Displays filtered mods based on category/object selection
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

### Workflow Module

> **Architecture:** See [WORKFLOW_ARCHITECTURE.md](../architecture/WORKFLOW_ARCHITECTURE.md)

- **ModImportWorkflowScreen** → `src/modules/workflow/components/modImport/ModImportWorkflowScreen.tsx`
  - Download manager style dashboard for importing mods
  - Features:
    - Status dashboard with overall statistics (Active, Completed, Failed)
    - Table view of all active imports with real-time progress
    - Batch action support (Confirm, Delete, Pause/Resume)
    - Auto-imports after compression (no confirmation needed)
    - Support for multiple concurrent imports
    - **Drop zone on tbody area** for continuous file imports while processing
  - Drop Zone Integration (Added: 2026-03-04):
    - Uses `useDropZone` hook targeting `.ant-table-tbody` element
    - Enabled when profile is selected
    - Visual feedback: Dashed border message box with "Drop to import" text
    - Styling matches ModListPanel drop zone pattern
    - Automatically uses selected category from mod store
    - MutationObserver to detect and attach to tbody after table renders
  - Empty State: Uses Ant Design `Empty` component with `PRESENTED_IMAGE_SIMPLE`
  - Automatically refreshes mod list when imports complete
  - Created: 2026-02-25
  - Updated: 2026-03-04 - Added drop zone for continuous imports

- **ModImportWorkflowTable** → `src/modules/workflow/components/modImport/ModImportWorkflowTable.tsx`
  - Table component displaying workflow queue items
  - Features: Expandable rows, progress bars, status tags, action buttons
  - Empty state: "No mods being imported" with simple icon (matching ModListPanel)
  - Updated: 2026-03-04 - Empty state now uses Ant Design Empty component

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
  - Read-only ID hash display
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

- **useDragDrop** → `src/shared/hooks/useDragDrop.ts:162`
  - Generic drag-and-drop hook for custom drag/drop behavior
  - Supports multiple event types with declarative handler configuration
  - Features: Automatic gap detection, visual feedback, DOM data extraction
  - Returns: { containerRef } - Callback ref to attach to container element
  - Handler config: eventType, nodeSelector, allow ('node'|'gap'|'all'), onData, onDrop
  - Fixed: 2026-02-21 - Dragleave flickering, gap detection, React state management

- **useDropZone** → `src/shared/hooks/useDropZone.ts`
  - OS-level file drop hook using WinForms overlay for real file paths
  - Integrates with backend DropZoneOverlay via EventBus IPC
  - **Architecture:** WinForms transparent overlay panel → Backend events → Frontend EventBus → CSS class application
  - **Signature:**
    ```typescript
    useDropZone({
      targetRef: React.RefObject<HTMLElement | null>,
      onDrop: (files: string[]) => void,
      enabled?: boolean,
      zoneId?: string,
      classes?: { hover?: string; drop?: string; }
    })
    ```
  - **Parameters:**
    - `targetRef` - Ref to target element for overlay positioning
    - `onDrop` - Callback with array of absolute file paths
    - `enabled` - Enable/disable drop zone (default: true)
    - `zoneId` - Custom zone identifier (default: auto-generated UUID)
    - `classes` - Custom CSS class names for visual states
      - `hover` - Class applied on mouse enter (default: 'drop-zone-hover')
      - `drop` - Class applied when dragging files (default: 'drop-zone-drop')
  - **Event Flow:**
    1. Mouse/drag enters overlay → Backend sends MOUSE_ENTER/DRAG_ENTER event
    2. EventBus receives event → Hook adds CSS class to targetRef element
    3. Mouse/drag leaves → Backend sends MOUSE_LEAVE/DRAG_LEAVE → Hook removes class
    4. Files dropped → Backend sends FILE_DROP with file paths → onDrop callback
    5. Click overlay → Backend sends CLICK → Hook finds clickable child and clicks it
  - **Visual Feedback:** Self-contained CSS file (useDropZone.css)
    - `.drop-zone-hover` - Mouse over state (matches CompactUpload hover style)
    - `.drop-zone-drop` - Dragging files state (drag-over visual feedback)
    - Theme-aware styles for both light and dark modes
  - **Auto-tracking:** Uses ResizeObserver & IntersectionObserver for position/size updates
  - **Lifecycle:** Auto-registers on mount, auto-unregisters on unmount or when element removed
  - **Click Forwarding:** Finds clickable children (`[role="button"]`, `button`, `a`) and triggers click
  - **Usage Example:**
    ```typescript
    const dropZoneRef = useRef<HTMLDivElement>(null);
    useDropZone({
      targetRef: dropZoneRef,
      onDrop: (files) => handleFileDrop(files),
      classes: { hover: 'custom-hover', drop: 'custom-drop' } // Optional
    });

    return <div ref={dropZoneRef}><CompactUpload /></div>;
    ```
  - Created: 2026-02-24
  - Updated: 2026-02-24 - Added customizable CSS classes with useRef optimization

- **useDelayedLoading** → `src/shared/hooks/useDelayedLoading.ts`
  - Delays loading state changes to prevent flicker
  - Returns: isDelayedLoading boolean
  - Created: 2026-02-20

- **useScrollPosition** → `src/shared/hooks/useScrollPosition.ts`
  - Persists and restores scroll position during content reloads
  - Prevents loss of scroll position when using overlay spinners
  - Returns: { scrollRef, saveScrollPosition, restoreScrollPosition, resetScrollPosition }
  - Usage: ModListPanel, CategoryTree
  - Created: 2026-03-07

- **useOptimisticUpdate** → `src/shared/hooks/useOptimisticUpdate.ts`
  - Optimistic UI updates with automatic verification
  - Handles update → verify → rollback on mismatch workflow
  - Created: 2026-02-20

---

## Services

### IPC Communication

- **bridgeService** → `src/shared/services/bridgeService.ts`
  - WebView2 IPC bridge service
  - Uses chrome.webview.postMessage for IPC communication
  - sendMessage → Sends messages to backend
  - initializeMessageReceiver → Sets up message listener
  - simulateBackendResponse → Dev mode mock responses
  - activeProfileId → Profile integration for all IPC messages

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

- **classificationService** → `src/shared/services/classificationService.ts`
  - getClassificationTree(profileId) - fetches full tree from database
  - findNodeById(tree, id) - local tree search (for UI only)
  - nodeExists(profileId, nodeId) - IPC database validation (for form validation)
  - createNode(profileId, nodeId, name, ...) - creates classification with thumbnail
  - moveNode(profileId, nodeId, newParentId, dropPosition) - moves node
  - updateNode(profileId, nodeId, newName) - updates classification name
  - deleteNode(profileId, nodeId) - deletes classification with thumbnail cleanup
  - getAllLeafNodes(tree) - gets all leaf nodes
  - flattenTree(tree) - flattens tree to list
  - Updated: 2026-02-21 (added nodeExists for IPC validation)

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
  - WebView2 IPC integration for native file operations

---

## Utilities

- **errorHandler** → `src/shared/utils/errorHandler.ts`
  - handleError(error) → Processes errors and shows user-friendly messages based on error code
  - OperationError class → Typed error with errorCode and data
  - getErrorMessage(errorCode) → Get user-friendly message for error code
  - isErrorCode(error, errorCode) → Check if error matches specific code
  - ERROR_MESSAGES mapping for all error codes
  - Created: 2026-02-21

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
  - BridgeMessage interface → WebView2 IPC message structure
  - BridgeResponse interface → WebView2 IPC response structure
  - ErrorDetails interface → Error code and data from backend

- **errorCodes.ts** → `src/shared/constants/errorCodes.ts`
  - ErrorCodes constants → MOD_FOLDER_IN_USE, MOD_ARCHIVE_NOT_FOUND, etc.
  - ErrorCode type → Union type of all error codes
  - Created: 2026-02-21

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
| typescript | 5.9.3 | Type safety |
| antd | 6.3.0 | UI components |
| vite | 7.3.1 | Build tool |
| react-i18next | 16.5.4 | Internationalization |

---

## Naming Conventions

- **PascalCase** for React components: `App.tsx`, `ModTable.tsx`, `GradingTag.tsx`
- **camelCase** for hooks: `useModData.ts`, `useModFilters.ts`, `useModActions.ts`
- **camelCase** for services: `modService.ts`, `photino.ts`
- **lowercase.type** for types: `mod.types.ts`, `message.types.ts`
- **camelCase** for utils: `grading.utils.ts`
- **Folders:** `components/`, `hooks/`, `types/`, `utils/`, `services/`

---

**Line Count:** ~700 lines
**Last Updated:** 2026-03-07 (Added ModProvider and modsStore architecture documentation)
**Parent:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Note:** If this file exceeds 800 lines, consider splitting into:
- `FRONTEND_COMPONENTS.md` (components only)
- `FRONTEND_STATE.md` (stores, providers, context)
- `FRONTEND_HOOKS_SERVICES.md` (hooks, services, utils)
