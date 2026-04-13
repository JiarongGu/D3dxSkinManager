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

## Test infrastructure notes

- Frontend: Jest + React Testing Library, `setupTests.ts` imports `@testing-library/jest-dom`
- Backend: xUnit + FluentAssertions + Moq, `InMemoryDatabaseTestBase` for DB tests
- Antd components in tests need `jest.mock('antd', ...)` to avoid `@rc-component/picker` resolution issue
- JSDOM lacks `scrollIntoView` — mock with `Element.prototype.scrollIntoView = jest.fn()`
