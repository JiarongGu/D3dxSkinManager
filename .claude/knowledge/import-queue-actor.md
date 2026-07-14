# Import queue = an internal ACTOR (mailbox + single loop), not per-item Task.Run

**All mod imports flow through ONE `IImportQueueActor` per profile — a `System.Threading.Channels`
mailbox drained by a single consumer loop that owns the queue state lock-free.** Producers only
`Enqueue`/`Cancel`; they never spawn the work. This REPLACED `WorkflowConcurrencyManager` (each import
spawned its own `Task.Run` that self-awaited a `SemaphoreSlim` — the model to avoid).

## Why

`WorkflowConcurrencyManager` was "Task.Run management": `ModImportWorkflowHandler` triplicated a
`new CancellationTokenSource()` + `_ = Task.Run(async () => { await TryAcquireSlotAsync(...); ...
ProcessStepAsync ...; finally ReleaseSlot })` across Start/Continue/Resume. Each item self-scheduled
against a semaphore — no single owner of the queue, locks everywhere, easy to leak a slot. The user
asked for "a proper queue + process, more like an actor, but internal". The codebase's proven queue
(`FileOperationPlanner`) is an event-driven dispatcher but still lock-based; the actor goes further —
one thread owns the state, so there are **no locks at all**.

## The shape (`Modules/Workflow/Services/ImportQueueActor.cs`)

- **Mailbox**: `Channel.CreateUnbounded<Msg>(SingleReader=true)`. `Enqueue/Cancel/SetMax` just
  `_mailbox.Writer.TryWrite(msg)` — thread-safe, non-blocking, callable from any thread.
- **One consumer loop**: `await foreach (msg in reader.ReadAllAsync(stop))` → `switch` → mutate state →
  `Pump()`. Because ONE thread touches `_pending` (`PriorityQueue<id,WorkflowPriority>`), `_running`
  (`Dictionary<id,CTS>`), `_pendingMeta`, `_cancelledPending`, `_reEnqueueAfterFinish` — **no locks**.
- **Messages**: `Enqueue(id,type,prio)` · `Finished(id)` · `Cancel(id)` · `SetMax(n)`.
- **`Pump`**: per-LANE — `while (lane.Running < lane.Max && dequeue-live(lane))` → mark running (a linked
  CTS + the lane) → `RunWorker`. `RunWorker` = `Task.Run(() => handler.ProcessAsync(id, ct))` OFF the loop
  thread (concurrent, bounded); its `finally` posts `Finished(id)` back to the mailbox. **Work runs
  off-thread; results re-enter as messages** — the loop never blocks on a job.
- **TWO LANES with independent caps (2026-07-14):** a `download` lane (`REMOTE_DOWNLOAD`, network-bound,
  `MaxDownloadConcurrency` default 4) and an `import` lane (everything else — `MOD_IMPORT` +
  `REMOTE_IMPORT`, CPU-bound compress, `MaxImportConcurrency` default 5). Each lane is a `{ PriorityQueue,
  Meta, Cancelled, Max }`; `_running` is global `id → (CTS, Lane)` so `Finished` decrements the right lane.
  A full download lane NEVER steals import slots (and vice-versa) — that's the whole point: a finished
  download waits for a compress slot instead of one shared `_max` coupling network + CPU. `LaneFor(type)`
  routes by job type (download types are a ctor set, default `{"REMOTE_DOWNLOAD"}`). Caps come live from
  `GlobalSettings.MaxParallelDownloads` / `MaxParallelImports` (wired in `WorkflowServiceExtensions`, updated
  on `GLOBAL_SETTINGS_CHANGED`). A job changes lane via the same `_reEnqueueAfterFinish` path (the stashed
  TYPE decides the new lane) — so the download→import hand-off is just a cross-lane re-enqueue.
- **Priority** reuses `WorkflowPriority`(Confirmed, Progress, CreatedAtUtc) via `WorkflowPriorityComparer`
  (confirmed → higher-progress → earlier). The **durable queue is the `WorkflowInfo` DB rows**; the actor
  is the in-memory scheduler over them.

## Typed handlers (`IImportJobHandler`)

