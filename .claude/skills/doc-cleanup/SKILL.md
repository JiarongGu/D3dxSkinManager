---
name: doc-cleanup
description: Delete redundant docs, consolidate duplicates, or archive deprecated content. Backs up before deleting.
---

# Doc Cleanup

**Format**: `/doc-cleanup <Operation> <Target> <Reason>`

## Operations

| Operation | What it does |
|-----------|-------------|
| `delete` | Delete a redundant doc. Backup to `docs/.cleanup-backups/`, update references in other docs. |
| `consolidate` | Merge content from multiple files into one. Remove duplicates, keep unique content. |
| `archive` | Move deprecated doc to `docs/.archive/` with deprecation header. |
| `deduplicate` | Find duplicate sections within/across files, replace duplicates with cross-references. |

## Safety Rules

1. Always create backup before destructive operations
2. Always search for references to the file before deleting (Grep for filename across docs/)
3. Always update references in other docs after delete/move
4. Require a reason for every operation

## Action

Execute the operation, report what was done in 3-5 lines. No verbose reports.
