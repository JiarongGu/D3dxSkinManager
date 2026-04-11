---
name: doc-cleanup
description: Use to delete redundant docs, remove temporary files, or consolidate duplicate content. Always backs up before deleting and updates references in other docs.
---

# Documentation Cleanup

Perform cleanup operations on documentation: remove redundant files, consolidate duplicates, remove outdated markers, and archive deprecated content.

**Purpose**: As the documentation system evolves, cleanup is needed to remove redundancy, consolidate content, and maintain documentation health. This skill provides safe, reversible cleanup operations.

## Arguments

**Format**: `/doc-cleanup <Operation> <Target> <Details>`

**Example**:
```
/doc-cleanup delete GUIDELINES "Content consolidated into AI_GUIDE.md"
/doc-cleanup consolidate "TESTING_GUIDE,TEST_PATTERNS" TESTING_GUIDE "Merged test patterns into main guide"
/doc-cleanup remove-markers AI_GUIDE NEW 30days
/doc-cleanup archive WORKFLOWS "Deprecated - replaced by skills"
```

**Parameters**:
- `Operation` - Cleanup operation (delete, consolidate, remove-markers, archive, deduplicate)
- `Target` - File(s) to clean up
- `Details` - Operation-specific details (reason, marker type, retention period)

## What This Skill Does

1. **Deletes redundant files**:
   - Removes fully redundant documentation
   - Creates deletion record
   - Updates references in other docs

2. **Consolidates duplicates**:
   - Merges content from multiple files
   - Removes duplicate sections
   - Updates cross-references

3. **Removes outdated markers**:
   - Removes NEW markers older than threshold
   - Removes UPDATED markers after retention
   - Cleans deprecated content past retention

4. **Archives deprecated content**:
   - Moves to archive folder
   - Adds deprecation metadata
   - Keeps for reference but removes from active docs

5. **Deduplicates content**:
   - Finds duplicate sections
   - Removes duplicates
   - Adds cross-references

## Operations

### 1. Delete Operation

**Purpose**: Permanently delete redundant documentation files.

**Format**: `/doc-cleanup delete <FileName> <Reason>`

**Example**:
```bash
/doc-cleanup delete GUIDELINES "95% redundant with AI_GUIDE.md - content consolidated"
```

**What it does**:
1. Validates file exists
2. Creates deletion record in `docs/.cleanup-log.md`
3. Searches for references to file in other docs
4. Updates or removes references
5. Deletes file
6. Generates deletion report

**Safety checks**:
- ❌ Won't delete if file has unique content (use consolidate instead)
- ❌ Won't delete if no reason provided
- ✅ Creates backup before deletion
- ✅ Logs deletion with timestamp and reason

**Deletion Record**:
```markdown
## Deleted: docs/ai-assistant/GUIDELINES.md
**Date**: 2026-04-11
**Reason**: 95% redundant with AI_GUIDE.md - content consolidated
**Backup**: `.cleanup-backups/GUIDELINES-2026-04-11.md`
**References Updated**:
- `docs/AI_GUIDE.md:15` - Removed reference
- `docs/CODE_GENERATION.md:42` - Updated to AI_GUIDE.md
```

### 2. Consolidate Operation

**Purpose**: Merge content from multiple files into one.

**Format**: `/doc-cleanup consolidate <SourceFiles> <TargetFile> <Reason>`

**Example**:
```bash
/doc-cleanup consolidate "TESTING_GUIDE,TEST_PATTERNS,UNIT_TEST_GUIDE" TESTING_GUIDE "Consolidated all test documentation"
```

**What it does**:
1. Reads all source files
2. Identifies unique content in each
3. Merges unique content into target file
4. Removes duplicates
5. Updates cross-references
6. Archives or deletes source files
7. Generates consolidation report

**Consolidation Report**:
```markdown
## Consolidation: Test Documentation
**Date**: 2026-04-11
**Target**: `docs/ai-assistant/TESTING_GUIDE.md`
**Sources**:
- `docs/ai-assistant/TEST_PATTERNS.md` (15KB) → 4KB unique content merged
- `docs/ai-assistant/UNIT_TEST_GUIDE.md` (8KB) → 2KB unique content merged

**Duplicates Removed**: 17KB
**Result**: TESTING_GUIDE.md now 30KB (was 23KB, added 7KB unique content)

**Actions Taken**:
- Deleted TEST_PATTERNS.md
- Deleted UNIT_TEST_GUIDE.md
- Updated references in 3 files
- Created backup in `.cleanup-backups/`
```

### 3. Remove Markers Operation

**Purpose**: Remove temporal markers (NEW, UPDATED, DEPRECATED) that have exceeded retention period.

**Format**: `/doc-cleanup remove-markers <Files> <MarkerType> <RetentionPeriod>`

