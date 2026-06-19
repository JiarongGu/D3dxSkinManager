# Test Coverage Priorities

Current state as of 2026-04-13. Update this when new tests are added.

## Frontend — P0 gaps (untested, critical)

| Area | What to test |
|------|-------------|
| **ProfileContext** | Profile loading, switching, create/delete, error handling |
| **settingsStore** | Zustand state mutations, global vs profile settings |
| **modService (IPC)** | loadMod, unloadMod, updateMetadata, batch ops |
| **categoryService (IPC)** | getCategoryTree, hierarchy operations |
| **settingsService (IPC)** | getGlobalSettings, updateGlobalSetting |

## Backend — P0 gaps (untested, critical)

| Area | What to test |
|------|-------------|
| **SettingFacade + GlobalSettingService** | Settings persistence, concurrent updates |
| **ProfileFacade + ProfileService** | Profile CRUD lifecycle, config loading |
| **ModFacade** | Full IPC routing, PayloadHelper parsing |
| **ModArchiveService** | Archive extraction, error handling |
| **MigrationService** | Multi-step migration, progress, rollback |

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

**Skipped suites (pre-existing test debt — triage, don't trust as coverage):** these were written but
**never executed** before the runner existed, so they rotted against refactors —
`ThemeContext.test`, `TooltipSystem.test` (mocked `modules/settings/services/settingsService`, a path that
moved to `shared/services/ipc/`; even with the path fixed the assertions are stale), `settingsFileService.test`,
`workflowService.test` (stale service-mock assertions), `FindingsView.test` (incomplete antd stub — renders
`Input`/FormDialog the mock doesn't provide). All `describe.skip` with a `// TODO(test-runner)` marker.
Plus 3 `it.skip` in `searchQueryParser.test` (id-field / lone-dash — decide whether the util *should*
match `id` in an any-field search before un-skipping). `App.test.tsx` (CRA "learn react" boilerplate) was deleted.
