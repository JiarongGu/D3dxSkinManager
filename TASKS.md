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

### B2. Edit mod value hangs after save — PARTIAL (real bug found+fixed; hang not reproduced)
**Found + fixed 2026-07-05:** the edit screen's **category change was silently dropped** —
`UPDATE_METADATA` has no Category field, so the edited value visually applied then reverted on the
next refresh (very plausibly the reported symptom). `modOperations.updateMod` now routes a changed
category through `UPDATE_CATEGORY` (queue-locked, auto-unload).
**Hang itself not reproduced:** metadata path is pure DB (fast); `UPDATE_INI_ENTRY` measured **7ms**
e2e on a normal mod. Remaining suspect: the awaited archive patch on a **very large** mod (append
rewrites the container) — if the user still sees it, capture `cdp iplog` on the specific mod; the
fix would be fire-and-forgetting the archive patch (ProcessRegistry) after the cache write.

### B3. Fix tools not applied properly — ✅ FIXED 2026-07-05
Two real gaps found and fixed:
1. **Disabled-cache mods were fixed in a throwaway temp extract** — archive patched but the retained
   `DISABLED-{id}` working copy left stale, so re-enabling it deployed PRE-fix content. The fix now
   runs **in the retained cache in place** (active OR disabled, via `GetCachePath`) so cache and
   archive stay in sync; only cacheless mods stage to temp.
2. **Same-size+same-mtime rewrites were invisible** to the length+mtime diff (copystat-style script
   writes) → fix silently never persisted. Snapshot now content-hashes files ≤4MB (covers all
   `.ini`/config; bulk textures stay on the cheap check).
Detection always covered ALL file types (not only `.ini`). Tests: `ModFixServiceTests` (10, real
script execution incl. disabled-cache in-place + hash-detection regressions).

### B4. Keybinding multi-`key=` + combo editing — ✅ FIXED 2026-07-05
Three real bugs:
1. **Later `key =` lines in a `[Key*]` section overwrote the first** — `ModKeybinding` now carries
   `AdditionalKeys`/`AdditionalKeyDisplays`; the editor renders every chord as its own clickable
   chip (keyboard + controller alternates), each independently rebindable (write-back already
   replaced the matching line).
2. **Combo capture used `e.key`** (layout/shift-dependent): Shift+1 produced `'!'`, symbol keys like
   `[` didn't resolve → digit/symbol combos were uncapturable. New `baseFromEvent(e.code, e.key)`
   resolves from the physical code (letters, digits, F1–F24, numpad, punctuation as raw chars).
3. Fullwidth `；` comment lines weren't skipped by the keybinding parser/rewriter.
Tests: `ModKeybindingServiceTests` (9) + keyChord vitest (6 new). Verified e2e: fixture with
`key = no_ctrl alt j` + `key = XB_LEFT_SHOULDER` shows both chips; rebinding the controller line
left the keyboard line intact; chip-captured **Ctrl+Shift+1** saved as `ctrl shift no_alt 1`.

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

### 1. Update a preset in place — ✅ SHIPPED 2026-07-05
`ModPresetService.OverwriteAsync` replaces a preset's mod list with the currently loaded mods
(name kept; PRESET_NOT_FOUND / PRESET_NO_ACTIVE_MODS guards). IPC `OVERWRITE_PRESET`; preset menu
rows have a sync button → confirm dialog → success toast; PRESET_SAVED refreshes the menu.
Tests: `ModPresetServiceTests` (3). Verified e2e (32→31 count change, name kept).

### 2. Mod-merge — in-game validation (user-side)
Namespace merge v2 is shipped (`NamespaceMergeBuilder` + `ModMergeService`, IPC `MERGE_MODS`).
**Remaining:** a real two-same-character in-game test to confirm the `$\<ns>\swapvar` gating +
OR-condition cycle key (the one unverified assumption — see `3dmigoto-ini-interface.md`). Fallback if
the key misbehaves: `activeOnly` OFF emits no condition.

