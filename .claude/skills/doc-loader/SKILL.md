---
name: doc-loader
description: Load the right project docs for a task. Routes by scope keyword to specific doc files.
---

# Doc Loader

**Format**: `/doc-loader "task description" <scope>`
**Scopes**: `backend` | `frontend` | `ipc` | `testing` | `architecture` | `all`

## Action

Read the docs listed for the matching scope. Skill routing is handled by `/skill-loader` (a separate core skill).

## Routing Table

**Always read first** (every task):
- `docs/AI_GUIDE.md` — skills table, architecture patterns

### By Scope

| Scope | Read These |
|-------|-----------|
| `backend` | `docs/core/DESIGN_DECISIONS.md`, `docs/keywords/BACKEND.md` |
| `frontend` | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md`, `docs/keywords/FRONTEND.md` |
| `ipc` | `docs/core/DESIGN_DECISIONS.md`, `docs/keywords/BACKEND.md`, `docs/keywords/FRONTEND.md` |
| `testing` | `docs/ai-assistant/TESTING_GUIDE.md` |
| `architecture` | `docs/core/DESIGN_DECISIONS.md`, `docs/architecture/CURRENT_ARCHITECTURE.md` |

### By Task Keyword (additional docs)

| Keyword in task | Also read |
|----------------|-----------|
| service, facade, event | `docs/core/DESIGN_DECISIONS.md` |
| component, hook, context | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` |
| error, i18n | `docs/core/DESIGN_DECISIONS.md` |
| cache | `docs/core/ADVANCED_PATTERNS.md` |
| database, migration | `docs/architecture/DATABASE_MIGRATION_ARCHITECTURE.md` |
| test | `docs/ai-assistant/TESTING_GUIDE.md` |
| batch | `docs/keywords/BACKEND.md` |
| menu, context menu, right-click | `docs/keywords/FRONTEND.md` |
| export, import, package | `docs/keywords/FRONTEND.md` |
| drag, drop, reorder | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md`, `docs/keywords/FRONTEND.md` |

### Rules Check (MANDATORY — do this after reading docs)

Scan `.claude/rules/*.md` filenames. If ANY rule file name matches the task (e.g., `context-menu-extension.md` for a context menu task), **read it** — rules contain wiring chains and implementation patterns discovered in previous sessions. These override generic skill templates when they exist.
