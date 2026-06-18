# D3dxSkinManager — Tasks & Roadmap

> Scope: a **game-agnostic** mod manager for 3DMigoto / XXMI-style games (ZZZ/ZZMI, Endfield/EFMI,
> Genshin/GIMI, Star Rail/SRMI, Wuthering Waves/WWMI, Honkai/HIMI, and any future importer that
> follows the same `Mods/` + `.ini` hash-override convention). Nothing is hard-coded to one game.
> Design principle: **everything customizable** — where a "wished" default exists, seed it as editable
> config, don't bake it into code.

---

## Done

- **Fix-tool library — multiple entries** ✅ — a toolset can expose several runnable entries
  (`SetEntries`, marker stores one per line); lone-exe auto-resolves, ambiguous → user multi-selects.
  Mod "Fix" submenu flattens to "Toolset — entry"; manager shows a multi-select + per-entry run.
  E2E-verified (import 2-exe folder → set both → run). A fix runs with cwd = the mod folder, so it
  fixes everything in that dir. *Future:* a "fix the whole Mods folder in one pass" mode.

- **Fix-tool library — live folder watcher** ✅ — `FixToolsWatcher` (FileSystemWatcher on `fixtools/`,
  started in the profile-scoped `ToolFacade`) emits `FIX_TOOLS_CHANGED`; the manager + mod "Fix"
  submenu re-scan automatically when a tool is dropped in / removed on disk. E2E-verified.

- **Fix-tool library (Phase 0)** ✅ — per-profile collection at `{profile}/fixtools/`, **folder-derived**
  (each subfolder = one tool; name = folder name, entry auto-detected exe→bat/cmd→py). Drop a folder in
  → it auto-appears with default info; delete it → it's gone (no registry to drift). Import copies a
  file/folder into the collection. IPC `FIX_TOOLS_GET/IMPORT/DELETE`; Fix Tools manager screen
  (add folder/file, delete, run-on-all); mod right-click **"Fix" submenu** lists tools to run directly
  (replaces the old "Run Fix Script…" dialog; `…` reserved for real dialogs like "Manage fix tools…").
  ContextMenu gained submenu support. 5 tests + e2e (import→list→delete) on the real backend.
  *Remaining:* live FileSystemWatcher push so the list refreshes without reopening (entries are already
  folder-derived, so "new/gone" is reflected on every read).

- **#14 Mod update (replace content, same id)** ✅ — `ModImportService.UpdateModAsync` overwrites the
  compressed archive in place, keeps all metadata, and invalidates the cache (new content extracts on
  next load via #9). IPC `MOD/UPDATE_MOD` (per-mod queue-locked), `modService.updateMod`, mod
  context-menu "Replace Content from File…". 2 tests. E2E-verified against the real backend.

- **#9 Stale-cache invalidation on load** ✅ — `ModLifecycleService.LoadAsync` now re-enables a disabled
  cache only when it's still fresh; if the archive is newer (e.g. a hash-fix / mod-update recompressed
  it), the stale cache is discarded (planner-routed) and re-extracted. Directly de-risks #6/#14.
  Also added load-status detail ("Enabling cache" / "Extracting archive" / "Refreshing stale cache"),
  partially covering **#16**. 2 new lifecycle tests.

- **#6 Patch / hash fixing tool** ✅ (`ModFixService` + `ModFixTool`)
  - Runs a user-supplied fix script (`.py` / `.exe` / `.bat` / `.cmd`) against **a single mod, a
    selection, or all mods**. Script runs with cwd = the mod's content folder (the convention these
    scripts expect); changes are re-compressed back into the archive so they persist across reload.
  - Per-mod serialization via `ModOperationQueue`; ProcessRegistry-tracked + cancellable (Activity panel).
  - Tunables (Python interpreter candidates, timeout, supported extensions, stdin auto-confirm) live in
    a seeded `ModFixOptions` — **next step: surface these as editable settings** (see Phase 1).
  - Entry points: Tools card (all mods) + mod context menu (single / selected).

---

## Backlog — reviewed for current relevance

> **#13 selected-mod-applied-with-filter** ✅ — fixed: `ModListPanel` prunes the selection to the
> visible (filtered) set, so bulk apply/load/edit/delete/fix never act on hidden mods.
> **#11 thumbnail right-click crash** — needs a live repro/stack; the preview context menu is guarded,
> so the crash is likely elsewhere (import-dialog thumbnail picker?). Capture the `[ErrorBoundary]`
> console output when it happens and I'll pinpoint it.


| # | Item | Status after review | Notes |
|---|------|---------------------|-------|
| 3 | key binding modification | **Partly done** | `ModKeybindingService` already *parses* keybindings from `.ini`. Missing: *write-back* (edit toggle/cycle keys) + UI. Generalize: edit any mod `.ini` setting, not just keys. |
| 4 | launch integration with XXMI | **Done (MVP)** ✅ | Per-profile launch config (path+args) in `ProfileConfiguration.Launch` + status-bar Launch button; point it at the XXMI launcher. *Future:* multiple named targets + auto-detect. |
| 5 | launch integration with 3DMigoto | **Done (MVP)** ✅ | Same launch config — point it at the 3DMigoto loader. (One generic launch command; named multi-targets are the future enhancement.) |
| 9 | loading an active mod must invalidate its cache | **Done** ✅ | `LoadInternalAsync` now re-extracts when the archive is newer than the disabled cache (`IsDisabledCacheStale`); stale cache discarded via the planner. |
| 10 | temp cleanup | **Largely done** | `OrphanCategory.TempFile` + FileCleanupTool already scan/clean temp orphans. Possible follow-up: opt-in auto-clean on exit (configurable). |
| 11 | thumbnail right-click crash | **Open (bug)** | Repro: right-click on thumbnail selection in preview panel. Needs investigation in `ModPreviewPanel` / `PreviewImageCarousel`. |
| 12 | auto-update from GitHub release | **Open** | App self-update: check latest GitHub release, download installer, prompt. Configurable channel/repo + opt-out. |
| 13 | selected mod should not be applied with active filter | **Open (bug)** | When a filter is active, apply/load should act on the explicit selection, not the filtered-out set. Investigate apply/preset path. |
| 14 | mod update (replace mod with same id) | **Done** ✅ | `UPDATE_MOD` overwrites the archive + invalidates cache, keeping metadata. Context-menu "Replace Content from File…". |
| 16 | "preparing" mod load status detail | **Mostly done** | Load now reports sub-step detail ("Enabling cache" / "Extracting archive" / "Refreshing stale cache") via ProcessRegistry. Remaining: per-file counts during extraction (optional). |

