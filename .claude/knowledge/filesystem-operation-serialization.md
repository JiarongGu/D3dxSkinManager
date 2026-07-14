# File System Operation Serialization (CRITICAL)

All mod data lives on disk in three directory families per profile:
- Archives: `{Mods}/{id}` (no extension)
- Active cache: `{CacheMods}/{id}`
- Disabled cache: `{CacheMods}/DISABLED-{id}`
- Previews: `{Previews}/{id}`

Concurrent operations on these paths corrupt state. Two layers protect them. **Both must be used — bypassing either reintroduces conflicts.**

## Layer 1 — `IFileOperationPlanner` (raw FS serialization)

`Modules/Context/Services/FileOperationPlanner.cs` is a **path-overlap dispatcher** (rewritten
2026-07-11 from a single-worker sequential queue). It is the ONLY place allowed to mutate mod data on
disk. Model:
- Ops whose **physical paths OVERLAP** (equal, or one an ancestor of the other, across
  Source/Target/Temp) run **strictly one-at-a-time in submission order** — the corruption-safety
  guarantee (unchanged).
- Ops on **DISJOINT paths** (e.g. different mods) run **in PARALLEL**, bounded by a small cap
  (`maxConcurrency`, default `clamp(cores-1, 1, 4)` — disk-IO bound; ctor takes an override so tests
  are core-count-independent). This is the win: batch fix/delete/preset-apply/analysis across many
  mods no longer serialize every compress/extract (was sum-of-all → now ~max, capped).
- Dispatch is event-driven (on submit + on completion) — no polling worker. `DispatchLocked` starts a
  pending op only if its paths overlap NO in-flight op AND no earlier still-pending op (per-resource
  FIFO); work runs on `Task.Run` so nothing executes while the lock is held. Dedup (identical
  type+source+target already pending/in-flight → eager Ok), retry, and idempotency are unchanged.

It does **not** call `Directory.*`/`File.*` directly — it goes through `IFileSystem`
(`Modules/Core/Services/FileSystem.cs`), a thin seam whose real impl (`SystemFileSystem`) forwards
to `System.IO`. The seam exists so concurrency tests can drive the planner with
`InMemoryFileSystem` (`D3dxSkinManager.Tests/TestHelpers/`), which simulates latency, injects transient
`IOException` locks, and records BOTH `MaxConcurrentMutations` (global peak — now >1, proves
parallelism) and **`MaxConcurrentSamePath` (must stay 1 — proves overlapping-path ops never run at
once)**. Layer 1 only needs PHYSICAL path-overlap safety; logical per-mod atomicity is Layer 2's job
(`IModOperationQueue`), so parallelizing disjoint paths is safe. Tests:
`FileOperationPlannerConcurrencyTests` (disjoint-parallel, same-path/ancestor serialized, cap
respected, mixed workload, compress-once-under-lock, transient/persistent lock).

**RULE: never call raw `Directory.*` / `File.*` mutators on a mod archive/cache/preview path.**
Submit a `FileSystemOperation` to the planner instead. Read-only calls (`Directory.Exists`,
`GetDirectories`, `GetLastWriteTime`) are fine outside the planner.

Compliant services: `ModArchiveService`, `ModCacheService` (`EnableCacheAsync`, `DisableCacheAsync`,
`DeleteCacheAsync`, `CleanupOldDisabledCachesAsync`, `CleanCacheAsync`).

**Fast single-file archive patch** (`FileSystemOperationType.UpdateFileInArchive` →
`ModArchiveService.UpdateFileInArchiveAsync`): for small edits (a keybinding/`.ini` change) patch just
the one entry via SharpSevenZip append (`CompressionMode.Append`, forward-slash entry path) instead of
a full `CompressCacheToArchiveAsync` recompress — ~17× faster (143ms vs ~2.5s). Append REPLACES the
matching entry (proven not-duplicate by `ArchiveHelperUpdateTests`). **Feed the source via a stream WE
own + dispose** (`CompressStreamDictionary` + `StreamWithAttributes`, NOT the file-path
`CompressFileDictionary` — the path overload let SevenZip hold the source `.ini` open past the call, so
the cache file stayed LOCKED after a value edit → the next re-edit/redeploy failed "file in use"; the
older full-recompress had instead HUNG on big mods. Fixed 2026-07-13, guarded by
`ArchiveHelperUpdateTests.UpdateFileInArchive_LeavesArchiveAndSourceUnlocked`). Still planner-serialized
+ per-mod queue-locked. Used by `ModKeybindingService.UpdateKeybindingAsync`, the general config editor
`ModIniService.UpdateEntryAsync`, and **`ModFixService`** (after a fix tool runs).

