# D3dxSkinManager — Tasks & Roadmap

> Scope: a **game-agnostic** mod manager for 3DMigoto / XXMI-style games (ZZZ/ZZMI, Endfield/EFMI,
> Genshin/GIMI, Star Rail/SRMI, Wuthering Waves/WWMI, Honkai/HIMI, and any future importer following the
> same `Mods/` + `.ini` hash-override convention). Nothing is hard-coded to one game.
> Design principle: **everything customizable** — seed a wished default as editable config, don't bake it in.
>
> **North star:** make managing mods effortless — organize, fix, edit, deploy and launch without leaving
> the app. The app is the **compressed mod library + organize + fix + edit + deploy**; **XXMI is the
> runtime** (injects 3DMigoto, launches the game). We complement XXMI, never reimplement it.

Last consolidated: 2026-07-05. This file holds only PENDING work — shipped features live in git
history + `docs/CHANGELOG.md`. (Shipped this last stretch and verified in code: XXMI settings
integration, per-profile fix-tool settings, single-file archive patch, keybinding editor, `.ini`
config editor, fix diff-persistence, onboarding wizard, mod-card clarity, mod health badge,
namespace-based mod-merge v2 — the GIMI-port v1 `MergeIniBuilder` was superseded and removed.)

---

## How the pieces actually fit (current architecture — read before planning)

- **Work dir / deploy.** `ModWork.Mode` ∈ `internal | external | xxmi`. Mods are stored **compressed** in
  the profile; the active **cache** (`CacheModsDirectory = WorkDirectory/Mods`) is what the importer reads.
  In `xxmi` mode the work dir IS the XXMI importer's folder, so the cache = `<importer>/Mods` (deploy
  target). See `.claude/rules/xxmi-integration.md`.
- **Launch is NOT a tab.** Picking an XXMI importer in **Settings → Mod Work** (`XxmiImporterPicker`) sets
  BOTH the deploy dir and the launcher path in one click; the status-bar **`LaunchButton`** runs it
  (`--nogui --xxmi <IMPORTER>`). The old Launch tab / GameLaunchTab / D3DMigotoTab were removed.
- **Archive writes are serialized + fast.** All mod-data FS mutations go through `IFileOperationPlanner`;
  read-modify-write flows through `IModOperationQueue` (per-mod / per-category). Small edits use the
  **single-file archive patch** (`UpdateFileInArchiveAsync`, ~17× faster than a full recompress). See
  `.claude/rules/filesystem-operation-serialization.md`.
