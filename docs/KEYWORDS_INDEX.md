# Keywords Index

> **🤖 AI ASSISTANTS:** Use this file first! It's the fastest way to find files, classes, and concepts.
>
> **Format:** `Keyword → File Path : Line Number (if applicable)`

**Purpose:** Quick lookup for RAG systems - find files without loading large documents.

**Last Updated:** 2026-02-18 (v2.2 - Plugin System Refactoring + Settings Fixes)

---

## Quick Navigation

| Category | Jump To |
|----------|---------|
| [Backend Classes](#backend-classes-c) | Facades, Services, Models, Configuration |
| [Frontend Components](#frontend-components-react) | React components, Layout, Common |
| [Frontend Hooks](#frontend-hooks-react) | Custom hooks for logic |
| [Frontend Types](#frontend-types-typescript) | TypeScript type definitions |
| [Frontend Services](#frontend-services-typescript) | API wrappers, Utilities |
| [Configuration](#configuration-files) | Config, Build scripts, DI setup |
| [Documentation](#documentation-files) | Docs, Guides |
| [Common Tasks](#common-tasks) | How-to quick links |
| [Feature Analysis](#feature-analysis) | Feature parity, gap analysis |

---

## Backend Classes (C#)

### Entry Point
- **Program** → `D3dxSkinManager/Program.cs`
  - Main method → `:11`
  - InitializeServices (DI setup) → `:24-38`
  - Photino window setup → `:42-55`
  - IPC message handler → `:65-120`

### Configuration (DI Container) ⭐ NEW

- **ServiceCollectionExtensions** → `D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs`
  - AddD3dxSkinManagerServices → `:12-45`
  - Registers all services with DI container
  - Configures service lifetimes (all Singleton)

### Facades ⭐ NEW

- **IModFacade** (interface) → `D3dxSkinManager/Facades/IModFacade.cs`
  - GetAllModsAsync → `:10`
  - LoadModAsync → `:11`
  - UnloadModAsync → `:12`
  - ImportModAsync → `:13`
  - SearchModsAsync → `:14`

- **ModFacade** (implementation) → `D3dxSkinManager/Facades/ModFacade.cs:14`
  - Constructor (DI) → `:21-34`
  - GetAllModsAsync → `:37`
  - LoadModAsync → `:40-48`
  - UnloadModAsync → `:51-59`
  - ImportModAsync → `:62`
  - SearchModsAsync → `:65`
  - GetLoadedModsAsync → `:68`
  - DeleteModAsync → `:71-79`

### Services - Repository Layer ⭐ NEW

- **IModRepository** (interface) → `D3dxSkinManager/Services/ModRepository.cs:13-30`

- **ModRepository** (implementation) → `D3dxSkinManager/Services/ModRepository.cs:32`
  - Constructor → `:37-42`
  - InitializeDatabaseAsync → `:45-80`
  - GetAllAsync → `:83-105`
  - GetByIdAsync → `:108-131`
  - ExistsAsync → `:134-149`
  - InsertAsync → `:152-179`
  - UpdateAsync → `:182-207`
  - DeleteAsync → `:210-224`
  - GetByObjectNameAsync → `:227-249`
  - GetLoadedIdsAsync → `:252-269`
  - GetDistinctObjectNamesAsync → `:272-289`
  - GetDistinctAuthorsAsync → `:292-309`
  - GetAllTagsAsync → `:312-329`
  - SetLoadedStateAsync → `:332-347`

### Services - Domain Services ⭐ NEW

- **IModArchiveService** (interface) → `D3dxSkinManager/Services/ServiceInterfaces.cs:7-13`

- **ModArchiveService** → `D3dxSkinManager/Services/ModArchiveService.cs:13`
  - Constructor → `:20-25`
  - LoadAsync → `:28-43`
  - UnloadAsync → `:46-61`
  - DeleteAsync → `:64-79`
  - CopyArchiveAsync → `:82-102`

- **IModImportService** (interface) → `D3dxSkinManager/Services/ServiceInterfaces.cs:15-19`

- **ModImportService** → `D3dxSkinManager/Services/ModImportService.cs:14`
  - Constructor → `:26-40`
  - ImportAsync → `:43-120` (complete import workflow)
  - ReadMetadataAsync → `:123-145`
  - GenerateNameFromDirectory → `:148-160`

- **IModQueryService** (interface) → `D3dxSkinManager/Services/ServiceInterfaces.cs:21-25`

- **ModQueryService** → `D3dxSkinManager/Services/ModQueryService.cs:10`
  - Constructor → `:15-18`
  - SearchAsync → `:21-80` (supports ! negation, AND logic)

### Services - Low-Level Services

- **IFileService** (interface) → `D3dxSkinManager/Services/FileService.cs:11-22`

- **FileService** → `D3dxSkinManager/Services/FileService.cs:24`
  - CalculateSha256Async → `:26-48`
  - ExtractArchiveAsync → `:51-90`
  - CopyDirectoryAsync → `:93-120`
  - DeleteDirectoryAsync → `:123-137`
  - Is7ZipAvailable → `:140-142`
  - Get7ZipPath → `:145-165`

- **IClassificationService** (interface) → `D3dxSkinManager/Services/ClassificationService.cs:11-20`

- **ClassificationService** → `D3dxSkinManager/Services/ClassificationService.cs:22`
  - ClassifyModAsync → `:29-65`
  - LoadRulesAsync → `:68-90`
  - GetRules → `:93`
  - AddRule → `:96`
  - SaveRulesAsync → `:99-110`

- **IImageService** (interface) → `D3dxSkinManager/Services/ImageService.cs:13-24`

- **ImageService** → `D3dxSkinManager/Modules/Core/Services/ImageService.cs:26`
  - GetThumbnailPathAsync → `:72-85`
  - GetPreviewPathsAsync → `:87-107` (NEW - scans previews/{SHA}/ folder for multiple previews)
  - GenerateThumbnailAsync → `:110-167`
  - GeneratePreviewsAsync → `:169-246` (Returns int count, creates previews in per-mod folders)
  - CacheImageAsync → `:249-277`
  - ResizeImageAsync → `:280-314`
  - ClearModCacheAsync → `:316-357` (Deletes entire preview folder)
  - GetSupportedImageExtensions → `:360-366`
  - GetImageAsDataUriAsync → `:368-394`
  - GetThumbnailAsDataUriAsync → `:396-400`
  - GetPreviewsAsDataUriAsync → `:402-414` (NEW - returns list of data URIs for all previews)

### Models ⭐ NEW

- **ModInfo** → `D3dxSkinManager/Modules/Mods/Models/ModInfo.cs:5`
  - Properties: SHA, ObjectName, Name, Author, Description, Type, Grading, Tags, IsLoaded, IsAvailable, ThumbnailPath, OriginalPath, WorkPath, CachePath, Category
  - Note: PreviewPath property removed - previews now scanned dynamically from previews/{SHA}/ folder

- **MessageRequest** → `D3dxSkinManager/Models/MessageRequest.cs:3`
  - Properties: Id, Type, Payload

- **MessageResponse** → `D3dxSkinManager/Models/MessageResponse.cs:3`
  - Properties: Id, Success, Data, Error

### Database
- **SQLite Connection** → `D3dxSkinManager/Services/ModRepository.cs:37`
- **Mods Table Schema** → `D3dxSkinManager/Services/ModRepository.cs:49-78`

### Plugin System ⭐ UPDATED 2026-02-18

> **Location Changed:** Plugins moved to modular architecture
> **OLD:** `D3dxSkinManager/Plugins/` → **NEW:** `D3dxSkinManager/Modules/Plugins/`

#### Plugin Infrastructure (Backend)

- **IPlugin** → `D3dxSkinManager/Modules/Plugins/Services/IPlugin.cs`
  - Base plugin interface
  - Properties: Id, Name, Version, Author, Description

- **IServicePlugin** → `D3dxSkinManager/Modules/Plugins/Services/IServicePlugin.cs`
  - Interface for plugins that provide services

- **IMessageHandlerPlugin** → `D3dxSkinManager/Modules/Plugins/Services/IMessageHandlerPlugin.cs`
  - Interface for plugins that handle IPC messages

- **PluginEventBus** → `D3dxSkinManager/Modules/Plugins/Services/PluginEventBus.cs`
  - Event bus for plugin communication
  - EmitAsync (virtual for mocking) → `:45`

- **PluginLoader** → `D3dxSkinManager/Modules/Plugins/Services/PluginLoader.cs`
  - Loads plugins from plugins directory
  - Constructor requires: pluginsPath, registry, services, logger

- **PluginRegistry** → `D3dxSkinManager/Modules/Plugins/Services/PluginRegistry.cs`
  - Registry of loaded plugins

- **IPluginContext** → `D3dxSkinManager/Modules/Plugins/Services/IPluginContext.cs`
- **PluginContext** → `D3dxSkinManager/Modules/Plugins/Services/PluginContext.cs`

#### Plugin Facade & DI

- **IPluginsFacade** → `D3dxSkinManager/Modules/Plugins/IPluginsFacade.cs`
  - Inherits IModuleFacade for IPC routing

- **PluginsFacade** → `D3dxSkinManager/Modules/Plugins/PluginsFacade.cs`
  - Handles plugin-related IPC messages

- **PluginsServiceExtensions** → `D3dxSkinManager/Modules/Plugins/PluginsServiceExtensions.cs`
  - DI registration for plugin module

- **ServiceCollectionExtensions** → `D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs:68-74`
  - PluginLoader factory registration (root level, not in module)

#### Plugin Models

- **PluginInfo** → `D3dxSkinManager/Modules/Plugins/Models/PluginInfo.cs`
  - DTO for plugin information (IPC)

#### External Plugins (27 Projects)

Located in `Plugins/` directory (external to backend):
- ScreenCapture, BatchProcessingTools, CacheClearup, etc.
- **Namespace:** All use `D3dxSkinManager.Modules.Plugins.Services` for infrastructure
- **Target Framework:** net8.0-windows

---

## Frontend Components (React)

### Main Application

- **App** → `D3dxSkinManager.Client/src/App.tsx:23` ⭐ ENHANCED
  - Main application component with theme support
  - AppWithProviders → Root component with ThemeProvider
  - App → ConfigProvider with Ant Design theme algorithms
  - AppContent → Main content with hooks
  - useModData, useModFilters, useModActions hooks
  - Updated: 2026-02-18 - Added theme system with ConfigProvider integration

### Layout Components ⭐

- **AppHeader** → `D3dxSkinManager.Client/src/components/layout/AppHeader.tsx:8`
  - Application header with branding

- **AppSider** → `D3dxSkinManager.Client/src/components/layout/AppSider.tsx:14`
  - Navigation sidebar with menu (5 tabs)
  - Props: selectedTab, onTabChange
  - Updated: 2026-02-17 - Added Tools and Plugins tabs

- **AppStatusBar** → `D3dxSkinManager.Client/src/components/layout/AppStatusBar.tsx:26` ⭐ ENHANCED
  - Status bar with progress, color-coded messages, Help/Suggestions links
  - Props: userName, serverStatus, modsLoaded, modsTotal, statusMessage, statusType, progressPercent, progressVisible, onHelpClick, onSuggestionsClick
  - Updated: 2026-02-17 Phase 3 - Added progress bar, color coding, action buttons
  - Updated: 2026-02-18 - Theme-aware colors, borders, and status indicators

### Common Components ⭐

- **GradingTag** → `D3dxSkinManager.Client/src/components/common/GradingTag.tsx:7`
  - Color-coded grading badge component
  - Props: grading

- **StatusIcon** → `D3dxSkinManager.Client/src/components/common/StatusIcon.tsx:7`
  - Load status indicator (loaded/unloaded)
  - Props: isLoaded

- **ModThumbnail** → `D3dxSkinManager.Client/src/components/common/ModThumbnail.tsx:8`
  - Thumbnail image with fallback icon
  - Props: thumbnailPath, alt

- **ContextMenu** → `D3dxSkinManager.Client/src/components/common/ContextMenu.tsx:24` ⭐ NEW (Phase 2)
  - Reusable context menu component
  - Props: items (ContextMenuItem[]), children, trigger
  - Supports: conditional visibility, disabled states, nested menus, dividers
  - Created: 2026-02-17

- **DragDropZone** → `D3dxSkinManager.Client/src/components/common/DragDropZone.tsx:18` ⭐ NEW (Phase 4)
  - Drag & drop zone for files and folders
  - Props: onFilesDrop, onFolderDrop, accept, children, disabled, showOverlay
  - File type filtering, visual feedback overlay
  - Automatic categorization (images vs archives)
  - Created: 2026-02-17

- **TooltipSystem** → `D3dxSkinManager.Client/src/components/common/TooltipSystem.tsx` ⭐ NEW (Phase 7)
  - Annotation system with level-based tooltip display
  - AnnotationProvider (context provider) → `:45`
  - useAnnotation (hook) → `:63`
  - AnnotatedTooltip (component) → `:84`
  - annotations (content library) → `:130-305`
  - Annotation levels: all, more, less, off
  - Tooltip levels: 1 (basic), 2 (detailed), 3 (expert)
  - localStorage persistence
  - Created: 2026-02-17 Phase 7

- **MultiTagInput** → `D3dxSkinManager.Client/src/shared/components/common/MultiTagInput.tsx` ⭐ NEW
  - Multi-tag input with autocomplete dropdown
  - Props: value, onChange, availableTags, onOpenTagSelector, placeholder, maxTagTextLength
  - Features: Type to add tags, autocomplete suggestions, create new tags on save
  - Comma separator support, max tag length validation (default 50 chars)
  - Button to open tag selector dialog
  - Responsive tag display with maxTagCount
  - Created: 2026-02-18

- **Compact Component Library** → `D3dxSkinManager.Client/src/shared/components/compact/` ⭐
  - Standardized component library for consistent sizing and styling throughout the application
  - **Location:** Dedicated `compact/` folder with index.ts for clean imports
  - **Import:** `import { CompactButton, CompactCard } from 'shared/components/compact'`
  - **Usage:** Always use Compact variants instead of standard Ant Design components for UI consistency

  **Components:**
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

  **Folder Reorganization:** 2026-02-18 - Moved from `common/` to `compact/` folder + added index.ts

- **SlideInScreen** → `D3dxSkinManager.Client/src/shared/components/common/SlideInScreen.tsx` ⭐ ENHANCED
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

### Context Providers ⭐ NEW

- **ThemeContext** → `D3dxSkinManager.Client/src/shared/context/ThemeContext.tsx` ⭐ NEW
  - ThemeProvider (context provider) → `:42`
  - useTheme (hook) → `:84`
  - Theme modes: light, dark, auto
  - System theme detection and auto-switching
  - localStorage persistence
  - data-theme attribute management
  - Created: 2026-02-18

- **ProfileContext** → `D3dxSkinManager.Client/src/shared/context/ProfileContext.tsx` ⭐ NEW
  - ProfileProvider (context provider) → `:48`
  - useProfile (hook) → `:94`
  - Profile state management and IPC integration
  - Created: 2026-02-18

### Mod Components ⭐

- **ModTable** → `D3dxSkinManager.Client/src/components/mods/ModTable.tsx:17` ⭐ ENHANCED
  - Main table component with Ant Design Table
  - Props: mods, loading, objects, authors, onLoad, onUnload, onDelete, onRowClick, selectedMod
  - Updated: 2026-02-17 Phase 2 - Added comprehensive 15-item context menu
  - Context menu: Load/Unload, Edit, Export, Copy SHA/Name, View Files, Add Folder/Archive, Delete

- **ModHierarchicalView** → `D3dxSkinManager.Client/src/components/mods/ModHierarchicalView.tsx:20` ⭐ ENHANCED
  - Three-panel hierarchical layout (Classification Tree → Mods Table → Preview Panel)
  - Props: mods, loading, onLoad, onUnload, onDelete
  - Updated: 2026-02-17 Phase 1 - Added search bars to Classification and Mods panels
  - Updated: 2026-02-17 Phase 2 - Added context menus to Classification Tree
  - Updated: 2026-02-17 Phase 4 - Integrated DragDropZone for file dropping
  - Search with count indicators [filtered/total]

- **ModPreviewPanel** → `D3dxSkinManager.Client/src/components/mods/ModPreviewPanel.tsx:11` ⭐ NEW
  - Preview panel for selected mod with large image and metadata
  - Props: mod (ModInfo | null)
  - Displays: preview image, name, object, author, tags, description, SHA with copy button
  - Created: Earlier in roadmap implementation

- **ModTableColumns** → `D3dxSkinManager.Client/src/components/mods/ModTableColumns.tsx:20`
  - createModTableColumns function → `:20`
  - Column configuration for mod table
  - Returns: ColumnsType<ModInfo>

- **ModSearchBar** → `D3dxSkinManager.Client/src/components/mods/ModSearchBar.tsx:11`
  - Search input with ! negation support
  - Props: value, onChange, onSearch

- **ModFilterPanel** → `D3dxSkinManager.Client/src/components/mods/ModFilterPanel.tsx:19`
  - Filter controls (object, grading)
  - Props: selectedObject, selectedGrading, objects, loading, callbacks

- **ModActionButtons** → `D3dxSkinManager.Client/src/components/mods/ModActionButtons.tsx:14`
  - Load/Unload/Delete action buttons
  - Props: mod, onLoad, onUnload, onDelete

- **ModManagementView** → `D3dxSkinManager.Client/src/components/mods/ModManagementView.tsx:29`
  - Complete mod management view (legacy - replaced by ModHierarchicalView)
  - Composes: ModSearchBar, ModFilterPanel, ModTable

### Dialog Components ⭐ NEW (Phase 5, Refactored 2026-02-18)

- **ModEditDialog** → `D3dxSkinManager.Client/src/modules/mods/components/ModEditDialog/` ⭐ REFACTORED
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
  - ModTagSelectorDialog moved into ModEditDialog folder: 2026-02-18

- **ModTagSelectorDialog** → `D3dxSkinManager.Client/src/modules/mods/components/ModEditDialog/ModTagSelectorDialog.tsx` ⭐ NEW
  - Tag selector dialog for mod editing workflow
  - Used by ModEditDialog's TagsSection and BatchEditDialog
  - Props: visible, availableTags, selectedTags, onConfirm, onCancel
  - Features: Search/filter, checkbox selection, Select All/Deselect All, selected count
  - Uses slide-in dialog pattern
  - Created: 2026-02-18
  - Renamed from TagSelectorDialog and moved to ModEditDialog folder: 2026-02-18

- **ImportTagSelectorDialog** → `D3dxSkinManager.Client/src/modules/mods/components/import/ImportTagSelectorDialog.tsx` ⭐ NEW
  - Tag selector dialog for import workflow
  - Used by import workflow (AddModUnit, BatchEditUnit)
  - Props: visible, availableTags, selectedTags, onConfirm, onCancel
  - Features: Search/filter, checkbox selection, Select All/Deselect All, selected count
  - Uses slide-in dialog pattern
  - Title: "Select Tags for Import"
  - Created: 2026-02-18
  - Separate from ModTagSelectorDialog for clear workflow separation

- **TagSelectDialog** → `D3dxSkinManager.Client/src/modules/core/components/dialogs/TagSelectDialog.tsx:17` ⭐ LEGACY
  - Legacy multi-select tag dialog (still available but not actively used)
  - Props: visible, selectedTags, availableTags, onSave, onCancel
  - Features: 13 common predefined tags, custom tag input, Select All/Clear All
  - Note: Replaced by ImportTagSelectorDialog for import workflow
  - Created: 2026-02-17 Phase 5

- **BatchEditDialog** → `D3dxSkinManager.Client/src/modules/mods/components/BatchEditDialog/` ⭐ REFACTORED
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

- **UnityArgsDialog** → `D3dxSkinManager.Client/src/components/dialogs/UnityArgsDialog.tsx:17` ⭐ NEW (Phase 8)
  - Unity game launch arguments configuration dialog
  - Props: visible, currentArgs, onSave, onCancel
  - Borderless window toggle, popup window mode, fullscreen mode
  - Screen dimensions (width×height) with spinboxes
  - Common resolutions helper panel
  - Parses existing args string and builds new args string
  - Created: 2026-02-17 Phase 8

- **FullScreenPreview** → `D3dxSkinManager.Client/src/components/dialogs/FullScreenPreview.tsx:10` ⭐ NEW (Phase 15)
  - Full-screen image preview modal
  - Props: visible, imageSrc, imageAlt, onClose
  - Black background (95% opacity) for optimal viewing
  - Image scales to 95vw×95vh maintaining aspect ratio
  - Click anywhere or ESC to close
  - Created: 2026-02-17 Phase 15.5

### Warehouse Components ⭐ NEW (Phase 10)

- **WarehouseView** → `D3dxSkinManager.Client/src/components/warehouse/WarehouseView.tsx:23` ⭐ NEW
  - Mod warehouse browsing and download component
  - Two-panel layout: Mod list table (left) + Preview panel (right)
  - Download progress tracking with status indicators
  - Status indicators: 已下载 (Downloaded), 正在下载... (Downloading)
  - Real-time search and filtering by name/category/author
  - Filter by Object/Category (Character, Weapon, UI)
  - Open in browser button for external mod links
  - Created: 2026-02-17 Phase 10

### File Dialog Services ⭐ NEW (Phase 11)

- **fileDialogService** → `D3dxSkinManager.Client/src/services/fileDialogService.ts` ⭐ NEW
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

### Keyboard Shortcuts & Help ⭐ NEW (Phases 13-14)

- **KeyboardShortcutManager** → `D3dxSkinManager.Client/src/utils/KeyboardShortcutManager.ts` ⭐ NEW
  - Global keyboard shortcut system with context-aware shortcuts
  - ShortcutConfig interface: key, modifiers, description, callback
  - register/unregister shortcuts, setContext for context-aware behavior
  - handleKeyDown with input field protection
  - formatShortcut for display (e.g., "Ctrl + F")
  - SHORTCUTS constants: FOCUS_SEARCH, SAVE, CANCEL, SUBMIT, etc.
  - Created: 2026-02-17 Phase 13

- **KeyboardShortcutsDialog** → `D3dxSkinManager.Client/src/components/dialogs/KeyboardShortcutsDialog.tsx:14` ⭐ NEW
  - Modal dialog displaying all keyboard shortcuts
  - Grouped by context (Global, Mod Management, Import Window, Dialogs)
  - Table format with shortcut tags and descriptions
  - Collapsible sections with dividers
  - Created: 2026-02-17 Phase 13

- **AboutDialog** → `D3dxSkinManager.Client/src/components/dialogs/AboutDialog.tsx:14` ⭐ NEW
  - App version and build information dialog
  - Technology stack display with Tags
  - Key features list, credits section
  - Resource links (GitHub, Docs, Issues)
  - MIT License footer
  - Created: 2026-02-17 Phase 14

- **HelpWindow** → `D3dxSkinManager.Client/src/components/windows/HelpWindow.tsx:20` ⭐ NEW
  - Comprehensive help documentation window
  - 4-tab interface: Quick Start, Features, Troubleshooting, Tips & Tricks
  - Collapsible panels for each feature
  - Alert components for visual emphasis
  - Common issues and solutions
  - Best practices and workflow tips
  - Created: 2026-02-17 Phase 14

### Visual Enhancements ⭐ NEW (Phase 12)

- **visual-enhancements.css** → `D3dxSkinManager.Client/src/styles/visual-enhancements.css` ⭐ NEW
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

### Import/Window Components ⭐ NEW (Phase 6)

- **AddModWindow** → `D3dxSkinManager.Client/src/components/windows/AddModWindow.tsx:37` ⭐ NEW
  - Import/Add Mod window with task queue table
  - Props: visible, tasks, onConfirm, onCancel, onEditTask, onRemoveTask, onBatchEdit, processing
  - Features: Task queue table, row selection, statistics footer, bulk operations
  - Task statuses: pending, processing, success, error, skipped
  - Created: 2026-02-17 Phase 6

- **AddModUnit** → `D3dxSkinManager.Client/src/components/windows/AddModUnit.tsx:17` ⭐ NEW
  - Single import task editing dialog
  - Props: visible, task, onSave, onCancel, onOpenTagSelector
  - Form fields: Name, Object, Description, Author, Grading, Tags
  - File info card with source path and type
  - Preview thumbnail display
  - Created: 2026-02-17 Phase 6

- **BatchEditUnit** → `D3dxSkinManager.Client/src/components/windows/BatchEditUnit.tsx:16` ⭐ NEW
  - Batch editing for multiple import tasks
  - Props: visible, selectedTasks, onSave, onCancel, onOpenTagSelector
  - Checkbox-based field selection
  - Field mask array for partial updates
  - Alert showing fields count and tasks count
  - Created: 2026-02-17 Phase 6

### Settings Components ⭐ UPDATED 2026-02-18

- **SettingsView** → `D3dxSkinManager.Client/src/modules/settings/components/SettingsView.tsx`
  - Settings tab with theme, logLevel, annotationLevel, thumbnailAlgorithm
  - **FIXED 2026-02-18:** Now properly saves all settings to backend
  - handleLogLevelChange → `:63-76` (calls settingsService.updateGlobalSetting)
  - handleAnnotationLevelChange → `:49-61` (calls settingsService.updateGlobalSetting)
  - handleThemeChange → `:78` (calls ThemeContext.setTheme)
  - handleThumbnailAlgorithmChange → `:67-77`

#### Backend Settings Services ⭐ UPDATED 2026-02-18

- **GlobalSettingsService** → `D3dxSkinManager/Modules/Settings/Services/GlobalSettingsService.cs`
  - **File Location:** `data/settings/global.json` (moved from `data/`)
  - **FIXED 2026-02-18:** Deadlock in UpdateSettingAsync resolved
  - GetSettingsAsync → `:41-81`
  - UpdateSettingsAsync → `:85-98`
  - UpdateSettingAsync → `:104-158` (fixed deadlock - no nested lock)
  - ResetSettingsAsync → `:163-174`

- **SettingsFacade** → `D3dxSkinManager/Modules/Settings/SettingsFacade.cs`
  - HandleMessageAsync → `:37-79` (routes UPDATE_FIELD, GET_GLOBAL, etc.)
  - UpdateGlobalSettingHandlerAsync → `:247-255`

- **settingsService** (Frontend) → `D3dxSkinManager.Client/src/modules/settings/services/settingsService.ts`
  - getGlobalSettings() → `:24-30`
  - updateGlobalSetting(key, value) → `:46-52` (sends UPDATE_FIELD IPC message)

### Tools Components ⭐ NEW

- **ToolsView** → `D3dxSkinManager.Client/src/components/tools/ToolsView.tsx:19`
  - Tools tab with cache management, tag management, and utilities
  - Features: Clear caches, cache browser, tag editor, mod order management
  - Created: Earlier in roadmap implementation

### Plugins Components ⭐ NEW

- **PluginsView** → `D3dxSkinManager.Client/src/components/plugins/PluginsView.tsx:20`
  - Plugins tab displaying all 26 plugins with enable/disable controls
  - Features: Plugin table, details modal, status indicators
  - Created: Earlier in roadmap implementation

---

## Frontend Hooks (React)

### Custom Hooks ⭐ NEW

- **useModData** → `D3dxSkinManager.Client/src/hooks/useModData.ts:8`
  - Data fetching and loading
  - Returns: { mods, loading, objects, authors, loadMods, loadFilters }
  - loadMods → `:16`
  - loadFilters → `:28`

- **useModFilters** → `D3dxSkinManager.Client/src/hooks/useModFilters.ts:8`
  - Filter state and logic
  - Returns: { filters, filteredMods, loading, updateFilter, clearFilters, handleSearch, hasActiveFilters }
  - filteredMods (computed) → `:14-36`
  - handleSearch → `:38-53`
  - updateFilter → `:55-57`
  - clearFilters → `:59-65`
  - hasActiveFilters → `:67-70`

- **useModActions** → `D3dxSkinManager.Client/src/hooks/useModActions.ts:6`
  - Mod operations (load, unload, delete)
  - Returns: { handleLoadMod, handleUnloadMod, handleDeleteMod }
  - handleLoadMod → `:7-15`
  - handleUnloadMod → `:17-25`
  - handleDeleteMod → `:27-45`

- **useTheme** → `D3dxSkinManager.Client/src/shared/context/ThemeContext.tsx:84` ⭐ NEW
  - Theme management hook
  - Returns: { theme, effectiveTheme, setTheme }
  - theme → Current theme mode (light/dark/auto)
  - effectiveTheme → Resolved theme (light/dark)
  - setTheme → Update theme preference
  - Created: 2026-02-18

---

## Frontend Types (TypeScript)

### Type Definitions ⭐ NEW

- **mod.types.ts** → `D3dxSkinManager.Client/src/types/mod.types.ts`
  - ModInfo interface → `:1-15`
  - GradingLevel type → `:17`
  - ModFilters interface → `:19-23`
  - ModStatistics interface → `:25-29`

- **message.types.ts** → `D3dxSkinManager.Client/src/types/message.types.ts`
  - MessageType union type → `:1-12`
  - PhotinoMessage interface → `:14-18`
  - PhotinoResponse interface → `:20-25`

---

## Frontend Services (TypeScript)

### API Communication

- **photinoService** → `D3dxSkinManager.Client/src/services/photino.ts:60` ⭐ ENHANCED
  - sendMessage → `:91-128`
  - initializeMessageReceiver → `:68-86`
  - simulateBackendResponse → `:133-162` (dev mode)
  - getMockMods → `:164-191` (dev mode)
  - activeProfileId → Profile integration for all IPC messages
  - Updated: 2026-02-18 - Added profile context integration

- **modService** → `D3dxSkinManager.Client/src/services/modService.ts:10`
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

### Utilities ⭐ NEW

- **grading.utils.ts** → `D3dxSkinManager.Client/src/utils/grading.utils.ts`
  - getGradingColor → `:3-11`
  - getGradingLabel → `:13-21`
  - gradingOptions → `:23-28`

- **logger.ts** → `D3dxSkinManager.Client/src/utils/logger.ts` ⭐ NEW (Phase 16.2)
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

---

## Configuration Files

### Project Files
- **.NET Project** → `D3dxSkinManager/D3dxSkinManager.csproj`
- **React package.json** → `D3dxSkinManager.Client/package.json`
- **Solution File** → `D3dxSkinManager.sln`

### Build Scripts
- **Production Build** → `build-production.ps1`
  - React build → `:11`
  - Copy to wwwroot → `:38`
  - .NET publish → `:53`

### Configuration
- **TypeScript Config** → `D3dxSkinManager.Client/tsconfig.json`
- **React Scripts** → `D3dxSkinManager.Client/package.json:24-27`
- **.gitignore** → `.gitignore`

### Styles ⭐ NEW
- **Theme Colors CSS** → `D3dxSkinManager.Client/src/styles/theme-colors.css` ⭐ NEW
  - Centralized color system with CSS custom properties
  - 50+ CSS variables for complete theme control
  - Light and dark theme definitions
  - Component-specific color overrides
  - Automatic Ant Design component styling
  - Created: 2026-02-18

- **Main Styles** → `D3dxSkinManager.Client/src/App.css`
  - Global application styles
  - Animation overrides (0.05s linear for performance)

- **Visual Enhancements** → `D3dxSkinManager.Client/src/styles/visual-enhancements.css`
  - UI polish and visual improvements

---

## Documentation Files

### Main Hubs
- **Developer Hub** → `docs/README.md`
- **AI Assistant Hub** → `docs/AI_GUIDE.md`
- **This File** → `docs/KEYWORDS_INDEX.md`
- **Change Log** → `docs/CHANGELOG.md`

### Core Docs
- **Project Overview** → `docs/core/PROJECT_OVERVIEW.md`
- **Architecture** → `docs/core/ARCHITECTURE.md` ⭐ (1000+ lines, fully updated)
- **Design Decisions** → `docs/core/DESIGN_DECISIONS.md` ⭐ NEW (Critical patterns)
  - Server-side processing pattern (with code examples)
  - IPC architecture and message formats
  - State management strategy
  - Component architecture principles
  - Refactoring strategy (implement first, refactor after)
- **Project Structure** → `docs/core/PROJECT_STRUCTURE.md`
- **Development Guide** → `docs/core/DEVELOPMENT.md`
- **Original Comparison** → `docs/core/ORIGINAL_COMPARISON.md`
- **Migration Guide** → `docs/core/MIGRATION_GUIDE.md`

### AI Assistant Guides
- **Guidelines** → `docs/ai-assistant/GUIDELINES.md`
- **Workflows** → `docs/ai-assistant/WORKFLOWS.md`
- **Reference** → `docs/ai-assistant/REFERENCE.md`
- **Troubleshooting** → `docs/ai-assistant/TROUBLESHOOTING.md`
- **Documentation Maintenance** → `docs/ai-assistant/DOCUMENTATION_MAINTENANCE.md`

### Root Documentation
- **Main README** → `README.md`
- **Architecture Overview** → `ARCHITECTURE.md` ⭐ (Updated for v2.0)
- **Quick Start** → `QUICKSTART.md`
- **Project Summary** → `PROJECT_SUMMARY.md`
- **Changes** → `CHANGES.md`
- **Move to Repo** → `MOVING_TO_NEW_REPO.md`

---

## Common Tasks

Quick links to documentation for common tasks:

### "How do I..."

| Task | Documentation | Relevant Files |
|------|--------------|----------------|
| **Add a new backend service?** | `docs/ai-assistant/WORKFLOWS.md#adding-services` | `ServiceCollectionExtensions.cs` |
| **Create service with DI?** ⭐ | `docs/core/ARCHITECTURE.md#dependency-injection` | `Configuration/` directory |
| **Add a React component?** | `docs/ai-assistant/WORKFLOWS.md#adding-components` | `components/` directories |
| **Create custom hook?** ⭐ | `docs/core/ARCHITECTURE.md#custom-hooks-pattern` | `hooks/` directory |
| **Add IPC message type?** | `docs/ai-assistant/WORKFLOWS.md#ipc-messages` | `photino.ts`, `Program.cs` |
| **Update the database schema?** | `docs/ai-assistant/WORKFLOWS.md#database-changes` | `ModRepository.cs:49` |
| **Build for production?** | `docs/core/DEVELOPMENT.md#building` | `build-production.ps1` |
| **Run in development?** | `docs/core/DEVELOPMENT.md#running` | See QUICKSTART.md |
| **Create a feature branch?** | `docs/ai-assistant/WORKFLOWS.md#git-workflow` | N/A |
| **Use Facade pattern?** ⭐ | `docs/core/ARCHITECTURE.md#facade-pattern` | `Facades/ModFacade.cs` |
| **Use Repository pattern?** ⭐ | `docs/core/ARCHITECTURE.md#repository-pattern` | `Services/ModRepository.cs` |

### "Where is..."

| What | Location |
|------|----------|
| **Main entry point (backend)?** | `D3dxSkinManager/Program.cs:11` |
| **DI container setup?** ⭐ | `D3dxSkinManager/Configuration/ServiceCollectionExtensions.cs:12` |
| **Service coordination?** ⭐ | `D3dxSkinManager/Facades/ModFacade.cs:14` |
| **Main UI component (frontend)?** | `D3dxSkinManager.Client/src/App.tsx:23` (81 lines) |
| **Data access layer?** ⭐ | `D3dxSkinManager/Services/ModRepository.cs:32` |
| **Mod file operations?** ⭐ | `D3dxSkinManager/Services/ModArchiveService.cs:13` |
| **Import workflow?** ⭐ | `D3dxSkinManager/Services/ModImportService.cs:14` |
| **Search logic?** ⭐ | `D3dxSkinManager/Services/ModQueryService.cs:10` |
| **Frontend-backend bridge?** | `D3dxSkinManager.Client/src/services/photino.ts:60` |
| **Custom hooks?** ⭐ | `D3dxSkinManager.Client/src/hooks/` directory |
| **Reusable components?** ⭐ | `D3dxSkinManager.Client/src/components/` directory |
| **Type definitions?** ⭐ | `D3dxSkinManager.Client/src/types/` directory |
| **SQLite database schema?** | `D3dxSkinManager/Services/ModRepository.cs:49` |
| **IPC message handler?** | `D3dxSkinManager/Program.cs:65` |

### "What is..."

| Concept | Documentation |
|---------|--------------|
| **Photino?** | `docs/core/PROJECT_OVERVIEW.md#technology-stack` |
| **Facade Pattern?** ⭐ | `docs/core/ARCHITECTURE.md#facade-pattern` |
| **Repository Pattern?** ⭐ | `docs/core/ARCHITECTURE.md#repository-pattern` |
| **Dependency Injection?** ⭐ | `docs/core/ARCHITECTURE.md#dependency-injection` |
| **Custom Hooks?** ⭐ | `docs/core/ARCHITECTURE.md#custom-hooks-pattern` |
| **Component Composition?** ⭐ | `docs/core/ARCHITECTURE.md#component-composition` |
| **ModFacade?** ⭐ | `docs/core/ARCHITECTURE.md#facade-layer` |
| **ModRepository?** ⭐ | `docs/core/ARCHITECTURE.md#repository-layer` |
| **IPC Communication?** | `docs/core/ARCHITECTURE.md#communication-flow` |
| **SQLite schema?** | `docs/core/ARCHITECTURE.md#database-schema` |
| **Project structure?** | `docs/core/PROJECT_STRUCTURE.md` |

---

## Feature Analysis

### Gap Analysis

- **Feature Gap Analysis** → `docs/features/FEATURE_GAP_ANALYSIS.md`
  - Python vs React comparison → Full document
  - Missing features → Section 1-5
  - Priority recommendations → Section 9
  - Backend API gaps → Section 10

### Missing Feature Categories

| Category | Python Features | React Features | Gap |
|----------|----------------|----------------|-----|
| **Context Menus** | 15 actions | 8 actions | 7 missing |
| **Settings Options** | 7 options | 4 options | 3 missing |
| **Additional Features** | 6 features | 1 feature | 5 missing |

### Priority Features to Implement

**Priority 1 (Critical):**
- View Original/Work/Cache Files
- Drag & Drop Import
- Full Screen Preview
- Edit Mod Metadata

**Priority 2 (Settings):**
- Log Level Configuration
- Annotation Level Persistence
- Custom Launch Program

**Priority 3 (Quality of Life):**
- Unload Button in Choices List
- Double-Click to Load Mod
- Click SHA to Copy
- Unity Args Builder

---

## Namespaces

### Backend (C#)
- **Root:** `D3dxSkinManager`
- **Configuration:** `D3dxSkinManager.Configuration` ⭐
- **Facades:** `D3dxSkinManager.Facades` ⭐
- **Models:** `D3dxSkinManager.Models` ⭐
- **Services:** `D3dxSkinManager.Services`

### Frontend (TypeScript)
- No traditional namespaces (ES modules)
- **Components:** `src/components/` ⭐
- **Hooks:** `src/hooks/` ⭐
- **Types:** `src/types/` ⭐
- **Utils:** `src/utils/` ⭐
- **Services:** `src/services/`

---

## Dependencies

### Backend (.NET)
| Package | Version | Purpose | File Reference |
|---------|---------|---------|----------------|
| Photino.NET | 4.0.16 | Desktop window framework | `Program.cs:4` |
| Microsoft.Data.Sqlite | 10.0.3 | SQLite database | `ModRepository.cs:6` |
| Newtonsoft.Json | 13.0.4 | JSON serialization | Various |
| Microsoft.Extensions.DependencyInjection | 10.0.3 | DI container ⭐ | `ServiceCollectionExtensions.cs:4` |
| System.Drawing.Common | 10.0.3 | Image processing ⭐ | `ImageService.cs:4` |
| xUnit | Latest | Unit testing ⭐ | Test project |
| Moq | 4.20.73 | Mocking ⭐ | Test project |
| FluentAssertions | 7.0.1 | Test assertions ⭐ | Test project |

### Frontend (React)
| Package | Version | Purpose | File Reference |
|---------|---------|---------|----------------|
| react | 19.2.4 | UI library | All `.tsx` files |
| typescript | 4.9.5 | Type safety | All `.ts/.tsx` files |
| antd | 6.3.0 | UI components | `App.tsx:2` |
| axios | 1.13.5 | HTTP client | (future use) |

---

## File Naming Conventions

### Backend (C#)
- **PascalCase** for files: `ModFacade.cs`, `ModRepository.cs`, `IModFacade.cs`
- **Folders:** `Configuration/`, `Facades/`, `Models/`, `Services/` ⭐

### Frontend (TypeScript/React)
- **PascalCase** for React components: `App.tsx`, `ModTable.tsx`, `GradingTag.tsx` ⭐
- **camelCase** for hooks: `useModData.ts`, `useModFilters.ts`, `useModActions.ts` ⭐
- **camelCase** for services: `modService.ts`, `photino.ts`
- **lowercase.type** for types: `mod.types.ts`, `message.types.ts` ⭐
- **camelCase** for utils: `grading.utils.ts` ⭐
- **Folders:** `components/`, `hooks/`, `types/`, `utils/`, `services/` ⭐

---

## Glossary

| Term | Definition | Reference |
|------|------------|-----------|
| **3DMigoto** | Game modding framework for DirectX | [External](https://github.com/bo3b/3Dmigoto/wiki) |
| **Mod** | Skin/texture modification file | `docs/core/PROJECT_OVERVIEW.md` |
| **SHA** | SHA256 hash of mod file (unique ID) | `ModInfo.cs` |
| **Object** | In-game character/object name | `ModInfo` |
| **Photino** | .NET wrapper for native OS windows | [External](https://tryphotino.io) |
| **IPC** | Inter-Process Communication (C# ↔ React) | `photino.ts`, `Program.cs:65` |
| **Facade** | Pattern for service coordination ⭐ | `docs/core/ARCHITECTURE.md#facade-pattern` |
| **Repository** | Pattern for data access abstraction ⭐ | `docs/core/ARCHITECTURE.md#repository-pattern` |
| **DI** | Dependency Injection ⭐ | `docs/core/ARCHITECTURE.md#dependency-injection` |
| **Custom Hook** | Reusable React logic ⭐ | `docs/core/ARCHITECTURE.md#custom-hooks-pattern` |
| **RAG** | Retrieval-Augmented Generation (AI pattern) | `docs/AI_GUIDE.md` |

---

## Update Instructions

### When to Update This File

Add entries when:
- ✅ Creating new classes or services
- ✅ Adding new React components or hooks ⭐
- ✅ Creating new documentation files
- ✅ Adding new concepts or terms
- ✅ Finding yourself searching for something >2 times
- ✅ Refactoring major components ⭐

### How to Update

1. **Add keyword in alphabetical order within section**
2. **Include file path and line number**: `File.cs:123`
3. **Use relative paths from project root**
4. **Keep entries concise** - one line per entry
5. **Update "Last Updated" date at top**
6. **Mark new v2.0 items with ⭐**

### Format
```markdown
- **KeywordName** → `path/to/file.ext:lineNumber`
  - Optional sub-detail → `:otherLineNumber`
```

---

## Search Tips

### Using This File with AI RAG

1. **Ctrl+F / Cmd+F** - Search for keywords
2. **Use section headers** - Jump to relevant category
3. **Follow file paths** - Load specific files, not entire docs
4. **Check line numbers** - Go directly to relevant code

### Example Queries

**"Where is ModFacade?"**
→ Search "ModFacade" in this file
→ Find: `D3dxSkinManager/Facades/ModFacade.cs:14`
→ Load that file only

**"How do I use DI?"**
→ See [Common Tasks](#common-tasks)
→ Jump to: `docs/core/ARCHITECTURE.md#dependency-injection`

**"Where are custom hooks?"**
→ Search "Custom Hooks" or "hooks/"
→ Find: `D3dxSkinManager.Client/src/hooks/` directory
→ Individual hook files listed above

---

⭐ = New or significantly changed in v2.0 (Major Refactoring)

*This index is maintained by developers and AI assistants. Keep it updated!*

*Last updated: 2026-02-17 (v2.0)*
