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

- [ ] Remote-import download QUEUE + shared import queue (requested 2026-07-14) — proper queue+actor,
  not Task.Run management. **P1 DONE**: `ImportQueueActor` (internal actor = Channels mailbox + single
  lock-free loop) replaces `WorkflowConcurrencyManager`; local mod imports flow through it via typed
  `IImportJobHandler`; 8 actor tests + reworked handler tests + full E2E (dummy folder import) green.
  See `import-queue-actor.md`. **P2 TODO**: add a `REMOTE_IMPORT` `IImportJobHandler` +
  `RemoteImportWorkflowHandler` (download→recompress→import); `RemoteFacade` enqueues onto the actor
  instead of `RemoteImportService`'s own unbounded `Task.Run`. **P3 TODO**: unified queue UI
  (`ModImportWorkflowScreen` shows remote too) + ProcessRegistry bridge for the Activity panel.

## Backlog
- [ ] (follow-up) remote-source README screenshots: the in-app guide's "Remote Library" page is now a full step-by-step (screenshot-free by rule); add step screenshots WITH highlight boxes to `docs/user-guide/images/` + reference from README (needs framing decisions — do with the user).
- [ ] (user-side) Confirm a real in-app MEGA download+import once — both FOLDER and FILE shares resolve + decrypt are live-validated (`MegaShareResolver`/`MegaCrypto`, remote-library.md); only the actual byte transfer + recompress + import is unrun in-app.

### Features

### Verification (user-side)
- [ ] Mod-state preset (mod `$var` persist): in-game confirm 3DMigoto restores the captured d3dx_user.ini
  toggles on apply — save a preset with "Also save mod state" checked, change toggles in-game, re-apply,
  confirm the saved toggles come back. (Mechanism + tests shipped; only the live 3DMigoto restore is gated.)

### Hygiene (opportunistic — do as-you-touch)
- (none pending) All four hygiene items ASSESSED 2026-07-14 → **leave as-is**, verdicts + reasons
  recorded in the rules so they're not re-litigated: `RunTrackedAsync` adoption (remaining services are
  non-clean fits; see `background-task-tracking.md`), oversized-file splits (`RemoteLibraryView`/
  `ModImportWorkflowHandler` reasonable/no clean seam; see `oversized-file-splits.md`),
  `useEventSubscription` adoption, and `.ini`→`IniParser` rewriter migration.

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
