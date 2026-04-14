# Shared Utilities Rule

Before writing ANY utility function (formatBytes, formatDate, clipboard copy, etc.), check `src/shared/utils/` first.

## Existing shared utilities

| Utility | Location | Use case |
|---------|----------|----------|
| `formatBytes` | `shared/utils/formatBytes.ts` | Human-readable file sizes |
| `copyToClipboard` | `shared/utils/clipboardHelper.ts` | Copy text + show notification |
| `handleError` | `shared/utils/errorHandler.ts` | Error handling + user messages |
| `notification` | `shared/utils/notification.ts` | Toast notifications |
| `imageUrlHelper` | `shared/utils/imageUrlHelper.ts` | File paths to `app://` URLs |
| `memoizeDebounce` | `shared/utils/memoizeDebounce.ts` | Debounced memoization |
| `parseSearchQuery` / `matchesSearchQuery` | `shared/utils/searchQueryParser.ts` | Search query parsing with AND/OR/NOT/field/exact operators |
| `navigateToTab` / `navigateToModSearch` | `shared/hooks/useAppNavigation.ts` | Cross-module tab navigation + mod search with category |
| `useTaskStore` | `shared/store/taskStore.ts` | Global background task tracking (addTask/updateTask/removeTask) |
| `initTaskEventBridge` | `shared/store/taskEventBridge.ts` | Backend progress events → taskStore (analysis, package) |

## Rules

1. **Never duplicate a utility** — always import from `shared/utils/`
2. **Service methods that are pure utilities** (no IPC calls) should be standalone functions in `shared/utils/`, not class methods
3. **If a utility is used in 2+ files**, extract it to `shared/utils/` immediately
4. **Dead code**: unused methods on IPC service classes should be removed, not left as baggage

## Past incidents

- **2026-04-13**: `formatBytes` was duplicated in 7 files across the codebase. Extracted to shared utility and removed all copies including dead-code methods on profileService and migrationService.
- **2026-04-13**: `navigator.clipboard.writeText + notification.success` pattern was duplicated in 5 files. Extracted to `copyToClipboard` shared utility.