`{ string JobType; Task<JobOutcome> ProcessAsync(jobId, ct); }` — the actor dispatches a job to the
handler whose `JobType` matches `WorkflowInfo.Type`. `ModImportWorkflowHandler` implements it (JobType
`"MOD_IMPORT"`); its `ProcessAsync` runs ONE leg: set Processing → `ProcessStepAsync` (which chains the
current step to its next resting point) → map the resting status to a `JobOutcome`. `Start/Continue/
Resume` shrank to: create/update the Pending row + `_queue.Enqueue(id, "MOD_IMPORT", BuildPriority)`.
`Cancel/Pause` → `_queue.Cancel(id)`.

**Remote imports are TWO-STAGE across the two lanes (2026-07-14).** A REMOTE_IMPORT is a DOWNLOAD leg
(download lane) then an IMPORT leg (import lane), so a finished download WAITS for a compress slot:
- The **row** is one `WorkflowInfo` (Type `REMOTE_IMPORT`, context = `RemoteImportWorkflowContext`
  `{ Job, Stage, Download }`). The **enqueue type** encodes the stage (NOT the row type): stage 1 enqueues
  `"REMOTE_DOWNLOAD"` (→ download lane, `RemoteDownloadHandler`), stage 2 `"REMOTE_IMPORT"` (→ import lane,
  `RemoteImportWorkflowHandler`). `RemoteDownloadHandler` is an `IImportJobHandler` ONLY (a lane dispatch
  key, not a row type / `IWorkflowHandler`).
- `StartRemoteImportAsync` creates the Pending row (Stage=Download) + enqueues `REMOTE_DOWNLOAD` (Confirmed
  tier — user-committed, no preview). `RemoteDownloadHandler.ProcessAsync` runs
  `IRemoteImportService.DownloadStageAsync(job, ct)` (resolve+save-to-drive+download → `RemoteDownloadResult`),
  persists it on the context (Stage=Import, row → Pending = "downloaded, queued for import"), and
  re-enqueues `REMOTE_IMPORT` (Progress 50 → finish downloaded items first). `RemoteImportWorkflowHandler.
  ProcessAsync` runs `ImportStageAsync(job, download, ct)` (extract+recompress+import+previews) → Completed
  deletes the row, Failed marks it. Each stage owns its OWN ProcessRegistry entry (Download / Import), token
  LINKED with the actor ct.
- `RemoteImportService.RunImportAsync` was SPLIT (verbatim at the bytes-on-disk boundary) into
  `DownloadStageAsync` (throws on cancel/fail, cleans its staging) + `ImportStageAsync` (returns a
  JobOutcome, cleans staging in finally, keeps the managed archive on failure) + `DiscardDownloadAsync`
  (cancel-between-stages cleanup).
- **Crash-resume always restarts from DOWNLOAD** ({profile}/temp staging is swept on startup): resume
  clears `Download` + re-enqueues `REMOTE_DOWNLOAD`. A stray IMPORT leg with no `Download` result yields +
  re-queues the download. Back-compat: an OLD bare-`RemoteImportJob` context deserializes as a fresh
  Download-stage context (`RemoteImportWorkflowHandler.DeserializeContext`).
- Frontend: `ModImportWorkflowTable` reads `context.job.detail.title` (legacy `context.detail.title`
  fallback) + shows "Downloaded · queued for import" / "Downloading" / "Importing" by `context.download`
  presence; `TaskDetailScreen` reads remote fields under `context.job`.
Tests: `RemoteImportWorkflowHandlerTests` (import leg + cancel-discards-staging), `RemoteDownloadHandlerTests`
(download leg persists + hands off, fail/cancel don't hand off), `ImportQueueActorTests` (lane independence
+ cross-lane re-enqueue + live cap).

