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
- **`Pump`**: while `_running.Count < _max && dequeue-live` → mark running (a linked CTS) → `RunWorker`.
  `RunWorker` = `Task.Run(() => handler.ProcessAsync(id, ct))` OFF the loop thread (concurrent, bounded);
  its `finally` posts `Finished(id)` back to the mailbox. **Work runs off-thread; results re-enter as
  messages** — the loop never blocks on a job. Default `_max=5` (compression is CPU-bound).
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

**Remote imports are a SECOND handler on the SAME actor (P2, DONE 2026-07-14).**
`RemoteImportWorkflowHandler` (JobType `"REMOTE_IMPORT"`, in `Modules/Workflow/Handlers/`) —
`StartRemoteImportAsync(RemoteImportJob)` creates a Pending REMOTE_IMPORT `WorkflowInfo` (context =
the serialized `RemoteImportJob`) + enqueues (Confirmed tier — a remote download is user-committed, no
preview). `ProcessAsync` runs ONE leg = the WHOLE download+import via
`IRemoteImportService.RunImportAsync(job, ct)` (the old `StartDownloadImport` Task.Run body, extracted
verbatim + made awaitable; its ProcessRegistry token is LINKED with the actor ct so either queue-cancel
or the Activity-panel cancel stops it). Completed → delete the queue row; Failed → mark Failed. Crash-
resumable (the row re-runs the download). `RemoteFacade.DOWNLOAD_IMPORT` now enqueues a workflow
(returns `{started, workflowId}`) instead of `RemoteImportService` firing its own unbounded Task.Run.
Tests: `RemoteImportWorkflowHandlerTests`. **P3 TODO**: a unified queue UI showing both types.

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

- `ImportQueueActorTests` (8) — bounded concurrency (peak ≤ max), pull-next-on-completion, priority
  admission, cancel queued (drops) / running (signals token), no slot leak on a throwing handler,
  re-enqueue-runs-again, unknown-type-doesn't-hang. Drive a FakeHandler whose per-job gate the test
  releases; poll with `WaitUntil`. These lock what `WorkflowConcurrencyManagerTests` guaranteed.
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