- **`.ini` is the mod's brain.** 3DMigoto drives everything from `.ini` (sections + command lists).
  Authoritative reference: `leotorrez.github.io/modding/docs/*` (scrape via `devtools/dev.mjs research`,
  it's a JS SPA). Interface notes + the mod-merge/namespace contract live in
  `.claude/rules/3dmigoto-ini-interface.md`.
- **Long ops are fire-and-forget** — never `await` a slow op in an IPC handler (bridge times out, UI
  freezes). Kick off in background, report via `ProcessRegistry` → status bar + Activity panel. See
  `.claude/rules/background-task-tracking.md`.

---

## Bugs / UX fixes (user-reported 2026-07-05, code-calibrated)

### B1. Delete blocks the UI — ✅ FIXED 2026-07-05
`DELETE`/`BATCH_DELETE` are now fire-and-forget (ack `{started:true}` in ~3ms, verified e2e).
`ModDeletionService` owns the ProcessRegistry entries (single = one ModDelete process; batch = ONE
cancellable process with per-item progress); failure emits `REFRESHED` to roll back the frontend's
optimistic row removal. Tests: `ModDeletionServiceTests` (7).

### B2. Edit mod value hangs after save
Saving a modified value hangs the UI. Likely the same class as B1 — the save path awaits a slow
archive write (recompress instead of the fast single-file patch, or a queued op behind a long lock).
Reproduce, trace which IPC blocks (`cdp iplog`), route the slow part through the fast patch and/or
fire-and-forget.

### B3. Fix tools not applied properly (change detection + sync back)
`ModFixService.PersistFixAsync` diffs by **length+mtime snapshot** and patches changed files into the
archive — but verify the full loop the user expects: detect **every** updated file via file update
time (not only `.ini`), patch those back into the compressed archive, **and copy them back into the
working/cache mod dir** so the deployed mod matches. Audit staging→archive→cache consistency; add a
regression test with a fix that touches a non-`.ini` file.

### B4. Keybinding: multiple `key=` lines per `[Key*]` + combo editing broken
The `[Key*]` parser (`ModKeybindingService.ParseIniFileAsync`) keeps only the **first** `key =` line —
later `key =` lines in the same section (keyboard + controller share state, per the 3DMigoto key doc)
are lost, and rebinding writes only one. Also the combination-key path in the editor doesn't work when
editing. Support multi-`key=` sections (parse as list, edit each) and fix combo capture on edit
(`KeyCaptureInput` emits combos; verify write-back rewrites the right line).

### B5. XXMI importer pick: no confirm/progress — ✅ FIXED 2026-07-05
Picking an importer used to apply instantly from the dropdown with no summary or feedback. Now the
pick is staged into a ConfirmDialog showing exactly what will be bound (work dir, deploy target,
launcher, `--nogui --xxmi <NAME>` command) with an applying-spinner (async onOk) and a hint that
every value stays manually adjustable in the section. Also fixed: apply now syncs the live
`workDirectory` store value so the section isn't left dirty. Verified in-app, both themes.

### B6. Cleanup tool: ignore dot-folders — ✅ FIXED 2026-07-05
`ScanOrphanedModCachesAsync` skips `.`-prefixed folders; the archive scan skips `.`-prefixed files.
Tests: `FileCleanupServiceTests`.

### B7. Cleanup tool: open-in-explorer broken for archives — ✅ FIXED 2026-07-05
Root cause: the UI guessed file-vs-directory from `name.includes('.')` — mod archives are
extensionless FILES, so they were misclassified and `openDirectory(file)` failed. The scanner now
reports `IsDirectory` on every `OrphanedItem` (backend knows what it scanned) and the UI uses it;
select-in-explorer (`/select`) works for extensionless files. Tests: `FileCleanupServiceTests`.

---

## Next up (features, prioritized)

### 1. Update a preset in place with the current setting
`ModPresetService.UpdateAsync` only renames. Add "overwrite preset with currently-loaded mods"
(IPC + context-menu/action on the preset). Small, high-value.

### 2. Mod-merge — in-game validation (user-side)
Namespace merge v2 is shipped (`NamespaceMergeBuilder` + `ModMergeService`, IPC `MERGE_MODS`).
**Remaining:** a real two-same-character in-game test to confirm the `$\<ns>\swapvar` gating +
OR-condition cycle key (the one unverified assumption — see `3dmigoto-ini-interface.md`). Fallback if
the key misbehaves: `activeOnly` OFF emits no condition.

### 3. Analyzer improvement (better 3DMigoto understanding)
Current analysis flags are heuristic — we have limited grounding in what 3DMigoto actually accepts.
Ground the checks in the authoritative INI docs (`leotorrez.github.io/modding/docs/*` — scrape via
`research`): valid section types, command syntax, key options, namespace rules. Then refine
health/duplicate/conflict logic (fewer false positives, real actionable findings).

### 4. Mod optimization tool (dedup assets, normalize names)
New tool: within a mod (or merge output), sha-dedup identical asset files (textures/buffers),
rewrite `.ini` `filename =` refs to the canonical copy, and normalize file names. Big archive-size
win for merged/multi-variant mods. Reuse the config-editor parse layer + planner-serialized writes.

### 5. Config-editor growth (extend the `.ini` editor)
Still open: `transition*` as ms number fields, **multiple `key=` lines** (ties into B4), Xbox `XB_*`,
controller combos; per-toggle grouping that ties a `[Key]`'s cycle list to the `$var` it drives
(cross-section view).

### 6. Remote mod library (the big reach)
Browse/fetch/download from remote sources (GameBanana-style) → one-click import into a profile.
Background WebView2 + per-site adapters (configurable, never hard-coded). Reuse ProcessRegistry +
Activity panel for download/import progress.

### 7. App self-update
Check latest GitHub release → download → prompt. Configurable channel + opt-out.
(`DownloadService` + update staging groundwork exists — see `.claude/rules/download-service.md`.)

---

## Ongoing / research

### Robustness of the file-based lifecycle ("robustness IS the UX")
Next audits: import partial-failure cleanup (extract OK → compress fails → orphaned cache/archive?),
merge staging cleanup on failure, and a pass that no mod archive/cache/preview path is mutated with
raw `Directory.*`/`File.*` outside the planner. See `filesystem-operation-serialization.md`.

### Mod-modification assistance (user wish 2026-06-19 — "really hard")
Pursue **hash-change detection / needs-refix flag** first (after a game update, compare
`TextureOverride` hashes against known-good sets / fix-tool detect step — leverages existing
fix+analysis). Recolor = partial (expose DDS for external editing). Retarget-to-other-character and
model surgery = long-horizon research, not committed scope.

### Doc consolidation — DONE 2026-07-05
CURRENT/MODULE architecture overlap resolved (each has one job); CHANGELOG split into
`docs/changelogs/2026-02|03/`; `keywords/FRONTEND.md` + `BACKEND.md` rewritten as compact current
indexes; `APP_FACADE_REFACTORING.md` archived (AppFacade no longer exists — routing is
MessageDispatcher → ProfileServiceRouter) and all references corrected.

---

## Parked / dropped (with reasons)

- **In-game on-screen toggle UI / menu — DROPPED.** No stock 3DMigoto primitive (no text/font/overlay
  command; `CustomShader` is raw DX11). Mods just cycle keys.
- **3DMigoto plugin-DLL interface — parked (low priority).** Not in the INI docs; XXMI bundles its own DLL.
- **Own 3DMigoto launcher (replicate XXMI inject) — parked.** `D3DMigotoService` backend exists but has
  no UI; injection is XXMI's job. (Kept in code deliberately — do not "clean up" without a decision.)
- **Set category color/icon — deferred.** Needs a `Category.color` field full-stack.
- **#11 thumbnail right-click crash — deferred (no repro).** Preview menu guarded + error boundaries;
  re-add if it recurs (capture `[ErrorBoundary]` console output).
- **Temp cleanup follow-up:** opt-in auto-clean on exit (configurable). Core FileCleanupTool is done.
- **Mod-load status detail follow-up:** optional per-file extraction counts. Stage reporting is done.

---

## Cross-cutting hygiene (ongoing)

- **Deferred dedup targets** (from the 2026-07-05 duplication audit — do opportunistically or as
  their feature comes up):
  - **Shared `.ini` parse helper** — 4 divergent parsers (ModIniService, ModKeybindingService,
    NamespaceMergeBuilder, ModAnalysisService) with inconsistent fullwidth `；` comment handling;
    consolidate into a Core helper **when doing B4 (keybinding multi-key) or the analyzer rework**,
    which touch those parsers anyway.
  - **`RunTrackedAsync` ProcessRegistry extension** — 9+ services repeat the Start/try/Complete/Fail
    wrapper; extract when next touching several producers at once.
  - **`DISABLED-` prefix constant** — string literal in ~8 files (ModCacheService has a local const);
    centralize alongside `GetCachePath` when next editing those services.
  - **Oversized files** — ModImportWorkflowHandler (1213 lines, step handlers extractable),
    ModAnalysisService (937), ModList.tsx (891, row renderer + context-menu builder extractable),
    HelpWindow.tsx (861, per-section components), CategoryGrid.tsx (745).
  - **`useEventSubscription` adoption** — ~15 components hand-wire `eventBus.subscribe`; migrate
    as-you-touch.
- Font sizes 12/14px only; CSS vars not hex; atomic design (L1/L2/L3 — `ui-component-layers.md`).
- Defensive `Array.isArray` guards on components consuming IPC arrays (pure-UI crash class).
- Verification gate: backend `dotnet build` + `dotnet test`; frontend `npx tsc --noEmit` + `npm test`
  (vitest, 192 passing) + `npm run build` + native `shot`. See `test-coverage-priorities.md`.
- After a multi-file wiring chain (3+ files), record it as a `.claude/rules/*.md`.
