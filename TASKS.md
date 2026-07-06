# TASKS

> **How to use:** add a task anywhere in **Backlog** as a `- [ ]` line (one line, plain words —
> anyone can add, including the user). Agents work top-down unless told otherwise, tick items
> `- [x]` and move them to **Done** with the commit hash. Detail/design lives in `.claude/rules/*.md`
> and `docs/` — NOT here. Keep this file a list.
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
- [ ] Remote library: WebView2-rendered fetch engine for JS-heavy/anti-bot sites (`engine:"webview"` — seam exists in `IRemotePageFetcher`)
- [ ] Remote library: auto re-sync scheduling + stale-entry pruning (entries not seen in N syncs)
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
- [ ] Tests for the newest remote paths (audit 2026-07-06): `QuarkShareResolver` (token→save→download→cleanup, 23018), `GameBananaEngine` (ParseSubfeed/ProfilePage), `RemoteImportService.MatchTagRules` (ordered rules, title regex)
- [ ] `Modules/Core/Helpers/` vs `Modules/Core/Utilities/` overlap (FileHelper vs FileUtilities, PathValidator vs ValidationHelper) — clarify split (stateful services vs static utils) or merge (audit 2026-07-06)
- [ ] `ModAnalysisService` inline SHA256 (`ComputeCombinedHashAsync` + per-file loop) → reuse `IHashHelper`/a Core combined-hash helper (dedup audit 2026-07-06)

## Parked (with reasons — don't pick up without a decision)
- In-game on-screen toggle UI — no 3DMigoto primitive (no text/overlay command)
- 3DMigoto plugin-DLL interface — XXMI bundles its own DLL; backend `Modules/Plugin` stays parked (do not delete)
- Own 3DMigoto launcher (`D3DMigotoService`) — injection is XXMI's job (kept in code deliberately)
- Plugin system UI — removed 2026-07-05 (user decision); re-add from git history if revived
- Update channel (beta/pre-release) — pointless until the repo publishes pre-releases
- Category color/icon — needs `Category.color` full-stack
- Thumbnail right-click crash — no repro; re-add if it recurs (capture `[ErrorBoundary]`)
- Temp cleanup: opt-in auto-clean on exit; mod-load per-file extraction counts

## Done (recent — newest first; hashes omitted because history is periodically rebased, see `git log`)
- [x] UX polish: cleanup tool (open button → CompactIconButton, positive "all clean" empty state) + remote detail meta line
- [x] Analyzer back-nav: scan landing on open (no auto-jump into stale findings) + explicit "View last results" button
- [x] UI batch: 修复模组/Fix Mod moved below 优化模组 + renamed; app-wide table hover uses the theme token; fix-tools dashed import panel; remote detail open-page button
- [x] Remote image cache: per-profile `{profile}/remote-cache/images` (grid+detail serve via app:// after first load) + cleanup-tool 远程缓存 category
- [x] Remote index v2: per-profile SQLite (migration + repository), incremental UPDATE sync (stops at first fully-known page), whole-site sync removed, mod→remote reference
- [x] App-wide sweep: raw antd form controls → compact L1 atoms (23 files, uniform 32px; CompactInput/Button forward refs)
- [x] Remote library: per-profile game binding (setup view, bind & sync, 换绑); adapter manager (add/edit/live-test/delete) + `direct` download method; seeder + synced index + fix batch (scope/sort/sizes)
- [x] Remote library stages 1–2: config-driven adapters + Cloudreve resolve + browse tab + download/import
- [x] Robustness audit: partial-import rollback + raw-FS sweep; IniParser read-path migration; analyzer UX (persistence, fix-in-place)
- [x] Everything earlier (analyzer grounding/dedup taxonomy/repair, import-queue overhaul, per-profile fix tools, config editor complete, XXMI settings, self-update, presets, optimizer, B1–B7) — see `git log` + `docs/changelogs/`

## Verification gate (every change)
Backend `dotnet build` + `dotnet test` (572); frontend `npx tsc --noEmit` + `npm test` (204) +
`npm run build`; UI changes: native `shot` in BOTH themes; e2e via `devtools/dev.mjs`
(`desktop-app-testing.md`). After multi-file wiring chains: record a `.claude/rules/*.md`.