### 3. Analyzer improvement (better 3DMigoto understanding) — ✅ SHIPPED 2026-07-05
Grounded the checks in the scraped leotorrez INI docs (override/resource/operators/constants/present/
custom-shader): new shared `Core/Helpers/IniParser` (both comment chars, control-flow-safe, namespace
directive, `IsDisabledPath`); DISABLED-prefixed inis excluded from hashes (killed false conflicts/
duplicates on merged mods); new checks — MalformedHash (8/16-hex), DeadOverride, UnbalancedCondition
(Error), DuplicateSection (Info — real mods repeat [Constants]), KeyMissingBinding, AllIniDisabled;
ShaderOverride hashes join the conflict set; plugin refs map pattern→name explicitly + presence check
covers the XXMI `<importer>/Core`. Real-library audit: missingPlugin noise 39→6 on a 12-mod sample.
**Dedup-assist UX (the analyzer's core job): "keep this one" on every duplicate-group card → confirm
dialog → background batch delete** (Activity panel); issue rows show localized type chips; the
stale/missing filter now includes missingPlugin. Tests: IniParserTests (9) + 4 grounded-check
service tests (526 backend total). Verified live against the real library both themes.
**Dedup taxonomy SHIPPED 2026-07-05 (2)** (user-defined 4 cases): `identical` (same assets + same
ini = exact clone) / **`iniVariant`** (same asset bytes, different ini — labeled with WHAT differs:
hash fix / keybindings / defaults / logic, via new per-aspect `IniFingerprints` finding column +
migration 202607050001) / `textureVariant` (same buffers, new textures) / **`similar` ~N%** (scored
overlap-coefficient over target hashes + buffer/texture bytes, threshold 0.70 — replaces the brittle
buffer-only ≥80% subset check; containment = merged-mod case scores ~1.0). Score calibration
(user-reported false positive): asset BYTES dominate (0.2 target / 0.55 buffers / 0.25 textures)
plus a hard ≥0.6 byte-overlap gate — "same character" alone can't group different outfits sharing a
base mesh. **Unbalanced if/endif: real-library forensics (300+ archives sampled) confirmed the
detections are REAL mod defects that 3DMigoto tolerates → severity downgraded to Warning
("repairable") AND a one-click repair shipped**: `ModIniService.RepairConditionBalanceAsync`
(IPC `REPAIR_INI_BALANCE`, 修复 button on the finding row) appends missing `endif`s at section end,
comments out stray ones, persists via the fast archive patch; requires the mod's cache. The
findings "stale" section widened to **Needs Attention** (all warning-status mods now have a home).
Tests: +5 taxonomy + 4 repair tests (535 backend). Repair verified e2e on a planted-defect fixture.
**Analyzer workflow UX SHIPPED 2026-07-05 (3):** `analyzerUiStore` persists view/session/filter/search
across close-reopen (per-profile, locate-a-mod-and-come-back restores where you were, verified live);
per-row **Fix with…** dropdown runs a fix tool on the finding's mod in place (fire-and-forget →
Activity panel, no navigation); HistoryView icon pair migrated to `CompactIconButton` L1 (danger-button
vertical-offset fix). Fix-tool library is **per-profile** again (`{profile}/fixtools`, one-time seed
from the legacy global dir).

### 4. Mod optimization (dedup assets) — ✅ SHIPPED 2026-07-05
`ModOptimizeService`: sha256-groups byte-identical non-`.ini` files in the mod's cache (active or
disabled), rewrites every `filename =` ref (resolved relative to each `.ini`'s own dir, separator
style preserved) to the canonical copy, deletes redundant copies only when no reference remains,
then full-recompresses (deletions can't append). `.ini` files are never deduplicated (sections load
per-file). IPC `OPTIMIZE_SCAN` (awaited, read-only) + `OPTIMIZE_APPLY` (fire-and-forget, ONE
`process.optimize` registry entry). UI: mod right-click **优化模组 / Optimize Mod** → dialog scans on
open, shows kept/struck-through copies + saved bytes, Apply → Activity panel. Tests:
`ModOptimizeServiceTests` (5, real files incl. cross-folder relative-ref rewrite). Verified e2e.
**Follow-up (not shipped): file-name normalization** — renaming referenced files needs the same
ref-rewrite machinery; add as an optimizer option later.

