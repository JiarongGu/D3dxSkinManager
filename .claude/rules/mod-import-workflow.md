# Mod-import workflow — queue priority, crash-resume, temp cleanup

DB-backed import workflows (`Modules/Workflow`): each import is a row (`WorkflowInfo`, status
Pending/Processing/WaitingForInput/…) driven by `ModImportWorkflowHandler` through steps
ExtractMetadata → CompressFolder → (WaitingForInput = preview) → **confirm** → ImportMod. "Confirm" =
`UPDATE_WORKFLOW_CONTEXT` (merge user metadata) then `CONTINUE_WORKFLOW` (step→ImportMod, status→Pending).
Parallelism is bounded by `IWorkflowConcurrencyManager` (default 5).

## Priority admission (SHIPPED 2026-07-11)
`WorkflowConcurrencyManager` was a bare `SemaphoreSlim` — a freed slot went to an ARBITRARY waiter
(not even FIFO), so a just-confirmed import could sit behind older unconfirmed previews. It's now a
**priority admission gate**: when a slot frees, the highest-`WorkflowPriority` queued waiter wins —
**confirmed** (ImportMod step) first, then **higher Progress**, then **earlier CreatedAt**.
- `TryAcquireSlotAsync(id, WorkflowPriority, ct)` — callers (`ModImportWorkflowHandler` Start/Continue/
  Resume) pass `BuildPriority(workflow, context)` = `(Step==ImportMod, Progress, CreatedAt)`.
- Cancellation-while-queued still throws + never leaks a slot; a cancelled waiter is skipped on release
  but the next waiter is still admitted. Tests: `WorkflowConcurrencyManagerTests` (ordering D>C>B>A,
  cancel, no-leak, skip-cancelled).

## Crash / close resume (FIXED 2026-07-11)
`WorkflowResumeService.ResumeAllWorkflowsAsync` existed but was **dead code** (never called); the only
resume was a once-per-mount React trigger (`ModImportWorkflowScreen`), so imports stayed stuck if the
user never opened that screen, and a crash leaves rows as `Processing`. Now it's wired **backend-side on
profile init** — `ProfileServiceRouter.CreateProfileServices`, after migrations + plugin start,
FIRE-AND-FORGET + isolated (never blocks/​fails profile open). `ResumeFromCurrentStepAsync` accepts
Pending AND Processing (so crashed `Processing` rows resume). **Double-run guard**: it skips a workflow
already active in-process (`_cancellationTokens.ContainsKey`), so the backend resume + the frontend
screen-mount resume can't double-process the same import. (No pause-on-close needed — resume handles
`Processing` directly.)

## Temp cleanup on crash (FIXED 2026-07-11)
No startup sweep of `{profile}/temp` existed → crash leftovers leaked (only the manual cleanup tool
removed them). `WorkflowResumeService.CleanupOrphanedImportTempAsync` (called first inside
`ResumeAllWorkflowsAsync`, i.e. on profile init) sweeps:
- `*.mic` (import compress temp, named `{workflowId}.mic`) **only when no ACTIVE workflow owns it** —
  an active workflow's `.mic` is KEPT so its resume still finds the compressed archive.
- `*.auc` (archive-update temp) — transient, none legitimately active at startup.
- `remote-*` dirs (RemoteImportService staging — fire-and-forget, never DB-resumable) — always orphaned.
Selection is the pure, unit-tested `SelectOrphanTempEntries(entries, activeWorkflowIds)`; the IO wrapper
is thin. Order matters: **sweep before resume** (keeps active `.mic`, drops orphans). Tests:
`WorkflowResumeServiceTests` (selector + real-temp-dir integration with a mocked repo).

Temp-name patterns live in `TempFileConstants` (`.mic`/`.auc`/`_temp_reorder`/`.tmp` atomic). See
`use-project-paths.md` (all working files under `{profile}/temp`), `background-task-tracking.md`
(fire-and-forget long ops), `filesystem-operation-serialization.md` (planner).
