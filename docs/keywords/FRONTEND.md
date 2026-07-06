# Frontend Keywords Index

> **Purpose:** Where frontend things live — React components, hooks, services, stores, types.
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)
> **Rules that override anything here:** `.claude/rules/ui-component-layers.md` (L1/L2/L3),
> `ui-design-rules.md`, `shared-utilities.md`, `enum-serialization.md`, `mod-list-derived-data.md`.

**Last Updated:** 2026-07-05 (rewritten as a compact index; the old file carried 2026-03-era history
and dead paths — `src/components/`, `src/hooks/`, `src/utils/`, `src/services/`, `src/types/` no
longer exist. Everything is under `src/modules/` and `src/shared/`.)

---

## Top level (`D3dxSkinManager.Client/src/`)

| Path | What |
|------|------|
| `App.tsx` | Root component: providers, lazy tabs, onboarding wiring |
| `index.tsx` | Entry; installs DEV `devInterceptor` (`window.__d3dx`) |
| `capture.tsx` | Separate entry for the screen-capture control-panel window |
| `setupTests.ts` | vitest setup (jest-dom, scrollIntoView/matchMedia stubs) |
| `styles/theme-colors.css` | ALL color tokens, light + dark (`ui-design-rules.md`) |
| `modules/` | Feature modules (see below) |
| `shared/` | Cross-module components/hooks/services/stores/types/utils |

## Modules (`src/modules/{module}/`)

