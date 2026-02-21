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
