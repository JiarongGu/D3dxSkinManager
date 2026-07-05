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
- [ ] Save presist values, so after reload the mod back we have the presist value 3dmigoto usually resets if a new mod is loaded, and also need to allow to reset this or save this to the ini for default

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
- [ ] `RunTrackedAsync` ProcessRegistry wrapper (9+ services repeat Start/try/Complete/Fail — extract when next touching several producers)
- [ ] Oversized-file splits: ModImportWorkflowHandler (1213), ModAnalysisService (~950), ModList.tsx (891), HelpWindow.tsx (861), CategoryGrid.tsx (745)
- [ ] `useEventSubscription` adoption (~15 components hand-wire `eventBus.subscribe`)
- [ ] Migrate remaining `.ini` write-back rewriters' read paths opportunistically (parse layer done — `IniParser`)

## Parked (with reasons — don't pick up without a decision)
- In-game on-screen toggle UI — no 3DMigoto primitive (no text/overlay command)
- 3DMigoto plugin-DLL interface — XXMI bundles its own DLL; backend `Modules/Plugin` stays parked (do not delete)
- Own 3DMigoto launcher (`D3DMigotoService`) — injection is XXMI's job (kept in code deliberately)
- Plugin system UI — removed 2026-07-05 (user decision); re-add from git history if revived
- Update channel (beta/pre-release) — pointless until the repo publishes pre-releases
- Category color/icon — needs `Category.color` full-stack
- Thumbnail right-click crash — no repro; re-add if it recurs (capture `[ErrorBoundary]`)
- Temp cleanup: opt-in auto-clean on exit; mod-load per-file extraction counts

## Done (recent — one line + commit; older history in git log / docs/changelogs/)
- [x] Remote library stage 4: in-app adapter manager (add/edit/live-test/delete) + `direct` download method — `ab2e08a`
- [x] Remote library stage 3: shipped-JSON seeder + synced local index (instant search, date hints, imported badge, sha256 identity) — `17f99ad`
- [x] Remote library stages 1–2: config-driven adapters + Cloudreve resolve + browse tab + download/import — `d77f6fd`, `9215b72`
- [x] Robustness audit: partial-import rollback + raw-FS sweep verdicts — `b48799a`
- [x] IniParser migration of ModIniService/ModKeybindingService read paths — `d67e97c`
- [x] Analyzer UX: state persistence, fix-in-place, CompactIconButton L1 — `194d71f`
- [x] Everything earlier (analyzer grounding/dedup taxonomy/repair, import-queue overhaul, per-profile fix tools, config editor complete, XXMI settings, self-update, presets, optimizer, B1–B7) — see `git log` + `docs/changelogs/`

## Verification gate (every change)
Backend `dotnet build` + `dotnet test` (572); frontend `npx tsc --noEmit` + `npm test` (204) +
`npm run build`; UI changes: native `shot` in BOTH themes; e2e via `devtools/dev.mjs`
(`desktop-app-testing.md`). After multi-file wiring chains: record a `.claude/rules/*.md`.
