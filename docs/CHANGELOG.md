# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> For detailed changes, see `changelogs/YYYY-MM/` folders.
> See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) for guidelines.

---

## [Unreleased]

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
