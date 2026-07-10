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
- [ ] after merge nothing shows — FIX SHIPPED (gate now copies swapvar into a local and gates on the local, not a cross-ns read in the `if`); awaiting in-game confirmation (the existing broken merge was hand-patched to test). Confirm it renders, then close.
- [ ] when merge mod if there are same assets, try to dedup — depends on the above (rewriting resource paths on an unverified render path can itself cause invisibility)
- [ ] Save persist values: after reload restore a mod's 3DMigoto `$var` state (3DMigoto resets on new mod load); allow reset + save-to-ini-as-default — RUNTIME GATED (needs the game to verify persisted-var behavior); large feature, needs a design pass

### Features
- [ ] Global config consolidation: assess a global-level sqlite (`{data}/app.db`) for cross-cutting structured data. Audit 2026-07-10: `settings/global.json` (app settings), `settings/online-accounts.json` (tokens — now DPAPI-protected at rest), `remote-sources/*.json` (DELIBERATELY hand-editable adapters — keep as files), per-profile JSONs + sqlite. Verdict so far: nothing left that needs a DB; introduce app.db only when the first genuinely relational global data arrives (e.g. cross-profile download history)
- [ ] Remote library: WebView2-rendered fetch engine for JS-heavy/anti-bot sites (`engine:"webview"` — seam exists in `IRemotePageFetcher`)
- [ ] Remote library: sha256 duplicate detection ACROSS index entries (same file posted multiple times)
- [ ] Remote library: form-based adapter editor (today: validated JSON editor + live test)
- [ ] Remote library: account-gated resolver types (quark etc. — currently open-in-browser)
- [ ] Optimizer: file-name normalization option (reuses the `filename =` ref-rewrite machinery)
- [ ] Mod-modification assistance: hash-change detection / needs-refix flag after game updates (see "Ongoing/research" note in git history 2026-06-19)

### Verification (user-side)
- [ ] Mod-merge: two-same-character in-game test of the `$\<ns>\swapvar` gate + OR-condition cycle key (fallback: `activeOnly` OFF — see `3dmigoto-ini-interface.md`)
- [ ] B2: "edit mod value hangs after save" — not reproduced; if it recurs, capture `cdp iplog` on that mod (suspect: awaited archive patch on a very large mod)

### Hygiene (opportunistic — do as-you-touch)
- [ ] `RunTrackedAsync` ProcessRegistry wrapper (16 services repeat Start/try/Complete/Fail — extract when next touching several producers; risky as a big-bang, do incrementally)
- [ ] Oversized-file splits: ModImportWorkflowHandler (1213), ModAnalysisService (~950), ModList.tsx (891), CategoryGrid.tsx (745), RemoteLibraryView.tsx (~570), RemoteLibraryManagementScreen.tsx (~530 — extract RuleEditor/AliasEditor/LibraryList)
- [ ] `useEventSubscription` adoption (~15 components hand-wire `eventBus.subscribe`)
- [ ] Migrate remaining `.ini` write-back rewriters' read paths opportunistically (parse layer done — `IniParser`)
- [ ] Tests: `QuarkShareResolver` (token→save→download→cleanup, 23018) — the GameBanana + MatchTagRules parts of the 2026-07-06 audit are covered (`GameBananaEngineTests`, `RemoteLibraryStoreTests`)
- [ ] `Modules/Core/Helpers/` vs `Modules/Core/Utilities/` overlap (FileHelper vs FileUtilities, PathValidator vs ValidationHelper) — clarify split (stateful services vs static utils) or merge (audit 2026-07-06)

## Parked (with reasons — don't pick up without a decision)
- In-game on-screen toggle UI — no 3DMigoto primitive (no text/overlay command)
- 3DMigoto plugin-DLL interface — XXMI bundles its own DLL (this is unrelated to the app's OWN
  `Modules/Plugin` C# plugin system, which is now LIVE — see `plugin-system.md`)
- Own 3DMigoto launcher (`D3DMigotoService`) — injection is XXMI's job (kept in code deliberately)
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
