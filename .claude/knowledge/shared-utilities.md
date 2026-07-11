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
| `flattenCategoryTree` / `flattenCategoryOptions` | `shared/utils/categoryTree.ts` | Category tree → flat node list / breadcrumb select options. NEVER hand-roll a tree flattener (5 copies deduped 2026-07-05) |
| `keyChord` (`buildRaw`/`buildDisplay`/`rawToDisplay`/`baseFromKey`) | `shared/utils/keyChord.ts` | 3DMigoto key-chord capture + raw↔friendly display (used by `KeybindingPreview` + `KeyCaptureInput`) |
| `KeyCaptureInput` | `shared/components/common/KeyCaptureInput.tsx` | Focus + press a key → captures a 3DMigoto hotkey chord; emits raw value. Includes the XB picker. Reuse for any hotkey field |
| `XboxButtonPicker` | `shared/components/common/XboxButtonPicker.tsx` | Dropdown of 3DMigoto `XB_*` controller buttons (gamepads fire no KeyboardEvent — pick, don't capture; `keyChord.XBOX_BUTTONS`) |
| `parseModRemoteRef` | `shared/utils/modRemoteRef.ts` | Parse a mod's `metadata.remote` identity (sourceId/listId/entryId/detailUrl) — powers the mod-detail remote backlink |
| `toPercent` | `shared/utils/toPercent.ts` | `current/total → 0–100` rounded + zero-guarded (never NaN/Infinity). Use for ANY progress-percent |
| `useModsState` | `modules/mod/hooks/useMods.ts` | Typed mods-store selector hook (`useModsState(s => s.slice)`). Selector param type derives from the store — do NOT hand-write `ReturnType<typeof useModsStore>` (resolves to `unknown`; see `risky-change-tests-first.md`) |
| `navigateToTab` / `navigateToModSearch` | `shared/hooks/useAppNavigation.ts` | Cross-module tab navigation + mod search with category |
| `useProcessStore` | `shared/store/processStore.ts` | Read-only Zustand mirror of the backend ProcessRegistry (see `background-task-tracking.md`) |
| `initProcessBridge` | `shared/store/processBridge.ts` | PROCESS_LIST_UPDATED events → processStore |

## Rules

1. **Never duplicate a utility** — always import from `shared/utils/`
2. **Service methods that are pure utilities** (no IPC calls) should be standalone functions in `shared/utils/`, not class methods
3. **If a utility is used in 2+ files**, extract it to `shared/utils/` immediately
4. **Dead code**: unused methods on IPC service classes should be removed, not left as baggage

## Past incidents

- **2026-04-13**: `formatBytes` was duplicated in 7 files across the codebase. Extracted to shared utility and removed all copies including dead-code methods on profileService and migrationService.
- **2026-04-13**: `navigator.clipboard.writeText + notification.success` pattern was duplicated in 5 files. Extracted to `copyToClipboard` shared utility.
- **2026-07-05**: category tree flattening was duplicated in 5 places (CategorySelect, ExportTab, CategoryScreen, CategoryGrid, ModEditScreen inline, + a `categoryService.flattenTree` class method). Extracted to `shared/utils/categoryTree.ts`; the service method was removed (rule #2: pure utilities are standalone functions, not IPC-class methods).
