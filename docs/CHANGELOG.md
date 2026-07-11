# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> Detailed changes are preserved in git history.

---

## [Unreleased]

### Added - 2026-07-06 - Remote mod library (redesigned) + in-app user guide
**Summary**: Browse remote mod sites in-app and download+import in one flow, plus a built-in user guide.
- **Remote library**: per-profile libraries (site + game), synced local index (instant search/sort/offline),
  GameBanana + huihui (Hui站) engines, Cloudreve + Quark download resolvers, ordered tag→category import
  rules, a "downloaded" filter + "locate imported mod", imported-tagging, Quark login via an in-app WebView2
  window (client User-Agent bypasses the download size limit). See `.claude/knowledge/remote-library*.md`.
- **In-app guide**: `docs/user-guide/USER_GUIDE.{en,cn}.md` rendered by a zero-dep `MarkdownView` (typed
  callouts, grouped nav) in the Help window; also the README main page. See `.claude/knowledge/in-app-guide.md`.
- **Release/CI**: release.yml + build-production.ps1 now pack `res/` (languages + remote seeds) and generate
  `manifest.json` (auto-update file list); prior release shipped without res/.

### Changed - 2026-07-06 - UI standardization + cleanup pass
- Global slim scrollbars (one `* { scrollbar-width: thin }` — removed 7 per-component overrides);
  `CompactTitle` clamps to 12/14px (fixed oversized section titles); dead code removed (GET_SOURCE_TEMPLATE),
  raw antd `<Select>` → `CompactSelect`. See `.claude/knowledge/ui-design-rules.md`.

### Fixed - 2026-04-13 - File dialog crash on right-click (image files)
**Summary**: Right-clicking image files in any file dialog (Open/Save/Folder) crashed the app with STATUS_STACK_BUFFER_OVERRUN (0xc0000409).

**Root cause**: .NET 8+ enables CET (Hardware-enforced Stack Protection) by default. Windows shell extensions for image thumbnails/context menus aren't CET-compatible — they trigger shadow stack violations when loaded in file dialogs.

**Fix**:
- Added `<CetCompat>false</CetCompat>` to D3dxSkinManager.csproj — disables CET so shell extensions work
- Removed `AutoUpgradeEnabled = false` from all file dialogs — modern Vista+ dialog now safe to use

**Files changed**: D3dxSkinManager.csproj, SystemFileDialogService.cs

### Archived months

- **March 2026** — unified error handling, active-mods view, multi-select, generic window system, DropZone overlay, workflow SQLite, TagManagementTool, and more: [changelogs/2026-03/CHANGELOG_2026-03.md](changelogs/2026-03/CHANGELOG_2026-03.md)
- **Late February 2026** — Classification→Category refactor, event system rework, i18n system, WebView2 migration, tag management: [changelogs/2026-02/CHANGELOG_2026-02.md](changelogs/2026-02/CHANGELOG_2026-02.md)

---

## Early February 2026 - Archived

**Summary**: 30+ changes including drag-drop system, image navigation, archive support, menu components, preview management, window state persistence, and migration fixes. (Detailed entries for this period were pruned — see git history. Late-Feb entries: [changelogs/2026-02/CHANGELOG_2026-02.md](changelogs/2026-02/CHANGELOG_2026-02.md).)

**Highlights**:
- ⭐⭐⭐⭐ Archive 7z Support & Optimistic Update Fixes
- ⭐⭐⭐⭐ Reusable Optimistic Update Hook
- ⭐⭐⭐ Menu Component System (ContextMenu, PopupMenu, usePopupMenu)
- ⭐⭐⭐ Preview Image Management with Context Menu
- ⭐⭐⭐ Windows Gallery Image Navigation & CSS Refactoring
- ⭐⭐⭐ Code Quality Refactoring (removed 40+ `any` types)
- ⭐⭐⭐ Work Directory Refactoring
- ⭐⭐⭐ Dynamic Preview System (`previews/{ID}/`)
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

- **March 2026**: [changelogs/2026-03/CHANGELOG_2026-03.md](changelogs/2026-03/CHANGELOG_2026-03.md)
- **Late February 2026**: [changelogs/2026-02/CHANGELOG_2026-02.md](changelogs/2026-02/CHANGELOG_2026-02.md)
- **Detailed per-change notes**: See `changelogs/YYYY-MM/` folders

---

**Current Line Count**: ~80 lines (Target: < 200 lines) ✅
**Last Cleanup**: 2026-07-05
**Next Cleanup**: when > 150 lines (archive the oldest month into `changelogs/YYYY-MM/`)
