# D3dxSkinManager — Tasks & Roadmap

> Scope: a **game-agnostic** mod manager for 3DMigoto / XXMI-style games (ZZZ/ZZMI, Endfield/EFMI,
> Genshin/GIMI, Star Rail/SRMI, Wuthering Waves/WWMI, Honkai/HIMI, and any future importer following the
> same `Mods/` + `.ini` hash-override convention). Nothing is hard-coded to one game.
> Design principle: **everything customizable** — seed a wished default as editable config, don't bake it in.
>
> **North star:** make managing mods effortless — organize, fix, edit, deploy and launch without leaving
> the app. The app is the **compressed mod library + organize + fix + edit + deploy**; **XXMI is the
> runtime** (injects 3DMigoto, launches the game). We complement XXMI, never reimplement it.

Last consolidated: 2026-06-18.

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

---

## Recently shipped (this stretch)

- **XXMI integration in Settings** ✅ — 3-value work mode (internal/external/xxmi); importer list
  **discovered from disk** (scan the XXMI root for importer markers, enriched by config); one pick sets
  deploy dir + launcher path. Launch via status-bar button. Launch tab removed.
- **Per-profile fix-tool settings** ✅ — `ModFixService` reads `config.FixTools` at run time (python path
  +Detect, timeout, extensions, auto-confirm); `FixToolSettingsCard` with per-section save/reset.
- **Single-file archive patch** ✅ — `UpdateFileInArchive` planner op + `ArchiveHelper` append-replace
  (proven not-duplicate). ~143ms vs ~2.5s. Foundation for fast `.ini`/keybinding/fix writes.
- **Keybinding editor** ✅ — chord capture (combo + `no_` defaults, recording indicator), rebind written
  back via the fast patch. `CompactIconButton` atom (tone-border hover).
- **General config (`.ini`) editor** ✅ — `ModIniService` parses every `.ini` → sections → entries,
  classifies editable (`[Key*]`/`[Constants]`) vs read-only (hash/override/resource/shader/command);
  parses `namespace`; one-value write-back via the fast patch with a **server-side read-only guard**.
  UI = slide-in, left tab per file (equal-height independent scroll), friendly labels, `type`→Mode select,
  advanced collapsed. Opened from mod right-click **"Edit config"**. 8 tests.
- **Fix-tool diff-based persistence** ✅ — after a fix runs, patch only the changed/added files; full
  recompress only on a deletion or when changed bytes ≥50% of the mod. Most fixes touch tiny `.ini`, so
  the fast path is the norm. 8 tests.
- **Settings UX / atomic design** ✅ — per-section save/reset; `CompactField`, `StatusTag`,
  `CompactIconButton` atoms; rows aligned (see `.claude/rules/ui-component-layers.md`).

---

## Next up (prioritized, grounded)

### 1. Mod-merge ✅ SHIPPED (pending in-game validation)
Combine several mods of one slot into a single mod that **cycles between them with one key**. Built as a
faithful port of GIMI's `genshin_merge_mods.py` (NOT namespace-based — hash-dedup + `[CommandList]`
branching on `$swapvar`; see `.claude/rules/3dmigoto-ini-interface.md`):
- `MergeIniBuilder` (pure, 5 structural tests) — dedup overrides by `(hash, match_first_index)`, branch
  command lists on `$swapvar`, suffix binds/resource refs by `.{group}`, emit `[Constants] $swapvar` +
  `[KeySwap]` cycle + `[Present]`.
- `ModMergeService` — stage each source's cache, run the engine, disable source `.ini`s in the copy,
  compress → import as a NEW mod (own GUID, originals untouched). IPC `MERGE_MODS`.
- `MergeModsDialog` — multi-select right-click "Merge N Mods" → reorder + name + cycle key → creates it.
- Verified file-level e2e (new mod with valid merged `.ini`). **Remaining: in-game swap validation with
  two real same-character mods (user-side; game not available to the agent).** Known MVP gaps: reflection/
  credit/transparency special-cases + source ShaderOverride/CustomShader sections are dropped.

### 2. #4 First-run onboarding + mod-card clarity
- First-run: pick/create profile → point at the XXMI install (reuse `XxmiImporterPicker`) → ready.
- Per-category active indicator ✅ — a small white-ringed green dot on the category card thumbnail + tree
  node when it (or a collapsed descendant) has a loaded mod; tooltip names the mod. `activeMods` lives in
  the mods store (refreshed by ModProvider on load/unload/profile), grouped by category id.
- Still: clearer loaded vs disabled vs orphaned mod cards; inline category conflicts.

### 3. Config-editor growth (extend the `.ini` editor)
- Expose more `[Key]` options the docs confirm: `back` (reverse-cycle key), `wrap`, `smart`,
  `delay`/`transition*`, **multiple `key=` lines**, Xbox `XB_*`, combos. (Editor + keybinding capture.)
- Per-toggle grouping that ties a `[Key]`'s cycle list to the `$var` it drives (cross-section view).

### 4. #3 Remote mod library (the big reach)
- Browse/fetch/download from remote sources (GameBanana-style) → one-click import into a profile.
- Background WebView2 + per-site adapters (configurable, never hard-coded). Reuse ProcessRegistry +
  Activity panel for download/import progress.

### 5. #12 App self-update
- Check latest GitHub release → download → prompt. Configurable channel + opt-out.

---

## Parked / dropped (with reasons)

- **In-game on-screen toggle UI / menu — DROPPED.** No stock 3DMigoto primitive: `command-list`/`present`/
  `custom-shader` have no text/font/notification; `CustomShader` is raw DX11. Would require shipping a font
  texture + text-render shader — out of scope for generate-from-`.ini`. (Mods just cycle keys; the only
  built-in readout is 3DMigoto's debug overlay in `d3dx.ini`.)
- **3DMigoto plugin-DLL interface — parked (low priority).** Not in the INI docs; lives in `bo3b/3Dmigoto`
  source. XXMI bundles its own DLL, so we don't need it.
- **Own 3DMigoto launcher (replicate XXMI inject) — parked.** `D3DMigotoService` exists but inject is
  XXMI's job; we lean on XXMI.
- **Set category color/icon — deferred.** Needs a `Category.color` field full-stack (model+repo+IPC+picker).

---

## Open bugs / backlog

| # | Item | Status | Notes |
|---|------|--------|-------|
| 11 | thumbnail right-click crash | **Deferred (no repro)** | Set aside per user 2026-06-18 — preview menu is guarded + panels have error boundaries, no live repro. Re-add if it recurs (capture `[ErrorBoundary]` console output to pinpoint). |
| 10 | temp cleanup | **Largely done** | FileCleanupTool scans/cleans temp orphans. Follow-up: opt-in auto-clean on exit (configurable). |
| 16 | mod-load status detail | **Mostly done** | Reports "Enabling cache / Extracting / Refreshing stale cache". Optional: per-file extraction counts. |

> Done & verified earlier (kept for history): #6 fix tool, #9 stale-cache invalidation, #13 filtered-selection,
> #14 mod update (replace content), #3 keybinding+config editor, #4/#5 launch via XXMI. Fix-tool library
> (folder-derived, watcher, multi-entry). See git history for detail.

---

## Cross-cutting hygiene (ongoing)
- Font sizes 12/14px only; CSS vars not hex; atomic design (L1/L2/L3 — `ui-component-layers.md`).
- Defensive `Array.isArray` guards on components consuming IPC arrays (pure-UI crash class).
- Frontend test runner is NOT wired (see `test-coverage-priorities.md`) — gate is `tsc` + `npm run build`
  + native `shot`. Wiring jest/vitest is a real reliability task.