**Example**:
```bash
/doc-cleanup remove-markers AI_GUIDE NEW 30days
/doc-cleanup remove-markers REFERENCE UPDATED 60days
/doc-cleanup remove-markers all DEPRECATED 90days
```

**What it does**:
1. Scans files for markers
2. Checks marker age (via git history or metadata)
3. Removes markers older than retention period
4. Generates removal report

**Retention Periods**:
- **NEW markers**: 30 days (default)
- **UPDATED markers**: 60 days (default)
- **DEPRECATED markers**: 90 days before archival (default)

**Example Removal**:
```markdown
<!-- BEFORE -->
| **backend-validator** | `/backend-validator Name Props` | FluentValidation class (NEW) |

<!-- AFTER (30+ days later) -->
| **backend-validator** | `/backend-validator Name Props` | FluentValidation class |
```

**Removal Report**:
```markdown
## Marker Removal: NEW markers
**Date**: 2026-04-11
**Files Scanned**: 5
**Markers Found**: 12
**Markers Removed**: 8 (>30 days old)
**Markers Kept**: 4 (<30 days old)

**Removed**:
- `docs/AI_GUIDE.md:42` - backend-validator skill (added 2026-03-01, 41 days ago)
- `docs/REFERENCE.md:78` - Validation constants (added 2026-03-05, 37 days ago)

**Kept**:
- `docs/AI_GUIDE.md:105` - doc-monitor skill (added 2026-04-10, 1 day ago)
```

### 4. Archive Operation

**Purpose**: Move deprecated content to archive folder for reference.

**Format**: `/doc-cleanup archive <FileName> <Reason>`

**Example**:
```bash
/doc-cleanup archive WORKFLOWS "Deprecated - replaced by skills system"
```

**What it does**:
1. Creates `docs/.archive/` folder if needed
2. Moves file to archive with timestamp
3. Adds deprecation metadata header
4. Updates references in active docs
5. Adds archive index entry
6. Generates archive report

**Archived File Structure**:
```markdown
---
archived: 2026-04-11
reason: Deprecated - replaced by skills system
replacement: .claude/skills/README.md
retention: Permanent (historical reference)
---

# [ARCHIVED] Workflows Guide

**NOTICE**: This document is archived and no longer maintained.
**Replacement**: See `.claude/skills/README.md` for current workflows.

[Original content follows...]
```

**Archive Index** (`docs/.archive/INDEX.md`):
```markdown
# Documentation Archive

## WORKFLOWS.md
**Archived**: 2026-04-11
**Reason**: Replaced by skills system
**Replacement**: `.claude/skills/README.md`
**Size**: 85KB
**Path**: `.archive/WORKFLOWS-2026-04-11.md`
```

### 5. Deduplicate Operation

**Purpose**: Find and remove duplicate content within a file or across files.

**Format**: `/doc-cleanup deduplicate <Target> <Scope>`

**Example**:
```bash
/doc-cleanup deduplicate AI_GUIDE self
/doc-cleanup deduplicate "AI_GUIDE,CODE_GENERATION" cross-file
```

**What it does**:
1. Scans for duplicate sections (>80% similarity)
2. Identifies canonical section (first occurrence or best quality)
3. Replaces duplicates with cross-references
4. Generates deduplication report

**Deduplication Example**:
```markdown
<!-- BEFORE (AI_GUIDE.md and CODE_GENERATION.md both have same content) -->

<!-- AI_GUIDE.md -->
## Skills System
Skills are reusable templates...
[Full description - 2KB]

<!-- CODE_GENERATION.md -->
## Skills System
Skills are reusable templates...
[Duplicate description - 2KB]

<!-- AFTER -->

<!-- AI_GUIDE.md -->
## Skills System
Skills are reusable templates...
[Full description - 2KB]

<!-- CODE_GENERATION.md -->
## Skills System
See [AI_GUIDE.md - Skills System](docs/AI_GUIDE.md#skills-system) for detailed information.
```

**Deduplication Report**:
```markdown
## Deduplication: Cross-file
**Date**: 2026-04-11
**Files**: AI_GUIDE.md, CODE_GENERATION.md
**Duplicates Found**: 3 sections
**Duplicates Removed**: 3 (6KB total)

**Actions**:
1. Skills System section (2KB duplicate) → Cross-reference in CODE_GENERATION.md
2. Agent Usage section (2KB duplicate) → Cross-reference in CODE_GENERATION.md
3. Evolution Process (2KB duplicate) → Cross-reference in CODE_GENERATION.md

**Result**: 6KB removed, replaced with 200B cross-references
**Savings**: 5.8KB (97% reduction)
```

## Safety Features

### Backup Before Cleanup

**All destructive operations create backups**:
```
docs/.cleanup-backups/
├── GUIDELINES-2026-04-11.md (deleted)
├── TEST_PATTERNS-2026-04-11.md (consolidated)
├── WORKFLOWS-2026-04-11.md (archived)
└── BACKUP_INDEX.md (backup log)
```

