---
name: skill-loader
description: >
  Route to relevant code-gen skills based on task description. Core discovery skill — invoke on every coding task.
  Returns which skills to invoke via Skill() and which to skip. Replaces the "invoke ALL skills" approach.
---

# Skill Loader

**Format**: `/skill-loader "task description"`

## Purpose

Reads the task description and returns ONLY the skills that match, with invocation commands. This replaces loading all 20+ skill templates — only matched templates enter context.

## Action

1. Read the task description
2. Match against the routing table below
3. Output:
   - **INVOKE** list — skills to call via `Skill()` right now (with arguments if determinable)
   - **SKIP** list — skills that don't apply (one-line summary, no `Skill()` call needed)
4. After outputting the lists, **immediately invoke** every skill in the INVOKE list via `Skill()` tool calls

## Routing Table — Code-Gen Skills

| Trigger (any match = INVOKE) | Skill |
|------------------------------|-------|
| New C# service, business logic class, layer-2 service | `/backend-service` |
| New IPC facade, facade handler, routing layer | `/backend-facade` |
| New frontend IPC service or new method on existing service | `/ipc-service` |
| New React component, panel, dialog, screen, modal | `/react-component` |
| New error, exception, throw, OperationException | `/error-with-i18n` |
| Both backend handler + frontend method for same IPC call | `/ipc-message-pair` |
| Batch SQL delete/update by ID list | `/batch-operation` |
| New DI service registration | `/service-registration` |
| New event consolidation, event storm reduction | `/event-handler` |
| New FileSystemWatcher, file change monitoring | `/file-watcher` |

## Routing Table — Review / Concurrency Tasks (no code-gen, route to rules)

Some tasks generate no scaffolding but still have a mandatory reading list. When the task is a
**review, audit, or concurrency/file-system question**, no INVOKE skills apply — instead point at
the rule that holds the hard-won patterns:

| Trigger (any match) | No INVOKE skill — read this rule instead |
|---------------------|-------------------------------------------|
| file system conflict, race condition, concurrency, locking, deadlock, parallel ops | `.claude/rules/filesystem-operation-serialization.md` |
| raw `Directory.*` / `File.*` mutation on mod data, planner, operation queue | `.claude/rules/filesystem-operation-serialization.md` |
| background operation, status bar, long-running task | `.claude/rules/background-task-tracking.md` |

Output these as a **"Rules to READ"** list alongside the (often empty) INVOKE list.

## Routing Table — Release/CI Skills

| Trigger (any match = INVOKE) | Skill |
|------------------------------|-------|
| Release notes, changelog, release preparation | `/release-notes` |

## Routing Table — Doc/Audit Skills

| Trigger | Skill |
|---------|-------|
| After completing a feature (audit) | `/post-feature` |
| Need to update AI_GUIDE.md | `/doc-update-guide` |
| Need to update KEYWORDS_INDEX.md | `/doc-update-reference` |
| Need to update ADVANCED_PATTERNS or DESIGN_DECISIONS | `/doc-update-technical` |
| Audit doc health | `/doc-monitor` |
| Remove redundant docs | `/doc-cleanup` |
| Shrink oversized docs | `/doc-optimize` |

## Routing Table — Communication Skills

| Trigger | Skill |
|---------|-------|
| User asks for terse/brief/compressed mode, or says "caveman" | `/caveman` |

## Multiple Matches

Multiple matches are common and expected. A full-stack feature typically matches:
- `/backend-service` + `/ipc-message-pair` + `/react-component` + `/error-with-i18n` + `/service-registration`

A pure frontend fix might only match `/react-component` or nothing at all (modifying existing code).

## No Matches

If no code-gen skill matches (pure refactor, config change, doc edit, bug fix in existing code), output:
> No code-gen skills apply — proceed with manual implementation.

This is normal. Not every task needs generated scaffolding.

## Output Format

```
### Skills to INVOKE:
- `/backend-service` — new CleanupService for file scanning
- `/react-component` — new FileCleanupTool panel with tabs
- `/error-with-i18n` — cleanup error codes
- `/service-registration` — register CleanupService in DI

### Skills to SKIP:
- `/backend-facade` — extending existing ToolFacade
- `/ipc-service` — extending existing toolService.ts
- `/ipc-message-pair` — adding methods to existing pair
- `/batch-operation` — no batch SQL needed
- `/event-handler` — no event consolidation
- `/file-watcher` — no file watching needed
- `/post-feature` — run after implementation
- `/doc-update-*` — run after implementation
- `/doc-monitor`, `/doc-cleanup`, `/doc-optimize` — not requested
- `/caveman` — not requested
```

## Important

- This skill replaces the "invoke ALL 20+ skills" protocol
- Only INVOKE list skills get `Skill()` calls — this saves ~30-50K tokens per task
- The SKIP list is printed for transparency but no tool calls are made
- Doc/audit skills (`post-feature`, `doc-update-*`) are typically deferred to "after implementation"
- Communication skills (`caveman`) only match when explicitly requested

## Mandatory Rules Check (ALWAYS — even when no skills match)

After outputting INVOKE/SKIP, **always remind to check `.claude/rules/*.md`**. These are cross-cutting
rules that apply to MOST frontend/backend work (the ones easy to forget):

| Rule File | When It Applies |
|---|---|
| `enum-serialization.md` | ANY new TypeScript type that maps to a C# enum — enums must be camelCase |
| `ui-design-rules.md` | ANY CSS/UI work — font sizes (12/14px), pattern reuse, Ant Design gotchas, theming |
| `ui-component-layers.md` | ANY component work — L1/L2/L3 layering, reuse compact atoms, never raw antd in L3 |
| `shared-utilities.md` | Before writing ANY util (formatBytes, clipboard, tree-flatten…) — reuse `shared/utils/` |
| `filesystem-operation-serialization.md` | ANY mod cache/archive/preview file op, concurrency, or `Directory.*`/`File.*` on mod data |
| `background-task-tracking.md` | ANY op >1s — fire-and-forget + ProcessRegistry, never block the IPC |
| `use-project-paths.md` | ANY file/scratch path — profile/global path services, never OS temp/AppData |
| `download-service.md` | ANY HTTP fetch — inject `IDownloadService`, never `new HttpClient` |
| `context-menu-extension.md` | Adding context-menu items to the category tree |
| `module-boundaries.md` | Any cross-module dependency — inject the sibling SERVICE, never another module's repository (+ reviewed-accepted exceptions) |
| `risky-change-tests-first.md` | Wide sweeps, shared type/generic changes, event wiring, boundary-moving refactors — write tests FIRST (they compile-guard + lock behavior), THEN change |

**Feature-specific rules** (remote, plugin, veil, xxmi, ini/merge, import, refix, webview-serving, …)
→ see the **By Feature → Authoritative Rule** table in `/doc-loader`. Match the feature and read that
rule FIRST — it beats the generic skill template and the `docs/` deep-dive.

These rules contain hard-won fixes from past sessions. Ignoring them causes repeated bugs.
