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

- Frontend: Jest + React Testing Library, `setupTests.ts` imports `@testing-library/jest-dom`
- Backend: xUnit + FluentAssertions + Moq, `InMemoryDatabaseTestBase` for DB tests
- Antd components in tests need `jest.mock('antd', ...)` to avoid `@rc-component/picker` resolution issue
- JSDOM lacks `scrollIntoView` — mock with `Element.prototype.scrollIntoView = jest.fn()`

## ⚠️ Frontend test RUNNER is not wired (found 2026-06-18)

The `D3dxSkinManager.Client` project has the **assertion libs** (`@testing-library/{react,dom,jest-dom,user-event}`, `@types/jest`) but **no runner**: no `jest`/`vitest` binary in deps, no `jest.config.*`/`babel.config.*`, no `test` script in `package.json`, and no root `package.json`. So every existing `*.test.tsx` (HealthStatusIcon, ScanView, FindingsView, …) **cannot currently execute**. `npx jest` falls back to a global jest with no transform → "Jest encountered an unexpected token" on TSX.

**Implication:** today the real frontend verification gate is `npx tsc --noEmit` + `npm run build` + visual (native `shot`). Tests are written to convention but unrunnable until a runner is added.

**To wire it up** (a real reliability task, not yet done): add `jest`, `jest-environment-jsdom`, and a TS transform (`ts-jest` **or** `babel-jest` + `@babel/preset-{env,react,typescript}`); a `jest.config.cjs` (jsdom env, `setupFilesAfterEnv` → `src/setupTests.ts`, `transformIgnorePatterns` allowing antd/rc-* ESM); and a `"test": "jest"` script. Then the existing `.test.tsx` files (and `CompactField.test.tsx`) run.