**Fix-tool persistence is diff-based** (`ModFixService.PersistFixAsync`): snapshot the work dir
(relpath ⇒ length+mtime **+ sha256 for files ≤4MB** — fix scripts can rewrite a file preserving size
AND timestamp, which a pure length+mtime diff misses; hashing covers exactly the small `.ini`/config
files fix tools touch) before the script, diff after, and patch ONLY the changed/added files via
`UpdateFileInArchiveAsync`. Full `CompressCacheToArchiveAsync` is the fallback only when a file was
**deleted** (append can't remove entries) or the changed bytes are **≥50% of the mod**
(`FullRecompressByteFraction`) — most fix tools only rewrite small `.ini` and leave the bulk textures
untouched, so the fast path is the norm. A no-op fix (no file changed) leaves the archive untouched.
**The fix runs IN the retained cache when one exists — active `{id}` OR disabled `DISABLED-{id}`
(via `IModCacheService.GetCachePath`)** — so the working copy and the archive stay in sync; fixing
only a temp extract left a disabled cache stale and the fix "didn't apply" when that cache was
re-enabled (user report 2026-07-05, fixed). Only a mod with no cache at all stages to temp.

## Layer 2 — `IModOperationQueue` (logical serialization)

`Modules/Mod/Services/ModOperationQueue.cs` serializes higher-level read-modify-write flows:
- `EnqueueAsync(modId, op)` — one operation per mod id at a time.
- `EnqueueCategoryOperationAsync(category, op)` — one load/unload per category at a time
  (loading mod B unloads mod A in the same category, so category state is shared).

The planner stops two raw FS calls colliding, but it does NOT make a multi-step flow atomic
(e.g. "enumerate loaded mods → unload them → enable self"). That needs the queue.

The category lock now lives **inside `ModLifecycleService.LoadAsync`/`UnloadAsync`** (not only in
`ModFacade`), so EVERY entry point — facade, preset, metadata — is serialized per category in one
place. Lock order is always mod-lock (facade) → category-lock (lifecycle); a thread holding a
category lock never waits on a mod lock, so no deadlock.

## Known bypass risks (audit these on every concurrency change)

| Path | Risk | Status |
|------|------|--------|
| `ModCacheService.CleanCacheAsync` | raw `Directory.Delete` bypassed the planner | FIXED 2026-06-17 — submits DeleteDirectory ops |
| `ModLifecycleService` Load/Unload category resolution | category lock not wired in → two loads of different mods in the same category could both end loaded | FIXED 2026-06-17 — `EnqueueCategoryOperationAsync` wrapped inside lifecycle |
| `ModPresetService` / `ModMetadataService` calling lifecycle directly | bypassed the facade queue | FIXED 2026-06-17 — covered transitively by the lifecycle-level category lock |
| `ModOperationQueue` semaphore cleanup (`if CurrentCount==1 TryRemove`) | check-then-remove race → two threads get different semaphores for the same key | FIXED 2026-06-17 — ref-counted `LockHandle` removed only at refcount 0 under a bookkeeping lock |
| `ModLifecycleService` fire-and-forget `CleanupOldDisabledCachesAsync` (`Task.Run`) | runs outside any lock; planner keeps it raw-FS safe but it is logically unsynchronized | LOW — acceptable |
| `ModDeletionService.DeletePreviewFolderAsync` | raw `Directory.Delete` on the preview dir while cache/archive deletion used the planner | FIXED 2026-06-19 — submits a `DeleteDirectory` op (test: `ModDeletionServiceTests`) |
| `ModDeletionService.BatchDeleteAsync` | per-id loop called `DeleteAsync` WITHOUT the per-mod queue lock (single-delete path was queued at the facade) → batch could race a concurrent load/unload/fix of the same mod | FIXED 2026-06-19 — wraps each delete in `IModOperationQueue.EnqueueAsync` (`DeleteAsync` stays non-enqueuing → no double-lock) (test: `ModDeletionServiceTests`) |
| `ModIdMigrationService` raw `Directory.Move`/`File.Move` on archive/cache/preview | renames mod IDs (hash→GUID) outside the planner | REVIEWED 2026-06-19 — LOW/acceptable: one-shot user-initiated bulk op, not concurrent with normal modding, has its own `renamedFiles` rollback. Route through the planner only if it ever runs alongside live ops. |
| `ImageService` raw preview-image ops (CreateDirectory/Copy/Delete/Move, set-thumbnail reorder) | preview-folder mutations outside the planner | REVIEWED 2026-06-19 — LOW/acceptable: small, low-contention, user-driven image ops on `previews/{id}` (separate from the hot cache/archive paths); set-thumbnail reorder has its own restore-on-failure. `ModPackageService` package-import preview copy is the same class (its export writes only to the external package dir). |
| `ModImportService.ImportAsync` copies the archive BEFORE the DB row exists | a `CreateAsync` failure left the copied archive (+ auto-imported previews) as invisible orphans until a cleanup-tool scan | FIXED 2026-07-05 — best-effort `RollbackImportAsync` (planner-routed `DeleteArchiveAsync` + preview deletes) on any post-copy failure; a rollback failure never masks the original error. Covers the workflow import too (it funnels through `ImportAsync`). Tests: `ModImportServiceTests`. |
| `ModOptimizeService` raw `File.Delete`/`File.WriteAllLines` inside the mod cache | dedup deletes + `filename =` rewrites bypass the planner | REVIEWED 2026-07-05 — LOW/acceptable: the WHOLE optimize (scan→rewrite→delete→recompress) holds the per-mod `IModOperationQueue` lock, so no normal mod op interleaves; per-file planner ops would add hundreds of queue items with no extra safety. Only unsynchronized collider = the accepted-LOW `CleanupOldDisabledCachesAsync` row above. |
| `ModIniService`/`ModKeybindingService` `File.WriteAllLines` on a cache `.ini` | config/keybinding edits write the cache file raw | ACCEPTED BY DESIGN — edit + archive patch run under the per-mod queue lock (see the fast single-file archive patch above); routing one small line rewrite through the planner buys nothing. |
| `FileCleanupService` raw deletes of scanned orphans | orphan cleanup deletes paths outside the planner | REVIEWED 2026-07-05 — LOW/acceptable: targets have NO DB row (nothing schedules planner ops on them; GUID reuse impossible), deletion is user-initiated from a scan snapshot, per-item try/catch surfaces locked files as failures. The mid-import "archive exists, row not yet" TOCTOU window is seconds and further narrowed by the `ImportAsync` rollback row above. |
| `ModMergeService` staging (`{profile}/temp/merge-*`) | staging leak on merge failure | REVIEWED 2026-07-05 — already correct: all mutations land in the staging dir, `finally { TryDeleteDir(staging) }` cleans success AND failure paths; sources are read via planner-routed extract. |

## External-process locks (the game / 3DMigoto holding files)

The planner serializes OUR operations; it cannot stop an **external** process from holding a file
open during a slow compress/extract. That is handled by:
- **Retry with backoff** (`RetryOperationAsync`: 3 attempts, 500ms × attempt). Transient locks that
  release within the budget succeed on retry.
- **Compress runs once**: `ExecuteCompressArchiveAsync` compresses to a temp file in Phase 1, then
  only the cheap delete+move replace is retried (`ReplaceFileWithRetryAsync`). A locked target does
  NOT trigger re-compression.
- **Clear failure**: a lock held past the retry budget returns `Fail` with "...may be in use by
  another process" (surfaced as `MOD_FOLDER_IN_USE`), never a hang.

If users hit persistent failures while the game is running, the lever is the retry budget
(`MAX_RETRY_ATTEMPTS` / `RETRY_DELAY_MS`) — currently tuned short to keep the UI responsive.

### Self-inflicted extraction locks + runaway carve (`ArchiveHelper.ExtractArchive`, fixed 2026-07-14)

Extracting a **self-extracting `.exe`** (a real archive with an executable stub — e.g. a 3DMigoto
global-fix mod) exposed two bugs, both in the polyglot-carve path:
- **Runaway carve recursion.** `ExtractArchive` falls back to `TryCarveEmbeddedArchive` (find an archive
  signature at a non-zero offset) and recurses on the carved file. An SFX `.exe` — or any file with a
  `PK`/`7z` signature inside its *compressed* data — makes carve find a FALSE signature, extract garbage,
  fail, and carve again forever (seen **15 levels deep**). Fixed with a **`MaxCarveDepth = 3`** cap
  (legit polyglots like huihui's mp4→zip carve ONCE); beyond it, throw the real error instead of carving.
- **Leaked output handle.** SharpSevenZip does NOT release the file it was mid-writing when
  `ExtractArchive` throws mid-extraction, so the next candidate's `Directory.Delete(targetDirectory)`
  fails **"used by another process"** — a SELF-inflicted lock (not the external-process kind above), and
  it PERSISTS (not a transient release delay). Fixed with `CleanTargetDirWithRetry`: retry the clean and
  **`GC.Collect()` + `GC.WaitForPendingFinalizers()`** before each retry so the leaked stream finalizes
  and closes the handle. Validated live: the RabbitFX `.exe` (Baidu/MEGA/Quark serve the same file) now
  imports end-to-end. `ArchiveHelperTests` stay green.
- **SECURITY invariant — EXTRACT-ONLY, NEVER EXECUTE.** A self-extracting `.exe` mod is handled by
  *reading* it as an archive (7z parses the embedded payload); the executable stub is never run.
  Downloaded/imported mod content is NEVER executed anywhere in the download→extract→import pipeline
  (verified: no `Process.Start`/`ShellExecute` in Remote / ArchiveHelper / Mod). Do NOT add a "run the
  installer/SFX" path — always extract.

Coverage: `FileOperationPlannerConcurrencyTests` proves disjoint-path ops run in parallel (bounded by
the cap) while overlapping-path ops (same path + ancestor/descendant) stay serialized, plus a mixed
workload (disjoint-parallel + shared-source-serialized at once), transient-lock retry,
compress-once-under-lock, a slow compress running concurrently with disjoint ops, and persistent-lock
→ in-use error, all via `InMemoryFileSystem`.

## Past incidents

- **2026-06-17**: `CleanCacheAsync` (cache-cleanup tool, `ToolFacade` `CLEAN_CACHE`) deleted cache
  directories with raw `Directory.Delete`, racing the planner worker mid-load/unload → IOException
  / partially-deleted folders. Routed through the planner. Same session: wired the category lock
  into the lifecycle, fixed the semaphore-cleanup race, and added an `IFileSystem` seam +
  in-memory-FS concurrency tests.