| Module | Key contents |
|--------|-------------|
| `core` | `components/layout/` — AppHeader, AppStatusBar, **ActivityPanel** (ProcessRegistry mirror), **LaunchButton**, ModPresetMenu; `components/onboarding/OnboardingWizard.tsx`; `components/dialogs/KeyboardShortcutsDialog.tsx`; `utils/KeyboardShortcutManager.ts` |
| `help` | `components/HelpWindow.tsx` (slide-in help) |
| `mod` | `ModProvider.tsx` (event subscriptions → store refresh); `store/modsStore.ts` (flat Zustand store: mods, selectedMod/s, activeMods, modHealth, categorySearch, expanded/locked keys…); `operations/` — modOperations, categoryOperations; `hooks/useMods.ts`, `useResizablePanels.ts`; `components/` — **CategoryPanel/** (tree+grid, context menu — see `context-menu-extension.md`), **ModListPanel/**, **ModPreviewPanel/**, ModHierarchicalView, **ModEditScreen/**, **BatchEditScreen/**, **ModIniEditor/** (config editor), **MergeModsDialog/**, TagManagementDialog, MultiTagInput, GradingTag, ResizeHandle |
| `profile` | ProfileManager, ProfileSelector, ProfileSwitcher |
| `setting` | SettingsView (4 flat tabs) — **ModWorkSettingsTab** (work-dir mode + **XxmiImporterPicker** + binding summary + editable game-launch — see `xxmi-integration.md`), **ModImportSettingsTab** (compression), FixToolSettingsCard, GlobalSettingsTab, SettingsSectionActions, UpdateDialog; `store/settingsStore.ts` (incl. `launchPath`/`launchArgs` + baseline) |
| `tool` | ToolsView (card grid) + tools: FileCleanupTool/, ModAnalyzerTool/ (ScanView/FindingsView/HistoryView), ModFixTool/, ModIdMigrationTool/, ModPackageTool/ (Export/Import), PythonMigrationTool/, ScreenCaptureTool/, TagManagementTool/ |
| `remote` | Remote mod library tab (`remote-library.md` + `remote-library-redesign.md`): RemoteLibraryView (library SWITCHER + tag filter + card grid), RemoteLibraryManagementScreen (libraries CRUD + ordered tag→category import rules + sites section), RemoteModDetailScreen (left gallery/tags · right download actions), RemoteSourceEditor/ManagerScreen (adapter form), `store/remoteUiStore.ts` (browse state survives tab switches) |
| `workflow` | `components/modImport/` — ModImportWorkflowScreen + table (import queue) |

## Shared components (`src/shared/components/`) — layers per `ui-component-layers.md`

- **Root (L3/infra):** AppWrapper, AppLoader, ErrorBoundary, `CategorySelect` (connected category dropdown — never build a new category TreeSelect)
- **`compact/` (L1 atoms):** CompactButton, CompactInput, CompactSelect, CompactSwitch, CompactText (**CompactTitle clamps to 14px / 12px — never a raw antd `<Title>`, see `ui-design-rules.md`**), CompactCard, CompactAlert, CompactDivider, CompactSpace, CompactSection, **CompactField** (labeled form row), **CompactIconButton** (toned inline icon action), CompactUpload, CompactThumbnailUpload
- **`common/` (L1/L2):** SlideInScreen, DataTable, CloseButton, CountBadge, StatusIcon, HealthStatusIcon, **StatusTag** (semantic status pills), **MarkdownView** (zero-dep markdown renderer w/ typed callouts — powers the in-app user guide, see `in-app-guide.md`), TooltipSystem (AnnotationProvider/AnnotatedTooltip), **KeyCaptureInput** (3DMigoto hotkey chord capture)
- **`dialogs/` (L2 — MANDATORY over raw Modal):** ConfirmDialog, FormDialog, InfoDialog (no-transition, delayed loading built in)
- **`menu/`:** ContextMenu (manual positioning, viewport-aware)
- **`notification/`:** CustomNotification; **`TagChip/`:** TagChip

## Shared hooks (`src/shared/hooks/`)

useAppNavigation (cross-tab nav + mod search), useDelayedLoading, useDragDrop (in-window drag),
useDropZone (OS file drop via WinForms overlay), useEventSubscription, useScrollPosition,
useSlideInScreen, **useStableRef** (stale-closure fix — `REACT_CLOSURE_PATTERNS.md`), useTagManagement

## Shared context (`src/shared/context/`)

ProfileContext (profiles + selection), SettingsProvider (loads global settings → settingsStore),
ThemeContext (**reads settingsStore**; sets `data-theme` + antd algorithm), I18Provider,
SlideInScreenContext

## Shared stores (`src/shared/store/`)

processStore + processBridge — read-only mirror of the backend **ProcessRegistry**
(`background-task-tracking.md`). DEV: `window.__processStore`. Module stores live in their module
(`modules/mod/store/modsStore.ts`, `modules/setting/store/settingsStore.ts`).

## Services (`src/shared/services/`)

- `bridgeService.ts` — WebView2 IPC bridge (timeouts, DEV fake-bridge for pure-UI Chrome)
- `baseModuleService.ts` — base class; typed `sendTypedMessage/Array/Boolean/Optional` (preferred)
  + deprecated `sendMessage/sendArrayMessage/...`. NO `sendGlobalMessage` — omit profileId instead.
- `eventBus.ts` — frontend event bus (`Module` + type); `devInterceptor.ts` — DEV `window.__d3dx`
- `i18n.ts` — i18next init, loads translations from backend
- **`ipc/`** — one service per module, singletons + consolidated `api` export in `ipc/index.ts`:
  modService, categoryService, profileService, settingsService, systemService, toolService,
  workflowService, launchService, languageService

## Types (`src/shared/types/`)

`*.types.ts` per domain (mod, category, profile, analysis, cleanup, modFix, modIni, modPackage,
modIdMigration, migration, capture, language, message) + **`ipc/modIpcRequests.ts`** (typed IPC
request map used by `sendTyped*`). Enums from C# are **camelCase strings** (`enum-serialization.md`).
`src/shared/constants/errorCodes.ts` mirrors backend `ErrorCodes.cs`.

## Utils (`src/shared/utils/`) — check here BEFORE writing any utility (`shared-utilities.md`)

formatBytes, clipboardHelper (copyToClipboard), errorHandler (handleError), notification,
imageUrlHelper (`app://` URLs), memoizeDebounce, searchQueryParser (AND/OR/NOT/field/exact),
keyChord (3DMigoto chord build/display), fileTypeRouter, delayedLoading, logger

## Conventions

- PascalCase components, camelCase hooks/services/utils, `lowercase.types.ts` types
- Component folders own their CSS (BEM: `.component-name__element--modifier`)
- Data flows down as props; only L3 talks to IPC/stores/eventBus
- Tests colocated in `__tests__/` — vitest (`vi.*`), 192 passing
