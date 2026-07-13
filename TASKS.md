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

### Features

### Verification (user-side)
- [ ] Mod-state preset (mod `$var` persist): in-game confirm 3DMigoto restores the captured d3dx_user.ini
  toggles on apply — save a preset with "Also save mod state" checked, change toggles in-game, re-apply,
  confirm the saved toggles come back. (Mechanism + tests shipped; only the live 3DMigoto restore is gated.)

### Hygiene (opportunistic — do as-you-touch)
- [ ] `RunTrackedAsync` ProcessRegistry wrapper — STARTED 2026-07-14. `ProcessRegistryExtensions.RunTrackedAsync` (Core) wraps the FIRE-AND-FORGET pattern (Start + Task.Run + try-Complete/catch-OCE-Cancel/catch-ex-Fail+onError); tested (`ProcessRegistryExtensionsTests`); adopted in `XxmiService.StartInstallerDownload`. **Adopt the rest INCREMENTALLY + verify each — semantics vary:** it fits only EXACT pattern-2 matches (OCE→Cancel). Clean P2 candidates left: `ModLifecycleService`, `PluginInstallService`, `RemoteImportService` (has a finally→wrap it in the work delegate), `ModAnalysisService` (resumable). NOT a fit as-is: pattern-1 sync services (12 of them: ModMerge/ModCache/ModDeletion/… treat OCE as Fail + rethrow, some have TWO Completes like UpdateService) — those want a separate `TrackAsync` (sync, no Task.Run, rethrow) whose OCE handling must match each. Don't blanket-apply.
- [ ] Oversized-file splits — **only clean seams, accept reasonable oversize** (see `oversized-file-splits.md`). Remaining: RemoteLibraryView.tsx (~570); ModImportWorkflowHandler (1225, stateful steps — no clean seam, likely leave). DONE: ModAnalysisService (grouping→ModAnalysisReportBuilder, 1199→978); ModList.tsx (932→671, row + useInfiniteScroll + useModFixTools; context-menu/actions left inline — entangled); CategoryGrid.tsx (736→531, CategoryGroup + segment helpers).
- [x] `useEventSubscription` adoption — ASSESSED 2026-07-14, verdict LEAVE AS-IS. Not ~15 sites: it's `ModProvider` (13 subs in one `[]`-deps effect **guarded** by `if(!selectedProfileIdRef.current)return` = subscribe-once-at-mount-if-profile — `useEventSubscription` is always-on, can't reproduce that guard without a behavior change on the central mod-event hub) + `useDropZone` (3 subs tangled with DOM element setup/`classList` cleanup). Neither is a behavior-preserving 1:1 swap; both patterns are reasonable. Per `oversized-file-splits.md` (accept reasonable) + `risky-change-tests-first.md` (event wiring is silent-at-runtime), don't force it.
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
