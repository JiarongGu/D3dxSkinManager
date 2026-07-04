# File System Operation Serialization (CRITICAL)

All mod data lives on disk in three directory families per profile:
- Archives: `{Mods}/{id}` (no extension)
- Active cache: `{CacheMods}/{id}`
- Disabled cache: `{CacheMods}/DISABLED-{id}`
- Previews: `{Previews}/{id}`

Concurrent operations on these paths corrupt state. Two layers protect them. **Both must be used — bypassing either reintroduces conflicts.**

## Layer 1 — `IFileOperationPlanner` (raw FS serialization)

`Modules/Context/Services/FileOperationPlanner.cs` is a single-worker queue that executes every raw
move/copy/delete/extract/compress sequentially. It is the ONLY place allowed to mutate mod data on
disk. It does **not** call `Directory.*`/`File.*` directly — it goes through `IFileSystem`
(`Modules/Core/Services/FileSystem.cs`), a thin seam whose real impl (`SystemFileSystem`) forwards
to `System.IO`. The seam exists so concurrency tests can drive the planner with
`InMemoryFileSystem` (`D3dxSkinManager.Tests/TestHelpers/`), which simulates latency, records peak
concurrent mutations (must stay 1), and injects transient `IOException` locks.

**RULE: never call raw `Directory.*` / `File.*` mutators on a mod archive/cache/preview path.**
Submit a `FileSystemOperation` to the planner instead. Read-only calls (`Directory.Exists`,
`GetDirectories`, `GetLastWriteTime`) are fine outside the planner.

Compliant services: `ModArchiveService`, `ModCacheService` (`EnableCacheAsync`, `DisableCacheAsync`,
`DeleteCacheAsync`, `CleanupOldDisabledCachesAsync`, `CleanCacheAsync`).

**Fast single-file archive patch** (`FileSystemOperationType.UpdateFileInArchive` →
`ModArchiveService.UpdateFileInArchiveAsync`): for small edits (a keybinding/`.ini` change) patch just
the one entry via SharpSevenZip append (`CompressionMode.Append`, forward-slash entry path) instead of
a full `CompressCacheToArchiveAsync` recompress — ~17× faster (143ms vs ~2.5s). Append REPLACES the
matching entry (proven not-duplicate by `ArchiveHelperUpdateTests`). Still planner-serialized + per-mod
queue-locked. Used by `ModKeybindingService.UpdateKeybindingAsync`, the general config editor
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
| `ImageService` raw preview-image ops (CreateDirectory/Copy/Delete/Move, set-thumbnail reorder) | preview-folder mutations outside the planner | REVIEWED 2026-06-19 — LOW/acceptable: small, low-contention, user-driven image ops on `previews/{id}` (separate from the hot cache/archive paths); set-thumbnail reorder has its own restore-on-failure. |

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

Coverage: `FileOperationPlannerConcurrencyTests` proves serialization under parallel mixed ops,
transient-lock retry, compress-once-under-lock, slow-op-blocks-concurrent-ops, and persistent-lock
→ in-use error, all via `InMemoryFileSystem`.

## Past incidents

- **2026-06-17**: `CleanCacheAsync` (cache-cleanup tool, `ToolFacade` `CLEAN_CACHE`) deleted cache
  directories with raw `Directory.Delete`, racing the planner worker mid-load/unload → IOException
  / partially-deleted folders. Routed through the planner. Same session: wired the category lock
  into the lifecycle, fixed the semaphore-cleanup race, and added an `IFileSystem` seam +
  in-memory-FS concurrency tests.
