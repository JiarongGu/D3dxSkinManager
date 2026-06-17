# Long-running process monitoring (ProcessRegistry → status bar + Activity panel)

When adding a background operation that takes >1s, register it with the **backend** `ProcessRegistry`
so it shows in the status bar + the download-manager-style **Activity panel**.

## Architecture (backend-authoritative)

```
IProcessRegistry (Core singleton)   ← the SINGLE source of truth for all long-running ops
  Start / Report / Complete / Fail / Cancel / GetAll / ClearCompleted
  → emits a consolidated SYSTEM/PROCESS_LIST_UPDATED snapshot via the global IEventBus
    → EventBusIpcBridge forwards it to the frontend
      → processStore (Zustand mirror, read-only)
        → AppStatusBar (running summary) + ActivityPanel (full list incl. history)
```

The old ephemeral frontend `taskStore`/`taskEventBridge` is **removed** — do not reintroduce a
frontend-only task store. Producers emit from the **backend**.

## How to register a long-running op (backend)

Inject `IProcessRegistry` (it's a Core singleton — available to profile-scoped services too) and wrap:

```csharp
var procId = _processRegistry.Start(ProcessType.ModLoad, $"Loading mod: {name}");
try {
    // ... work ...
    _processRegistry.Report(procId, percent);     // optional; null = indeterminate
    _processRegistry.Complete(procId);
} catch (Exception ex) {
    _processRegistry.Fail(procId, ex.Message);
    throw;
}
```

- `Finish` (Complete/Fail/Cancel) is **idempotent** — calling Complete after Fail is a safe no-op, so
  the "Complete at the end + Fail in catch/branches" pattern works.
- For cancellable ops: `Start(type, title, cancellable: true)` then honor `GetToken(procId)`.
- `ProcessType`/`ProcessStatus` are camelCase on the wire (see `enum-serialization.md`).

## Wired producers (examples to copy)

| Op | Service |
|----|---------|
| mod load/extract | `ModLifecycleService.LoadInternalAsync` |
| preset apply / unload-all | `ModPresetService` |
| cache cleanup | `ModCacheService.CleanCacheAsync` |
| archive update (compress) | `ModArchiveService.CompressCacheToArchiveAsync` |
| batch category update | `ModMetadataService.BatchUpdateCategoryAsync` |
| mod-id migration | `ModIdMigrationService.MigrateAsync` |
| package export/import | `ModPackageService` |

NOT yet on the registry (own in-screen progress only): mod **analysis** (complex pause/resume state
machine — natural first candidate for durable/resumable jobs) and **file-cleanup scan**.

## IPC + frontend
- IPC: `SystemFacade` `GET_PROCESSES` / `CANCEL_PROCESS` / `CLEAR_COMPLETED_PROCESSES`;
  `systemService` mirrors these.
- `processStore` + `processBridge` (init in `App.tsx`) hold the snapshot; `ActivityPanel` renders it.
- DEV: `window.__processStore` is exposed for pure-UI Chrome testing (see desktop-app-testing.md).
