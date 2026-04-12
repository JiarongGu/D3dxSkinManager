---
name: pattern-finder
description: Find existing code patterns in the codebase to use as reference. Gives concrete search commands.
---

# Pattern Finder

**Format**: `/pattern-finder <PatternType> [Module]`

## Action

Run the Glob/Grep commands for the pattern type below. Show top 2-3 results with file paths and a brief code snippet. Don't produce verbose reports — just find and show the patterns.

## Search Commands by Pattern Type

### Backend

| Pattern | Glob | Grep |
|---------|------|------|
| `service` | `Modules/{Module}/Services/*Service.cs` | `class.*Service.*:.*I.*Service` |
| `repository` | `Modules/{Module}/Repositories/*Repository.cs` | `class.*Repository` |
| `facade` | `Modules/{Module}/*Facade.cs` | `RouteMessageAsync\|HandleAsync` |
| `event-handler` | `Modules/{Module}/EventHandlers/*.cs` | `IProfileEventBus\|EmitAsync` |
| `file-watcher` | `Modules/**/Services/*.cs` | `FileSystemWatcher` |
| `cache-service` | `Modules/**/Services/*.cs` | `IMemoryCache\|GetOrCreateAsync` |
| `batch-operation` | `Modules/**/*.cs` | `Batch\|IN \(@` |

### Frontend

| Pattern | Glob | Grep |
|---------|------|------|
| `component` | `src/modules/{Module}/components/**/*.tsx` | `React\.FC\|export const.*=.*=>` |
| `ipc-service` | `src/shared/services/ipc/*Service.ts` | `extends BaseModuleService` |
| `context` | `src/**/context/*.tsx` or `src/**/*Context.tsx` | `createContext\|useContext` |
| `hook` | `src/**/hooks/use*.ts` or `src/**/use*.ts` | `export function use\|export const use` |
| `ag-grid` | `src/**/*.tsx` | `AgGridReact\|ColDef` |
| `context-menu` | `src/**/*ContextMenu*` | `ContextMenu\|contextMenu\|MenuProps` |
| `dialog` | `src/**/*Dialog*.tsx` | `ConfirmDialog\|visible.*onOk\|onCancel` |

### Cross-Cutting

| Pattern | Grep |
|---------|------|
| `error-handling` | `OperationException\|handleError` |
| `i18n` | `useTranslation\|t\(.*'\|en\.json` |
| `events` | `EmitAsync\|eventBus\.subscribe\|IProfileEventBus` |

When `Module` is provided, replace `{Module}` in paths. When omitted, use `*` wildcard.

### Rules Patterns (check AFTER code search)

After running code searches above, also scan `.claude/rules/*.md` for matching patterns:

```
Glob: .claude/rules/*.md
```

Rules files contain **wiring chains** — multi-file implementation sequences discovered in previous sessions (e.g., `context-menu-extension.md` describes the 4-file chain for adding category menu items). If a rule matches your task, **follow the rule over generic skill templates** — rules are battle-tested from real implementations.
