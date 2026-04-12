---
name: doc-update-reference
description: Update KEYWORDS_INDEX.md after adding new files, constants, IPC messages, or skills.
---

# Doc Update Reference

**Format**: `/doc-update-reference <EntryType> <Details>`
**EntryTypes**: `new-path` | `new-keyword` | `new-constant` | `new-ipc` | `update-entry`

## Action

1. Read `docs/KEYWORDS_INDEX.md` (and the relevant keyword file under `docs/keywords/`)
2. Add the new entry in the appropriate section, maintaining existing format
3. For new IPC messages: add to the module's section in `docs/keywords/BACKEND.md`
4. For new components/hooks: add to `docs/keywords/FRONTEND.md`
5. For new file paths: add to the relevant keyword file
6. Update "Last Updated" date
7. Write the updated file(s)
