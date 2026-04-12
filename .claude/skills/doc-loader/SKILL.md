---
name: doc-loader
description: Load the right project docs for a task. Routes by scope keyword to specific doc files.
---

# Doc Loader

**Format**: `/doc-loader "task description" <scope>`
**Scopes**: `backend` | `frontend` | `ipc` | `testing` | `architecture` | `all`

## Action

Read the docs listed for the matching scope, then INVOKE every matching code-gen skill from the Skill Routing Table below.

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

## After Loading — Skill Routing (MANDATORY)

After reading docs, **INVOKE** every code-gen skill that matches the task using the table below. Do NOT just suggest — call the Skill tool for each match so the template is loaded into context before writing any code.

### Skill Routing Table

| Task involves | INVOKE this skill |
|---------------|-------------------|
| New C# service class | `/backend-service` |
| New IPC facade or facade handler | `/backend-facade` |
| New frontend IPC service/method | `/ipc-service` |
| New React component, panel, dialog, screen | `/react-component` |
| New error/exception/throw | `/error-with-i18n` |
| Both backend handler + frontend method for one IPC call | `/ipc-message-pair` |
| Batch SQL delete/update by ID list | `/batch-operation` |
| New DI service registration | `/service-registration` |
| New event consolidation handler | `/event-handler` |
| New FileSystemWatcher | `/file-watcher` |

**Multiple matches are common** — a feature that adds a backend service + IPC endpoint + frontend component needs `/backend-service`, `/ipc-message-pair`, and `/react-component` all invoked.

If no code-gen skill matches (pure refactor, config change, doc edit), state "No code-gen skills apply" and proceed.
