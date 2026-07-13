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
- **`Report` is cheap to call per-item (2026-07-10):** report-driven snapshot emissions are THROTTLED
  (≤1 per 100ms + a trailing emit so the last value always lands) because the IPC batcher queues
  events WITHOUT coalescing — a tight loop used to ship one full snapshot per item. Lifecycle
  transitions still emit immediately. Don't hand-roll your own report throttling in producers.
- **Titles/details are LOCALIZED via keys (2026-07-05).** `Start(..., titleKey: "process.x", titleArg: name)`
  and `Report(..., detailKey: "process.stage.y")` — the frontend renders `t(key, {arg})` via
  `processTitle()/processDetail()` (processStore); the plain `title`/`detail` strings stay as the
  English fallback (+ logs). Add BOTH `process.*` keys (en+cn) for every new producer — a keyless
  Start shows raw English in a non-English UI (the bug this fixed).
- **`RunTrackedAsync` (`ProcessRegistryExtensions`, Core) wraps the EXACT fire-and-forget shape** —
  `Start` + `Task.Run` + try/`Complete`/catch-`OperationCanceledException`→`Cancel`/catch-ex→`Fail`(+`onError`).
  Use it ONLY when a producer matches that shape 1:1 (the `work(procId, ct)` delegate does its own
  try/`finally` cleanup). Adopted in `XxmiService.StartInstallerDownload`; tested by
  `ProcessRegistryExtensionsTests`. **The remaining long-op services were assessed (2026-07-14) as NOT
  clean fits — leave them hand-rolled:** `ModAnalysisService` (field `_currentProcId`, custom
  cancel-vs-complete + resumable + `finally`), `ModLifecycleService` (`Start` outside the `Task.Run`,
  two `Task.Run` blocks, multiple `Fail`s), `PluginInstallService.StartPackInstall` (`Complete` inside
  both branches), `RemoteImportService.StartImportAsync` (right shape but a ~165-line body on the
  UNTESTED critical MEGA/Quark/download import path — cosmetic dedup at silent-breakage risk, so
  `risky-change-tests-first` says no). Don't blanket-apply; a green build ≠ a correct background op.
- `ProcessType`/`ProcessStatus` are camelCase on the wire (see `enum-serialization.md`).
- **The registry is PURELY IN-MEMORY (2026-07-10) — {data}/process-state.json is GONE.** Finished
  history was purged at startup anyway; the only cross-restart state that matters is a
  crash-interrupted RESUMABLE op, whose real checkpoint lives in the PROFILE DB (e.g. an analysis
  session left "running"). The owning profile-scoped service announces those on profile init via
  **`RegisterInterrupted(type, title, resumePayload, titleKey?, titleArg?, profileId?, startedAtUtc?)`**
  (deduped by type+resumePayload — profile switches re-announce). `ProcessInfo.ProfileId` rides on
  `PROCESS_RESUME_REQUESTED` and the AppStatusBar dispatcher resumes against the OWNING profile,
  not the selected one. Making a new op resumable = persist your checkpoint in the profile DB +
  announce it on service construction (copy `ModAnalysisService.AnnounceInterruptedSessionsAsync`).
  A startup cleanup step deletes the legacy json once.

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
| mod delete / batch delete (fire-and-forget) | `ModDeletionService` — single `DeleteAsync` = one ModDelete process; `BatchDeleteAsync` = ONE cancellable process with per-item progress. IPC `DELETE`/`BATCH_DELETE` ack immediately; failure emits `REFRESHED` to roll back the frontend's optimistic row removal |
| mod analysis (status bar + Activity) | `ModAnalysisService` — `Start`/`Report`(per-mod %)/`Complete`/`Fail`. **Resumable**: `resumePayload` = sessionId; the `AppStatusBar` resume dispatcher re-invokes `resumeAnalysis(profileId, sessionId)` on `PROCESS_RESUME_REQUESTED` (type `analysis`). |

| file-cleanup scan (fire-and-forget) | `FileCleanupService.ScanAllOrphansAsync` (ProcessType.FileScan, per-category progress). IPC `SCAN_ALL_ORPHANS` acks immediately; results via `TOOL/ORPHAN_SCAN_COMPLETE` `{ results, error? }` (2026-07-10 — the awaited scan froze the UI) |
| file-cleanup clean (fire-and-forget) | `FileCleanupService.CleanOrphansAsync` (ProcessType.Cleanup, per-item progress, titleKey `process.fileClean`). IPC `CLEAN_ORPHANS` acks immediately; the CleanupResult arrives via `TOOL/ORPHAN_CLEAN_COMPLETE` (each mounted tab filters by `category`) |

## IPC + frontend
- IPC: `SystemFacade` `GET_PROCESSES` / `CANCEL_PROCESS` / `CLEAR_COMPLETED_PROCESSES`;
  `systemService` mirrors these.
- `processStore` + `processBridge` (init in `App.tsx`) hold the snapshot; `ActivityPanel` renders it.
- DEV: `window.__processStore` is exposed for pure-UI Chrome testing (see desktop-app-testing.md).
