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
- [ ] (follow-up) remote-source README screenshots: the in-app guide's "Remote Library" page is now a full step-by-step (screenshot-free by rule); add step screenshots WITH highlight boxes to `docs/user-guide/images/` + reference from README (needs framing decisions — do with the user).
- [ ] (user-side) Confirm a real in-app MEGA download+import once — both FOLDER and FILE shares resolve + decrypt are live-validated (`MegaShareResolver`/`MegaCrypto`, remote-library.md); only the actual byte transfer + recompress + import is unrun in-app.
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
