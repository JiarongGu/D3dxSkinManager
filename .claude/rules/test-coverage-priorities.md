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
| **ModArchiveService** | Archive extraction, error handling (heavier — file ops) |
| **MigrationService** | Multi-step migration, progress, rollback (heavier) |

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

## Frontend test runner — WIRED 2026-06-19 (vitest)

Was unwired (assertion libs but no runner). Now: **vitest** (chosen over jest — jest fights antd 6's deep
ESM; vitest reuses Vite's esbuild pipeline so it "just works"). `tsconfig.json` already excludes test
files from the build `tsc`, so test globals don't affect `npm run build`. The existing `jest.*` calls were
converted to `vi.*` (no `requireActual` to worry about).

**Current state:** `npm test` is GREEN — **99 passing**, 0 failing. The frontend verification gate is now
`npx tsc --noEmit` + `npm test` + `npm run build` + visual (native `shot`).

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
negation char) is now a no-op instead of a literal search term. Frontend suite: **168 passing, 0 skipped**.
Plus 3 `it.skip` in `searchQueryParser.test` (id-field / lone-dash — decide whether the util *should*
match `id` in an any-field search before un-skipping). `App.test.tsx` (CRA "learn react" boilerplate) was deleted.