---

## Roadmap toward the north star

**North star:** *make managing mods effortless* — discover, download, organize, fix, and launch without
leaving the app.

### Phase 0 — Fix-tool hardening (extends #6)
- Surface `ModFixOptions` as editable settings (global + per-profile override): Python path, timeout,
  extensions, auto-confirm. Seed detected Python path.
- Optional: per-profile **fix-tool registry** (save frequently-used fix scripts with a name) — bridges
  to "download fix tools per profile".

### Phase 1 — Launch & live workflow (#4, #5, #16, #9, #14)
- One configurable **launch-target** abstraction (named targets: XXMI / 3DMigoto / custom exe + args),
  auto-seeded by detection. Launch button in the status bar / per profile.
- Cache-correctness pass: invalidate/re-extract stale caches on load (#9) and enable in-place mod
  update keeping the same ID (#14). Granular load status detail (#16).

### Phase 2 — Authoring & polish (#3, #11, #13, #10)
- Mod `.ini` editor (keybindings + general settings) writing back to the cache + recompress (#3).
- Bug sweep: thumbnail right-click crash (#11), filtered-selection apply (#13), opt-in temp auto-clean (#10).

### Phase 3 — Remote mod library (new — the big lever)
- **Mod library / browser** backed by remote sources (e.g. GameBanana-style sites), with fetch +
  download + one-click import into a profile.
- Powerful scraping via a **background WebView2** (headless-ish, in-process) to handle JS-rendered
  pages and per-site adapters — sources are configurable/pluggable, never hard-coded to one site.
- Reuse the ProcessRegistry for download/import progress and the Activity panel.

### Phase 4 — App self-update (#12)
- GitHub-release auto-update with a configurable channel and opt-out.

---

## UX / UI improvement plan (proactive)

Cross-cutting polish to make management effortless. Prioritized; each is small-to-medium.

### High value
- **Bulk-action toolbar for the mod list** ✅ — appears above the list when 2+ mods selected: count,
  Edit, Fix ▾ (per-tool/entry), Delete, Clear. Reuses the batch handlers + the #13 selection pruning.
  *Future:* add Load/Unload-selected once batch load semantics are defined (category-exclusive).
- **Active-filter chips** ✅ — search-active status row under the search bar: "Showing N of M" + a
  removable chip that clears the search. *Future:* per-token chips + a category chip.
- **Fix Tools drag-drop import** ✅ — drop file(s)/folder(s) anywhere on the manager to add them as
  fix tools (reuses `useDropZone` native OS-path drop; backend auto-detects file vs folder). Empty-state
  card already present. *Future:* rename + description per tool.
- **Loaded/conflict clarity on mod cards** — clearer loaded vs disabled vs orphaned styling; a
  per-category "one active" indicator; surface category conflicts inline.

### Medium
- **Activity panel polish** ✅ — grouped into Running / History sections; running-count chip added to
  the status-bar task area. (Clear-completed + per-process detail were already present.)
- **Launch button** (depends on Phase 1) — prominent per-profile "Launch game" with the configured
  target; show last-launched + running state.
- **Consistent context menus** — shared grouping/iconography across mod-list, category, and preview
  menus; reserve "…" strictly for entries that open a dialog (done for fixes).
- **Profile switcher affordance** — quicker profile switch (the app is per-game); show game + mod count.

### Category right-click menu
- **Unload all in category** ✅ + **Fix all in category** ✅ (submenu of fix tools → runs against every
  mod in the category). Wired via the `context-menu-extension.md` 4-file chain + CategoryPanel handlers.
  Plus **Expand / Collapse subtree** ✅ (recursively expand/collapse a node's descendants, respecting
  locked nodes). *Future:* set color/icon — deferred, needs a `Category.color` field added full-stack
  (model + repo + IPC + picker), not present today.

### Reliability
- **Per-panel error boundaries** ✅ — `ErrorBoundary` gained a compact self-resetting mode; the
  Category / Mods / Preview panels are each wrapped, so one panel crashing shows a localized
  "… failed to render — Try again" instead of blanking the whole app (also contains #11 if preview-side).
  Each top-level tab (Mods/Tools/Plugins/Settings) is also boundary-wrapped, so a crash in any tab
  degrades locally instead of app-wide. *Next:* defensive `Array.isArray` guards on components
  consuming IPC arrays.

### Hygiene (ongoing, per ui-design-rules.md)
- Audit font sizes (12/14px only), hardcoded colors → CSS vars, and `danger`-button icon alignment.
- First-run onboarding: profile/game setup + link to install the matching Model Importer (XXMI/EFMI/…).
