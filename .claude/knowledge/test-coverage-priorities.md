# Test Coverage Priorities

Current state as of 2026-04-13. Update this when new tests are added.

## Frontend — P0 gaps (untested, critical)

| Area | What to test |
|------|-------------|
| ~~**ProfileContext**~~ | ✅ DONE 2026-06-19 (`ProfileContext.test.tsx` — load+select-active, first-fallback, load error, create-adds, delete-selected-guard, throws-outside-provider). Fixed a real **stale-closure bug**: delete/select guards read `state` from `useCallback([])` → mirrored latest state into a ref; action callbacks now `return execute(...)` so callers can catch. |
| ~~**settingsStore**~~ | ✅ DONE 2026-06-19 (`settingsStore.test.ts` — dirty tracking, baseline reset) |
| ~~**modsStore**~~ | ✅ DONE 2026-06-19 (`modsStore.test.ts` — setters, busy tracking, per-profile reset) |
| **modService / categoryService / settingsService (IPC)** | thin `sendMessage` wrappers — LOW value (asserting "calls bridge with type X"); deprioritized |

## Backend — P0 gaps (untested, critical)

| Area | What to test |
|------|-------------|
| ~~**GlobalSettingService**~~ | ✅ DONE 2026-06-19 (`GlobalSettingServiceTests` — persistence, single-field update, cache, log-level, events) |
| ~~**ProfileService**~~ | ✅ DONE 2026-06-19 (`ProfileServiceTests` — create/get/update/delete lifecycle, delete-active guard, switch) |
| ~~**ModFacade**~~ | ✅ DONE 2026-06-19 (`ModFacadeTests` — routing GET_ALL→repo+enrich, GET_BY_ID parses id payload (real PayloadHelper), missing-payload→error, LOAD→queue→lifecycle, unknown-type→error). 18 services mocked + real PayloadHelper. |
| ~~**ModArchiveService**~~ | ✅ DONE 2026-06-19 (`ModArchiveServiceTests` (7) — path/exists, extract missing/success(maps type+fileCount)/planner-fail, delete exists/missing, single-file append. Planner mocked (no real 7z); `File.Exists` backed by a temp file). |
| ~~**MigrationService**~~ | ✅ DONE 2026-06-19 (`MigrationServiceTests` (5)). Refactored the ctor from 6 concrete `MigrationStepN` params → `IEnumerable<IMigrationStep>` (DI registers each step `AddSingleton<IMigrationStep, …>`); the orchestrator self-orders by `StepNumber`. Tests: runs in StepNumber order regardless of injection order, a throwing step stops the run + records `FailedAtStep`/`FailedStepName`, progress reported through Complete, pre-cancelled token runs nothing, AnalyzeSourceAsync drives step 1. |

## Remote module — frontend tests now EXIST (2026-07-13)

Was a P0 gap (no `modules/remote/**/__tests__`). First suites added for the source-editor UX pass:
`RemoteSourceTestResultView.test` (pass/fail indicator states), `RemoteSourceCompareDialog.test`
(only-differing-fields + revert-to-default), `RemoteSourceEditor.test` (Save disabled until dirty; Test
runs + renders the indicator; compare button gated on `origin==='customized'`). Backend:
`RemoteBrowseServiceTests` test-connection (success/failure-as-data/no-lists) + `RemoteSourceStoreTests`
`GetDefault`. Pattern for antd-`Select` components: the global `ResizeObserver` stub is in `setupTests.ts`.

## What's well-tested

- Category module (service, repository, events, mapper) — excellent coverage
- Mod module (repository, import, lifecycle, metadata, operation queue)
- Tool module (ModAnalysisService — grouping, conflicts, state machine)
- **File-operation concurrency** (added 2026-06-17): `FileOperationPlannerConcurrencyTests` +
  `InMemoryFileSystem` fake prove serialization under parallel mixed ops, transient-lock retry,
  compress-once-under-lock, and persistent-lock → in-use error. `ModLifecycleServiceTests` prove
  same-category loads serialize / different categories parallelize. `ModOperationQueueTests` cover
  the ref-counted lock-handle cleanup race.

## Test infrastructure notes

