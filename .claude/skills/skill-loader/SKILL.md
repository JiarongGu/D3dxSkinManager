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

After outputting INVOKE/SKIP, **always remind to check `.claude/rules/*.md`** for rules that match the task scope. Critical rules that apply to most frontend/backend work:

| Rule File | When It Applies |
|---|---|
| `enum-serialization.md` | ANY new TypeScript type that maps to a C# enum — enums must be camelCase |
| `ui-design-rules.md` | ANY CSS or UI component work — font sizes (12/14px only), pattern reuse, Ant Design gotchas |
| `context-menu-extension.md` | Adding context menu items to category tree |

These rules contain hard-won fixes from past sessions. Ignoring them causes repeated bugs.
