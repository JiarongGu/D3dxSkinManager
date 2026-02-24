# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> Detailed changes are preserved in git history.

---

## [Unreleased]

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
Fixed Console.WriteLine usage (5 Composition files → ILogger), NotImplementedException (2 files → graceful returns), frontend services (3 services → extend BaseModuleService).
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