- Frontend: **Vitest** + React Testing Library (jsdom). Run with `npm test` (`vitest run`) / `npm run test:watch`.
  Config: `vitest.config.ts` (reuses Vite's react + tsconfig-paths plugins so antd 6 / lodash-es / `@/`
  alias resolve like the app). `globals: true` → `describe/it/expect/vi` are globals (use `vi`, not `jest`).
  `src/setupTests.ts` imports `@testing-library/jest-dom` and stubs `scrollIntoView` + `matchMedia` (jsdom gaps).
- Backend: xUnit + FluentAssertions + Moq, `InMemoryDatabaseTestBase` for DB tests
- jsdom lacks `scrollIntoView`/`matchMedia` — already stubbed in `setupTests.ts` (no per-test mock needed).
- **Backend test namespace gotcha (2026-07-15):** a test file physically under `Modules/System/` MUST use
  namespace `D3dxSkinManager.Tests.Modules.SystemModule.Services` — NOT `...Modules.System.*`. Introducing a
  `Modules.System` namespace shadows the BCL `System` for EVERY sibling test under `Modules.*` that writes a
  fully-qualified `System.X` (`System.IO`, `System.Text`, `System.Collections`, `System.InvalidOperationException`),
  producing a cascade of `CS0234 'X' does not exist in 'D3dxSkinManager.Tests.Modules.System'`. Match the existing
  `SystemModule` convention (see `UpdateServiceTests`).

## Frontend test runner — WIRED 2026-06-19 (vitest)

Was unwired (assertion libs but no runner). Now: **vitest** (chosen over jest — jest fights antd 6's deep
ESM; vitest reuses Vite's esbuild pipeline so it "just works"). `tsconfig.json` already excludes test
files from the build `tsc`, so test globals don't affect `npm run build`. The existing `jest.*` calls were
converted to `vi.*` (no `requireActual` to worry about).

**Current state (2026-07-05):** `npm test` is GREEN — **192 passing, 0 skipped** (18 files). The frontend
verification gate is `npx tsc --noEmit` + `npm test` + `npm run build` + visual (native `shot`).

**Revived 2026-06-19:** `FindingsView.test` (9) — completed its antd stub (`Input`) + mocked `FormDialog`
to a visible-gated stub. `workflowService.test` (6) — the failure was NOT stale assertions: the
`jest.`→`vi.` migration missed **multi-line chains** (`const spy = jest⏎  .spyOn(...)` — `jest` at
line-end, no dot) so `jest` was undefined. Fixed the trailing `jest`→`vi`. (Watch for this pattern if
reviving others.) Both green.

`settingsFileService.test` (24) — was stale against the `BaseModuleService` refactor: it mocked
`bridgeService` and asserted **positional** `sendMessage('SETTING','GET_FILE',{...})` + expected `null`,
but the service now calls `this.sendMessage(type, profileId, payload)` → `bridgeService.sendMessage({
module, type, profileId, payload })` and returns **`undefined`**, and no longer `console.error`s.
Rewrote the call assertions to the object shape + `null`→`undefined` + dropped the stale console assert. Green.

`TooltipSystem.test` (13) — the `settingsService` *instance* lives in the ipc barrel
(`settingsService.ts` only exports the class), so the mock/import had to target `'../../../services/ipc'`
not the file (it was resolving to `undefined`). Also updated 2 stale assertions: the retry test now
waits on the call count (not the displayed text, which already shows the default `all`), and
`useAnnotation` no longer throws outside a provider (default-valued context) — asserts the default.
`ThemeContext.test` — **DELETED** (obsolete): ThemeContext was refactored to read theme from
`useSettingsStore` (SettingsProvider loads it); the test covered removed responsibilities (self-load,
3× retry/backoff, throw-outside-provider). A fresh store-reactive suite (setTheme→updateGlobalSetting,
effectiveTheme auto-resolution, data-theme) is a clean future task, not a revival.

**Fully green — no skips at all.** The `searchQueryParser` id/lone-dash question was resolved
(2026-06-19, per user: *id-search is used internally a lot*): id matches **exactly** (full GUID, no
substring noise) for both bare terms and the `id:` prefix — the tests were corrected to assert exact
(`mod().id` is `'abc123'`, so `'abc'` must NOT match). Plus a real util fix: a standalone `'-'` (stray
negation char) is now a no-op instead of a literal search term. The former 3 `it.skip` in
`searchQueryParser.test` were resolved by that decision. `App.test.tsx` (CRA "learn react" boilerplate) was deleted.