**Unified queue UI (P3, DONE 2026-07-14).** `useWorkflowQueue.refresh` fetches BOTH `MOD_IMPORT` +
`REMOTE_IMPORT` (its event subscriptions already added either type by payload — only the initial load
missed remote). `ModImportWorkflowTable` derives a REMOTE_IMPORT row's name from the `RemoteImportJob`
context (`detail.title`, not `context.name`), shows a cloud icon + a **`workflow.queue.remoteBadge`**
("Remote"/"远程") tag, and skips the folder-path tooltip/fileCount. Remote rows have no
preview/confirm (WaitingForInput actions auto-skip by status); live byte-progress still shows in the
Activity panel (ProcessRegistry). The whole "shared import queue" ask (P1+P2+P3) is complete.

## Non-obvious rules (learned building it)

- **DI CYCLE**: the handler depends on the actor (enqueue) and the actor dispatches to the handler.
  Resolving `IEnumerable<IImportJobHandler>` in the actor ctor would construct the handler mid-construction
  → cycle. Break it by injecting `Func<IEnumerable<IImportJobHandler>>` and resolving **lazily on the loop
  thread** (first `Pump`). DI: `AddSingleton<Func<IEnumerable<IImportJobHandler>>>(sp => sp.GetServices<IImportJobHandler>)`.
- **Yield = the preview pause**: a leg that reaches `WaitingForInput` returns `JobOutcome.Yielded`; the
  actor frees its slot, the row stays WaitingForInput, and `CONTINUE_WORKFLOW` posts a FRESH `Enqueue`
  (now confirmed → jumps ahead). Preserves extract→compress→**pause**→confirm→import exactly.
- **Re-enqueue-while-running race**: a confirm can arrive before the yield's `Finished` is processed.
  `OnEnqueue` of a still-`_running` job stashes it in `_reEnqueueAfterFinish` and re-enqueues on `Finished`
  — so the confirm is never swallowed by the running-dedup.
- **No manual double-run guard**: the actor dedups an `Enqueue` for an already-queued/running job, so the
  old `_cancellationTokens.ContainsKey` guard in `ResumeFromCurrentStepAsync` is gone (backend profile-init
  resume + frontend screen-mount resume can both fire; the terminal/WaitingForInput status checks reject
  anything already finished).
- **Cancel**: running → signal its linked CTS (the worker's `Finished` frees the slot); queued → lazily
  dropped at dequeue via `_cancelledPending`.
- Lifecycle: profile-scoped singleton; the loop starts in the ctor, `DisposeAsync` cancels + drains.
- **IPC gotcha (test-time)**: `GET_WORKFLOW`/`GET_WORKFLOWS_BY_TYPE`/`CONTINUE_WORKFLOW`/`DELETE_WORKFLOW`
  take the payload as a **raw string** (`request.Payload?.ToString()`), NOT an object. `CREATE_WORKFLOW`
  takes an object (`{type, initialData}`). Passing an object to the raw-string ones silently returns
  null/[] (a false negative when driving imports over `cdp ipc`).

## Tests

- `ImportQueueActorTests` (11) — bounded concurrency (peak ≤ max), pull-next-on-completion, priority
  admission, cancel queued (drops) / running (signals token), no slot leak on a throwing handler,
  re-enqueue-runs-again, unknown-type-doesn't-hang, **+ two-lane: independent caps (a full download lane
  doesn't steal import slots), cross-lane download→import re-enqueue runs, live download-cap raise admits
  more**. Drive a FakeHandler whose per-job gate the test releases; poll with `WaitUntil`. These lock what
  `WorkflowConcurrencyManagerTests` guaranteed.
- `ModImportWorkflowHandlerTests` — now tests the LEG via `ProcessAsync` directly + verifies enqueue.
- E2E validated (2026-07-14): dummy folder → CREATE_WORKFLOW → actor ran extract+compress → WaitingForInput
  → CONTINUE → import → Completed + mod imported.

## Related

- [background-task-tracking.md](background-task-tracking.md) — `RunTrackedAsync` + the fire-and-forget
  ProcessRegistry model (the actor's workers still report progress via the registry).
- [mod-import-workflow.md](mod-import-workflow.md) — the `WorkflowInfo` rows / steps / crash-resume the
  actor schedules over.
- [filesystem-operation-serialization.md](filesystem-operation-serialization.md) — `FileOperationPlanner`,
  the codebase's other (lock-based, event-driven) queue-dispatcher.
