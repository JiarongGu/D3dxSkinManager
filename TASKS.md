# TASKS

> **How to use:** add a task anywhere in **Backlog** as a `- [ ]` line (one line, plain words —
> anyone can add, including the user). Agents work top-down unless told otherwise. When a task is
> finished, DELETE its line — the commit message is the record (no Done section piles up here).
> Detail/design lives in `.claude/rules/*.md` and `docs/` — NOT here. Keep this file a list.
>
> Scope ground rules (unchanged): game-agnostic 3DMigoto/XXMI mod manager; the app = compressed
> library + organize + fix + edit + deploy, XXMI = runtime; everything customizable via config, never
> hard-coded. Architecture context: `.claude/rules/` (start with `xxmi-integration.md`,
> `filesystem-operation-serialization.md`, `background-task-tracking.md`, `remote-library.md`,
> `3dmigoto-ini-interface.md`).

## In progress

(none)

## Backlog
- [ ] (follow-up) remote detail sync: optional per-library "prefer cache" setting + a proactive "re-sync changed detail" pass (re-fetch entries with a stale DetailFetchedUtc). CORE shipped: "Full re-sync" menu (diff-write + prune) + live-first/cache-fallback detail persistence.
- [ ] update main page guide for download (with chinese)
- [ ] update guide for how to add and use remote source (you probably can do a step by step screen shot with highlight box area for a lot of guide)
- [ ] load preset got some issue that load mod from decompress failed
- [ ] load preset does not load mod state properly on first run, but after mod f10 refresh then load again the state loaded
- [ ] some mod state does not loaded properly you might have to check its original state file properly
- [ ] mod editor: let a hotkey have BOTH a keyboard key AND a controller button (co-exist, not either/or). VERIFIED against 3DMigoto (bo3b/3Dmigoto `Dependencies/d3dx.ini` `[KeyMomentaryHoldExample]` has `Key = RBUTTON` + `Key = XB_LEFT_TRIGGER` — "used interchangeably"): a `[Key]` section takes MULTIPLE `key =` lines. App already MODELS it (`ModKeybinding.additionalKeys`) and the keybind modal shows keyboard-editable + controller read-only chips. GAP is the mod INI editor: an `isHotkey` row is ONE `KeyCaptureInput` that REPLACES the value when you pick XB. Need an add/remove "alternate key line" UI + backend to add a `key =` line to the section (`KeyCaptureInput`/`XboxButtonPicker` + `ModIniEditor` + the `.ini` write-back). The SAME co-exist logic ALSO applies to the keybind modal (`KeybindingPreview`) — it currently shows controller alternates read-only; it should let you ADD/remove a controller alternate there too. Build the add/remove-alternate as SHARED logic used by both the mod editor and the keybind modal.
- [ ] more huihui download support https://huihui168.org/?news_12/6647.html include different location of hui盘 and new provider MEGA
- [ ] GameBanana detail enrichment fails on some mods with `'W' is an invalid start of a value` (non-JSON/HTML response, e.g. mods/686817) → enrichment aborts "no progress". Detect non-JSON responses + skip/retry gracefully instead of aborting the batch.
- [ ] Consolidate 3DMigoto + XXMI into a dedicated 3DMigoto module (3DMigoto is the CORE; XXMI is just one
  way users set it up — user framing 2026-07-13). Move `XxmiService` (→ a detector/adapter that points at a
  3DMigoto instance) + `D3dmigotoUserConfigService` + d3dx.ini / deploy-target / launch resolution into a
  `Modules/Migoto` (or similar); update DI/IPC/imports. Cross-cutting reorg — scope deliberately, tests-first.
  See `3dmigoto-ini-interface.md` (framing section).

### Features

### Verification (user-side)
- [ ] Mod-state preset (mod `$var` persist): in-game confirm 3DMigoto restores the captured d3dx_user.ini
  toggles on apply — save a preset with "Also save mod state" checked, change toggles in-game, re-apply,
  confirm the saved toggles come back. (Mechanism + tests shipped; only the live 3DMigoto restore is gated.)

### Hygiene (opportunistic — do as-you-touch)
- [ ] `RunTrackedAsync` ProcessRegistry wrapper (16 services repeat Start/try/Complete/Fail — extract when next touching several producers; risky as a big-bang, do incrementally)
- [ ] Oversized-file splits: ModImportWorkflowHandler (1213), ModAnalysisService (~950), ModList.tsx (891), CategoryGrid.tsx (745), RemoteLibraryView.tsx (~570)
- [ ] `useEventSubscription` adoption (~15 components hand-wire `eventBus.subscribe`)
- [ ] Migrate remaining `.ini` write-back rewriters' read paths opportunistically (parse layer done — `IniParser`)

## Parked (with reasons — don't pick up without a decision)
- Merge same-asset dedup — NOT needed: `ModOptimizeService` (mod-optimize) already dedups shared assets;
  run optimize AFTER a merge instead of building dedup into the merge builder.
- In-game on-screen toggle UI — no 3DMigoto primitive (no text/overlay command)
- 3DMigoto plugin-DLL interface — XXMI bundles its own DLL (this is unrelated to the app's OWN
  `Modules/Plugin` C# plugin system, which is now LIVE — see `plugin-system.md`)
- Own 3DMigoto launcher (`D3DMigotoService`) — injection is XXMI's job (kept in code deliberately)
- Global config sqlite (`{data}/app.db`) — ASSESSED 2026-07-10, verdict NO: nothing left needs a DB
  (global.json settings, DPAPI-protected online-accounts.json, hand-editable remote-sources/*.json,
  per-profile sqlite all fit their stores). Revisit only when genuinely relational global data
  arrives (e.g. cross-profile download history)
- Update channel (beta/pre-release) — pointless until the repo publishes pre-releases
- Category color/icon — needs `Category.color` full-stack
- Thumbnail right-click crash — no repro; re-add if it recurs (capture `[ErrorBoundary]`)
- Temp cleanup: opt-in auto-clean on exit; mod-load per-file extraction counts

## Done
Finished tasks are NOT kept here — history lives in `git log` (conventional-commit messages carry the
detail) and `docs/changelogs/`. When you finish a backlog item, DELETE its line and let the commit
message be the record.

## Verification gate (every change)
Backend `dotnet build` + `dotnet test` (all green, no skips); frontend `npx tsc --noEmit` + `npm test` +
`npm run build`; UI changes: native `shot` in BOTH themes; e2e via `devtools/dev.mjs`
(`desktop-app-testing.md`). After multi-file wiring chains: record a `.claude/rules/*.md`.
