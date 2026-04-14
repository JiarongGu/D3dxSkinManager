# Background Task Status Bar Integration

When adding a new background operation that takes >1s, integrate it with the global task store so the status bar shows progress.

## Architecture

```
taskStore (Zustand)     ← components add/remove tasks
taskEventBridge         ← backend events update progress (global, init in App.tsx)
AppStatusBar            ← subscribes to taskStore, shows bar + hover panel
```

## Two integration patterns

### Pattern A: Direct async (archive update, delete, file scan)

For operations where the component awaits a single IPC call:

```typescript
import { useTaskStore } from 'shared/store/taskStore';

const taskId = `operation-name-${id}`;
useTaskStore.getState().addTask({ id: taskId, label: t('statusBar.tasks.myOp') });
try {
  await someService.doWork(...);
} finally {
  useTaskStore.getState().removeTask(taskId);
}
```

### Pattern B: Event-driven (analysis scan, package export/import)

For operations with backend progress events:

1. **Component** calls `addTask()` when starting the operation (with descriptive label)
2. **taskEventBridge** (global) subscribes to progress events and calls `updateTask()` with percentage
3. **taskEventBridge** calls `removeTask()` on completion event

Component only needs to add the task — bridge handles updates and cleanup.

## Key rules

- `addTask` is **idempotent** — duplicate IDs are ignored
- Task IDs must be unique per operation instance (use `operation-${modId}` for per-item ops)
- Use fixed IDs for singleton operations (`mod-analysis`, `mod-package`)
- Labels should be pre-translated using `t()` — the store holds display strings, not i18n keys
- Include context in labels: `"Analyzing mods: Weapons"` not just `"Analyzing..."`

## Existing integrations

| Operation | ID | Pattern | File |
|-----------|-----|---------|------|
| Archive update | `update-archive-{modId}` | A (direct) | ModList.tsx |
| Analysis scan | `mod-analysis` | B (events) | ModAnalyzerTool.tsx + taskEventBridge.ts |
| Package export | `mod-package` | A+B (direct start + event progress) | ModPackageContext.tsx + taskEventBridge.ts |
| Package import | `mod-package` | A+B | ModPackageContext.tsx + taskEventBridge.ts |
| File cleanup scan | `file-cleanup-scan` | A (direct) | FileCleanupTool.tsx |
| Delete duplicate | `delete-mod-{modId}` | A (direct) | ModAnalyzerTool.tsx |
