---
name: doc-update-guide
description: Update AI_GUIDE.md after adding a skill, feature, or workflow change. Bumps version and adds changelog entry.
---

# Doc Update Guide

**Format**: `/doc-update-guide <ChangeType> <Details>`
**ChangeTypes**: `new-skill` | `new-pattern` | `update-workflow` | `new-feature` | `deprecation`

## Action

1. Read `docs/AI_GUIDE.md`
2. Increment version (minor for additions, major for breaking changes)
3. Update "Last Updated" date
4. Based on ChangeType:
   - `new-skill` → add row to skills table
   - `new-pattern` → add to architecture patterns section
   - `update-workflow` → update the relevant workflow steps
   - `new-feature` → add to relevant section
   - `deprecation` → add strikethrough + replacement note
5. Write the updated file
