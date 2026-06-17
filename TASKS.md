# D3dxSkinManager — Tasks & Roadmap

> Scope: a **game-agnostic** mod manager for 3DMigoto / XXMI-style games (ZZZ/ZZMI, Endfield/EFMI,
> Genshin/GIMI, Star Rail/SRMI, Wuthering Waves/WWMI, Honkai/HIMI, and any future importer that
> follows the same `Mods/` + `.ini` hash-override convention). Nothing is hard-coded to one game.
> Design principle: **everything customizable** — where a "wished" default exists, seed it as editable
> config, don't bake it into code.

---

## Done

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

| # | Item | Status after review | Notes |
|---|------|---------------------|-------|
| 3 | key binding modification | **Partly done** | `ModKeybindingService` already *parses* keybindings from `.ini`. Missing: *write-back* (edit toggle/cycle keys) + UI. Generalize: edit any mod `.ini` setting, not just keys. |
| 4 | launch integration with XXMI | **Open** | No launch config on `Profile` yet. Add per-profile launch target (path + args) seeded by detecting XXMI Launcher / a Model Importer instance. |
| 5 | launch integration with 3DMigoto | **Open** | Same launch-config mechanism as #4, target = raw 3DMigoto loader. Share one configurable "launcher" abstraction (list of named launch targets), not two hard-coded paths. |
| 9 | loading an active mod must invalidate its cache | **Open (correctness)** | `LoadInternalAsync` calls `EnableCacheAsync` (re-uses the disabled cache). If the archive changed (e.g. after a fix / mod update), the cache is stale. Fix: re-extract when archive is newer than cache. Tied to #6 and #14. |
| 10 | temp cleanup | **Largely done** | `OrphanCategory.TempFile` + FileCleanupTool already scan/clean temp orphans. Possible follow-up: opt-in auto-clean on exit (configurable). |
| 11 | thumbnail right-click crash | **Open (bug)** | Repro: right-click on thumbnail selection in preview panel. Needs investigation in `ModPreviewPanel` / `PreviewImageCarousel`. |
| 12 | auto-update from GitHub release | **Open** | App self-update: check latest GitHub release, download installer, prompt. Configurable channel/repo + opt-out. |
| 13 | selected mod should not be applied with active filter | **Open (bug)** | When a filter is active, apply/load should act on the explicit selection, not the filtered-out set. Investigate apply/preset path. |
| 14 | mod update (replace mod with same id) | **Open** | Re-import a new archive over an existing mod ID (keep metadata/category, swap content + invalidate cache). Shares cache-invalidation logic with #9. |
| 16 | "preparing" mod load status detail | **Partly done** | `LoadInternalAsync` registers a ProcessRegistry process + emits `LOADING`, but with no sub-step detail. Add `Report(detail)` for "extracting archive", "enabling cache", file counts. Small add on the process-monitoring work. |

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
