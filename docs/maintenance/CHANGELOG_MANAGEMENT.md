# CHANGELOG Management Guide

**Purpose**: Keep the main CHANGELOG.md file small, useful, and maintainable.

**Target**: Main CHANGELOG.md should be **under 200 lines** total.

---

## Core Principles

### 1. Main CHANGELOG.md = Summary Only
- **Purpose**: Quick overview of recent changes
- **Target Audience**: Users and developers looking for what changed
- **Size Limit**: Maximum 200 lines
- **Content**: Only recent changes (last 1-2 months)
- **Format**: 3-5 line summaries with links to detailed changelogs

### 2. Detailed Changelogs = In changelogs/ Folder
- **Location**: `docs/changelogs/YYYY-MM/YYYY-MM-DD-description.md`
- **Purpose**: Complete technical documentation of changes
- **No Size Limit**: Can be as detailed as needed
- **Content**: Problem, solution, files modified, code snippets, testing details

---

## Rules for Main CHANGELOG.md

### ✅ DO Include in Main CHANGELOG:
1. **Summary entries only** (3-5 lines each)
2. **Last 1-2 months** of changes
3. **Link to detailed changelog** for complex changes
4. **Key impact points** (tests passing, breaking changes)
5. **Version releases** and major milestones

### ❌ DO NOT Include in Main CHANGELOG:
1. **Detailed problem descriptions** → Move to detailed changelog
2. **Code snippets** → Move to detailed changelog
3. **Line-by-line file changes** → Move to detailed changelog
4. **Long impact analysis** → Move to detailed changelog
5. **Changes older than 2 months** → Archive to detailed changelog

---

## Summary Entry Format

### Template:
```markdown
### [Type] - YYYY-MM-DD - [Short Title] [Stars]

[1-2 sentence description of what changed and why]

**Summary:**
- Key point 1
- Key point 2
- Modified files (file1.cs, file2.tsx)
- Impact: ✅ Tests pass, builds successful

**Details**: [changelogs/YYYY-MM/YYYY-MM-DD-description.md](changelogs/YYYY-MM/YYYY-MM-DD-description.md)
```

### Example:
```markdown
### Fixed - 2026-02-20 - Migration Archive Storage ⭐⭐

Fixed migration to match Python version's archive storage format.

**Summary:**
- Archives: `{SHA}` (no extension) instead of `{SHA}.7z`
- Modified: `ModFileService.cs`, `MigrationStep5.cs`
- Impact: ✅ 173 tests pass

**Details**: [changelogs/2026-02/2026-02-20-migration-archive-storage-fix.md](changelogs/2026-02/2026-02-20-migration-archive-storage-fix.md)
```

---

## Maintenance Schedule

### Monthly Maintenance (1st of each month):
1. **Review CHANGELOG.md size**: If > 200 lines, archive old entries
2. **Archive entries older than 2 months**:
   - Move to `docs/changelogs/YYYY-MM/monthly-summary.md`
   - Keep only the section header in main CHANGELOG with archive link
3. **Update archive index**: Create/update archive summary files

### When Adding New Entry:
1. **Check if entry is detailed** (>20 lines):
   - YES → Create detailed file in `changelogs/YYYY-MM/`
   - Add summary entry to main CHANGELOG with link
2. **Check if entry is simple** (<10 lines):
   - YES → Can include directly in main CHANGELOG
   - Still consider creating detailed file for future reference

---

## Archive Process

### Step 1: Create Monthly Archive File
```bash
# Create archive for older month
touch docs/changelogs/2026-02/monthly-archive.md
```

### Step 2: Move Old Entries
Move entries older than 2 months from main CHANGELOG.md to the archive file.

### Step 3: Update Main CHANGELOG
Replace the old section with an archive link:

```markdown
## February 2026 - Archived

**Summary**: 15 changes including migration fixes, UI improvements, and refactoring.

**See**: [changelogs/2026-02/monthly-archive.md](changelogs/2026-02/monthly-archive.md) for complete details.

---
```

---

## File Structure & Naming Conventions

```
docs/
├── CHANGELOG.md                                    ← Summary only (< 200 lines)
│
└── changelogs/
    ├── 2026-01/
    │   ├── 2026-01-15-feature-x.md                ← Detailed entry
    │   ├── 2026-01-20-bug-fix-y.md                ← Detailed entry
    │   └── january-archive.md                     ← Month archive (end of month)
    │
    ├── 2026-02/
    │   ├── 2026-02-20-migration-fix.md            ← Detailed entry
    │   ├── 2026-02-19-refactoring.md              ← Detailed entry
    │   └── february-archive.md                    ← Month archive (end of month)
    │
    └── 2026-03/
        ├── 2026-03-05-new-feature.md
        └── ...
```

### Naming Conventions:

**Detailed Entry Files:**
- Format: `YYYY-MM-DD-short-description.md`
- Examples:
  - `2026-02-20-migration-archive-storage-fix.md`
  - `2026-02-19-code-quality-refactoring.md`
  - `2026-03-15-plugin-system-enhancement.md`

**Monthly Archive Files:**
- Format: `month-name-archive.md` (lowercase month name)
- Examples:
  - `january-archive.md`
  - `february-archive.md`
  - `march-archive.md`
- **Purpose**: Contains ALL detailed entries from that month when archived
- **Created**: End of month or when main CHANGELOG > 150 lines

---

## Decision Tree

```
New Change to Document
    │
    ├─ Is it simple/trivial? (< 10 lines)
    │   └─ YES → Add directly to main CHANGELOG
    │
    ├─ Is it detailed? (> 20 lines)
    │   └─ YES → Create detailed file + summary in main CHANGELOG
    │
    └─ Main CHANGELOG > 200 lines?
        └─ YES → Archive old entries (> 2 months)
```

---

## Quick Reference

| Scenario | Action |
|----------|--------|
| Simple bug fix | Add 3-5 line entry to main CHANGELOG |
| Feature addition | Create detailed file + summary link |
| Major refactoring | Create detailed file + summary link |
| Monthly review | Archive entries > 2 months old |
| CHANGELOG > 200 lines | Immediate cleanup required |

---

## For AI Assistants

### Before Adding to CHANGELOG:
1. Check current line count: `wc -l docs/CHANGELOG.md`
2. If > 200 lines: Archive old entries first
3. Decide: Summary only or detailed file needed?
4. If detailed: Create `changelogs/YYYY-MM/YYYY-MM-DD-name.md`
5. Add summary entry with link

### After Session:
1. Verify CHANGELOG.md is < 200 lines
2. Ensure all detailed changes have separate files
3. Check all links work correctly

---

## Examples

### ✅ Good Summary Entry (5 lines):
```markdown
### Fixed - 2026-02-20 - Archive Storage ⭐⭐
Fixed migration archive format. Modified: ModFileService.cs.
Impact: ✅ 173 tests pass.
**Details**: [changelogs/2026-02/2026-02-20-migration.md](...)
```

### ❌ Bad Entry (Too Detailed):
```markdown
### Fixed - 2026-02-20 - Archive Storage ⭐⭐
**Problem:**
The migration service was storing archives with extensions...
[20 more lines of detailed explanation]
[Code snippets]
[File-by-file changes]
```

---

**Last Updated**: 2026-02-20
**Owner**: AI Assistants + Development Team
**Review Schedule**: Monthly (1st of month)