### 5. Config-editor growth (extend the `.ini` editor)
SHIPPED 2026-07-05: `delay`/`transition`/`release_delay`/`release_transition` render as ms
`InputNumber` fields (suffix "ms", step 50) and `transition_type`/`release_transition_type` as a
linear/cosine Select in `ModIniEditor`, with friendly labels in both languages. Multiple `key=`
lines shipped earlier with B4. Verified e2e both themes against a fixture `[Key*]`; UI save
persists via `UPDATE_INI_ENTRY`.
SHIPPED 2026-07-05 (2): Xbox controller helpers — `XboxButtonPicker` (shared L1 dropdown of the
`XB_*` set; gamepad presses fire no KeyboardEvent so they're picked, not captured) attached to
`KeyCaptureInput` AND the keybinding-chip editor; `keyChord` renders XB/XB2 values friendly
("XB LB"). Verified live (pick → field shows XB LB → cancel restores).
SHIPPED 2026-07-05 (3): per-toggle grouping — each toggle card now surfaces the `[Constants]`
default of the `$var` its `[Key]` cycles (a Select over the exact cycle-list domain, never a
switch), removed from the plain Variables group; unclaimed vars stay listed. Frontend-only
(computed over `GET_INI_FILES`); writes go through the existing `UPDATE_INI_ENTRY` fast patch.
Verified live round-trip on a real mod (`global persist $xie` 0→1→0 on disk).
**Config-editor item COMPLETE.**

### 6. Remote mod library (the big reach)
Browse/fetch/download from remote sources (GameBanana-style) → one-click import into a profile.
Background WebView2 + per-site adapters (configurable, never hard-coded). Reuse ProcessRegistry +
Activity panel for download/import progress.

### 7. App self-update — ✅ SHIPPED (verified in code 2026-07-05)
`UpdateService`: GitHub latest-release check (version + release notes + manifest file-diff) →
download + sha256-verify → stage to `{install}/.update` → the C++ launcher applies on next start.
UI: Global settings check button + `UpdateDialog`; auto-check opt-in toggle (`autoUpdateCheck`).
**Deferred: configurable channel** (beta/pre-release) — pointless until the repo actually publishes
pre-releases; add a `releases` (non-latest) query + channel setting when that happens.

---

## Ongoing / research

### Robustness of the file-based lifecycle ("robustness IS the UX")
Audit round DONE 2026-07-05 (findings + verdicts recorded in the rule's bypass table):
- **Import partial-failure — REAL GAP, FIXED**: `ImportAsync` copies the archive before the DB row;
  a `CreateAsync` failure orphaned it. Now `RollbackImportAsync` (planner-routed) best-effort undoes
  the copy + auto-imported previews; rollback failure never masks the original error. Covers the
  workflow import too. Tests: `ModImportServiceTests` (+2).
- Merge staging cleanup on failure — already correct (`finally TryDeleteDir(staging)`).
- Raw-FS sweep over `Modules/` — new reviewed rows: ModOptimizeService (queue-locked, LOW),
  ModIniService/ModKeybindingService cache-`.ini` writes (accepted by design), FileCleanupService
  orphan deletes (LOW), ModPackageService (external-dir writes; preview copy = ImageService class).
Future rounds: re-run the sweep whenever a new service mutates mod data.

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
- **Plugin system UI — REMOVED 2026-07-05 (user decision: not implementing now).** The 插件 nav tab,
  `src/modules/plugin/` (PluginsView/PluginRegistry/usePluginSystem) and the dead `AppSider` were
  deleted; frontend `Module.PLUGIN` event plumbing removed. The BACKEND `Modules/Plugin/`
  (PluginLoader/PluginRegistry/PluginFacade, loads `Plugins/` dir at startup) stays parked like
  D3DMigotoService — do not remove without a decision; re-add UI from git history if revived.
- **Set category color/icon — deferred.** Needs a `Category.color` field full-stack.
- **#11 thumbnail right-click crash — deferred (no repro).** Preview menu guarded + error boundaries;
  re-add if it recurs (capture `[ErrorBoundary]` console output).
- **Temp cleanup follow-up:** opt-in auto-clean on exit (configurable). Core FileCleanupTool is done.
- **Mod-load status detail follow-up:** optional per-file extraction counts. Stage reporting is done.

---

## Cross-cutting hygiene (ongoing)

- **Deferred dedup targets** (from the 2026-07-05 duplication audit — do opportunistically or as
  their feature comes up):
  - **Shared `.ini` parse helper** — DONE 2026-07-05: `Core/Helpers/IniParser` shipped with the
    analyzer rework; ModAnalysisService, **ModIniService.GetIniFilesAsync** and
    **ModKeybindingService.ParseIniFileAsync** migrated (read paths). Side-fixes from the migration:
    control-flow lines (`if $x == 1`) no longer surface as bogus editable entries; `condition =`
    lines no longer misread as a keybinding's cycle var; disabled check now covers FOLDERS
    (`IsDisabledPath`, XXMI `exclude_recursive` semantics); key rebind matches/preserves inline
    comments. Write-back line rewriters stay by design, and NamespaceMergeBuilder stays a raw-line
    rewriter (IniParser is read-only — it strips the comments/layout a rewriter must preserve).
  - **`RunTrackedAsync` ProcessRegistry extension** — 9+ services repeat the Start/try/Complete/Fail
    wrapper; extract when next touching several producers at once.
  - **`DISABLED-` prefix constant** — DONE 2026-07-05: `Modules/Mod/ModConventions`
    (`DisabledCachePrefix` + `IsDisabledCacheName`/`CacheNameToModId`); all 8 call sites migrated.
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
