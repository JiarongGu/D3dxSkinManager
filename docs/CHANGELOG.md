# Changelog

All notable changes to the D3dxSkinManager project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **📋 Note**: This file contains summaries only (< 200 lines target).
> For detailed changes, see `changelogs/YYYY-MM/` folders.
> See [maintenance/CHANGELOG_MANAGEMENT.md](maintenance/CHANGELOG_MANAGEMENT.md) for guidelines.

---

## [Unreleased]

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
