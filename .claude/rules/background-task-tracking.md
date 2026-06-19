# Long-running process monitoring (ProcessRegistry → status bar + Activity panel)

When adding a background operation that takes >1s, register it with the **backend** `ProcessRegistry`
so it shows in the status bar + the download-manager-style **Activity panel**.

## RULE: long ops are FIRE-AND-FORGET — never block the IPC (it times out + freezes the UX)

The frontend bridge **times out** a pending IPC (`bridgeService` → "Request timeout"). So a slow op
(merge, fix-run, migration, package, analysis, anything that extracts/copies/compresses/scans) **must
NOT be `await`ed inside its facade handler** — that blocks the caller until timeout and **blocks the
user from using the app**. Instead the handler **kicks off the work in the background and returns
immediately**; progress + result flow through the **ProcessRegistry → events**, and the user keeps
working. Pattern (mirrors `ToolFacade` fix-runner / migration, `ModFacade.MergeModsAsync`):

```csharp
private Task<object?> StartLongOpAsync(IpcRequest request)
{
    var args = /* parse + validate synchronously so bad input errors right away */;
    _ = Task.Run(async () =>      // fire-and-forget — DO NOT await in the handler
    {
        try { await _service.DoItAsync(args); }   // the service Starts/Reports/Completes/Fails on the registry
        catch (Exception ex) { _logger.Error($"... {ex.Message}", ModuleName, ex); } // swallow: avoid unobserved-task crash
    });
    return Task.FromResult<object?>(new { started = true });   // immediate ack
}
```

Frontend: the trigger calls the IPC, shows a "started in background — see Activity" toast, and
**closes/returns immediately** (no awaiting the result). The created/changed data arrives via the normal
event (`MOD_LIST_UPDATED`, etc.) which the providers already refresh on. The service reports progress via
`_processRegistry.Report(procId, percent, stage)` so the Activity panel shows stages, and `Fail` surfaces
errors there. **Never** make a long op a synchronous request/response.

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
| mod-merge (fire-and-forget) | `ModMergeService.MergeAsync` (IPC `MERGE_MODS` returns immediately) |
| mod analysis (status bar + Activity) | `ModAnalysisService` — `Start`/`Report`(per-mod %)/`Complete`/`Fail`. **Resumable**: `resumePayload` = sessionId; the `AppStatusBar` resume dispatcher re-invokes `resumeAnalysis(profileId, sessionId)` on `PROCESS_RESUME_REQUESTED` (type `analysis`). |

NOT yet on the registry (own in-screen progress only): **file-cleanup scan**.

## IPC + frontend
- IPC: `SystemFacade` `GET_PROCESSES` / `CANCEL_PROCESS` / `CLEAR_COMPLETED_PROCESSES`;
  `systemService` mirrors these.
- `processStore` + `processBridge` (init in `App.tsx`) hold the snapshot; `ActivityPanel` renders it.
- DEV: `window.__processStore` is exposed for pure-UI Chrome testing (see desktop-app-testing.md).