### Cleanup Log

**All operations logged**:
```markdown
# Cleanup Log

## 2026-04-11 14:30 - Delete Operation
- **File**: docs/ai-assistant/GUIDELINES.md
- **Operation**: delete
- **Reason**: 95% redundant with AI_GUIDE.md
- **Backup**: .cleanup-backups/GUIDELINES-2026-04-11.md
- **Status**: Success

## 2026-04-11 14:35 - Consolidate Operation
- **Files**: TEST_PATTERNS.md, UNIT_TEST_GUIDE.md → TESTING_GUIDE.md
- **Operation**: consolidate
- **Reason**: Consolidate all test documentation
- **Backup**: .cleanup-backups/TEST_PATTERNS-2026-04-11.md
- **Status**: Success
```

### Reference Validation

**Before deleting, checks for references**:
```python
# Pseudo-code
def safe_delete(file_path, reason):
    # Find references
    references = find_references(file_path)

    if references:
        print(f"WARNING: {len(references)} references found:")
        for ref in references:
            print(f"  - {ref.file}:{ref.line}")

        # Suggest updates
        print("\nRecommended actions:")
        for ref in references:
            print(f"  - Update {ref.file} to remove/replace reference")

    # Create backup
    backup_path = create_backup(file_path)

    # Log operation
    log_cleanup(file_path, "delete", reason, backup_path)

    # Delete
    delete_file(file_path)
```

## Important Rules

- ✅ Always create backups before destructive operations
- ✅ Always log cleanup operations
- ✅ Always check for references before deleting
- ✅ Always provide reason for cleanup
- ✅ Always update cross-references after cleanup
- ✅ Keep backups for at least 90 days
- ✅ Run doc-monitor before and after cleanup
- ❌ Don't delete without checking references
- ❌ Don't skip backup creation
- ❌ Don't consolidate without checking for unique content
- ❌ Don't remove markers without checking age

## Integration with Other Skills

**Typical cleanup workflow**:
```bash
# 1. Monitor for issues
/doc-monitor redundancy all

# 2. Review findings (e.g., GUIDELINES.md is 95% redundant)

# 3. Consolidate or delete (this skill)
/doc-cleanup delete GUIDELINES "Redundant with AI_GUIDE.md - content consolidated"

# 4. Verify cleanup
/doc-monitor broken-links all  # Should show 0 broken links

# 5. Run quarterly marker cleanup
/doc-cleanup remove-markers all NEW 30days
/doc-cleanup remove-markers all UPDATED 60days
```

**After major migration**:
```bash
# 1. Archive old workflow docs
/doc-cleanup archive WORKFLOWS "Replaced by skills system"

# 2. Remove temporary migration files
/doc-cleanup delete MIGRATION_TO_SKILLS "Temporary migration tracking - migration complete"
/doc-cleanup delete DOCS_CLEANUP_PLAN "Temporary planning doc - plan executed"
```

## Validation Checklist

After cleanup operation:

- [ ] Backup created in `.cleanup-backups/`
- [ ] Operation logged in `docs/.cleanup-log.md`
- [ ] References updated in other docs
- [ ] No broken links (verify with `/doc-monitor broken-links`)
- [ ] Cleanup report generated
- [ ] Health score improved (verify with `/doc-monitor all`)

## Best Practices

### Quarterly Cleanup Schedule

**Every 3 months**:
```bash
# 1. Remove old markers
/doc-cleanup remove-markers all NEW 30days
/doc-cleanup remove-markers all UPDATED 60days

# 2. Check for new redundancy
/doc-monitor redundancy all

# 3. Consolidate if needed
/doc-cleanup consolidate "file1,file2" target "reason"

# 4. Archive deprecated content past retention
/doc-cleanup remove-markers all DEPRECATED 90days
# Then archive any deprecated docs
```

### Before Major Release

**Pre-release cleanup**:
```bash
# 1. Full health check
/doc-monitor all all

# 2. Address critical issues
# (fix broken links, remove high redundancy)

# 3. Clean up markers
/doc-cleanup remove-markers all NEW 30days

# 4. Verify health improved
/doc-monitor all all
```

## Reference Examples

**Good Cleanup Operation**:
```bash
# Well-reasoned deletion
/doc-cleanup delete GUIDELINES "95% redundant with AI_GUIDE.md. Unique content (workflow diagrams) moved to AI_GUIDE.md on 2026-04-10. All references updated."
```

**Bad Cleanup Operation**:
```bash
# No reason provided
/doc-cleanup delete GUIDELINES  ❌

# Deleting without checking references
/doc-cleanup delete IMPORTANT_DOC "Not needed"  ❌ (Other docs might reference it!)
```

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-cleanup skill

**How to update this skill**:
1. If new cleanup operations needed, add to Operation options
2. If safety features evolve, update safety checks
3. If backup strategy changes, update backup process
4. If retention periods change, update retention defaults
