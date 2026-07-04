# Backend Keywords Index

> **Purpose:** Where backend things live — C# modules, services, infrastructure (.NET 10 + WinForms + WebView2)
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)
> **Rules that override anything here:** `.claude/rules/filesystem-operation-serialization.md`,
> `background-task-tracking.md`, `webview-resource-serving.md`, `download-service.md`,
> `enum-serialization.md`, `use-project-paths.md`.

**Framework:** net10.0-windows
**Last Updated:** 2026-07-05 (rewritten as a compact index; the old file carried 2026-03-era service
descriptions, dead classes — FileService, HashService, OperationNotificationService,
ClassificationService — and stale line numbers.)

---

## Entry + Infrastructure (`D3dxSkinManager/`)

| Thing | Path |
|-------|------|
| Entry point | `Program.cs` |
| App init + DI bootstrap | `Infrastructure/ApplicationBootstrapper.cs` (PerMonitorV2 DPI, CET off via csproj) |
| Main form (WebView2 host, window state, splash) | `Infrastructure/ApplicationHost.cs` |
| **IPC routing: module → profile-scoped facade** | `Infrastructure/ProfileServiceRouter.cs` (`MapModule<TFacade>`; creates/caches per-profile providers; runs migrations on first use) |
| i18n source files | `Languages/en.json`, `Languages/cn.json` (every `OperationException` code needs BOTH) |
| Tests | `D3dxSkinManager.Tests/` (xUnit + FluentAssertions + Moq; `TestHelpers/InMemoryFileSystem`) |
| Auto-update applier | `D3dxSkinManager.Launcher/` (C++; see `docs/LAUNCHER_ARCHITECTURE.md`) |

**Facade convention:** `Modules/{Module}/{Module}Facade.cs` — interface + implementation in ONE file,
extends `BaseFacade` (`Modules/Core/BaseFacade.cs`), routes in `RouteMessageAsync` switch. Thin
delegation only. DI per module in `Modules/{Module}/{Module}ServiceExtensions.cs` (`TryAddSingleton`).

## Core module (`Modules/Core/`)

- **WebView/** — `IpcHandler.cs` (WebView2 bridge; **`JsonStringEnumConverter(CamelCase)`** — see
  `enum-serialization.md`), `EventBusIpcBridge.cs` (backend events → frontend, batched),
  `WebViewInitializer.cs` (env, `app://` + `app.local` serving — see `webview-resource-serving.md`),
  `DropZoneManager.cs` (OS drag-drop overlay), `SplashScreenPanel.cs`
- **Services/** — `MessageDispatcher.cs` (middleware pipeline, `UseRoute`/`MapModule` fallthrough),
  `ProcessRegistry.cs` (**single source of truth for long ops** — `background-task-tracking.md`),
  `DownloadService.cs` (single HTTP chokepoint — `download-service.md`), `StartupCleanupService.cs`,
  `EagerLoadingService.cs` (startup prewarm), `GlobalPathService.cs`, `FileSystem.cs` (`IFileSystem`
  seam for planner tests), `FileTransferService.cs` (sha-named copies), `CustomSchemeHandler.cs`,
  `PathCache.cs`, `PerformanceMonitor.cs`, `WebViewSessionManager.cs`, `FormInteractionService.cs`
- **Helpers/** — `ArchiveHelper.cs` (SharpSevenZip + native 7z.dll: extract/compress/validate/append),
  `PayloadHelper.cs` (`GetRequiredValue`/`GetOptionalValue`), `LogHelper.cs`
- **Constants/ErrorCodes.cs**, **Exceptions/OperationException.cs** (`code` + params → frontend i18n
  `errors.{CODE}`), **Event/** (IEventBus/IProfileEventBus), Models, Utilities (LruCache)

## Context module (`Modules/Context/`) — profile-scoped plumbing

`FileOperationPlanner.cs` (**the ONLY place that mutates mod archive/cache/preview paths** —
`filesystem-operation-serialization.md`), `ProfilePathService.cs` (all profile paths —
`use-project-paths.md`), `SecondaryWindowService.cs`, `ProfileServerService.cs`, `ImageService.cs`
(thumbnails/previews; downscale at import, never at serve)

## Mod module (`Modules/Mod/`)

Layered (see `docs/core/DESIGN_DECISIONS.md` #6): Layer-1 pure ops, Layer-2 logic + events,
Layer-3 event consolidation.

| Service | Role |
|---------|------|
| `ModLifecycleService` | load/unload + category conflict; **owns the per-category lock** |
| `ModArchiveService` | extract/compress/delete; **`UpdateFileInArchiveAsync`** fast single-file patch |
| `ModCacheService` | enable/disable/delete/scan/clean cache dirs (planner-routed) |
| `ModCacheWatcher` | FileSystemWatcher → CACHE_CHANGED |
| `ModOperationQueue` | per-mod + per-category logical serialization |
| `ModImportService` / `ModDeletionService` | import pipeline / delete + batch delete (queue-locked) |
| `ModQueryService` / `ModEnrichmentService` | queries + status-flag/category/tag enrichment |
| `ModMetadataService` / `ModTagService` | metadata + batch category update / tags |
| `ModPresetService` (+ `ModPresetRepository`) | presets (apply/unload-all on registry) |
| `ModKeybindingService` | `[Key*]` parse + rebind write-back (fast patch) |
| `ModIniService` | config editor: parse all `.ini` → entries, server-side read-only guard |
| `ModMergeService` + `NamespaceMergeBuilder` | namespace-based mod-merge v2 (`3dmigoto-ini-interface.md`) |
| `ModListEventHandler` | 8 events → `MOD_LIST_UPDATED` |
| `ModRepository` / `TagRepository` | SQLite access |

## Other modules

| Module | Contents |
|--------|----------|
| `Category` | CategoryService (+cache), CategoryRepository, CategoryEventHandler → `CATEGORY_TREE_UPDATED`; Entities/Mappers |
| `Profile` | ProfileService (CRUD/switch), ProfileRepository |
| `Setting` | GlobalSettingService (`data/settings/global.json`), LanguageService, SettingFileService, WindowStateService |
| `System` | SystemFileDialogService, SystemFileService (open explorer/file), SystemProcessService, SystemSettingsService, `UpdateService` (GitHub release check + staged update → Launcher applies); SystemFacade also serves `GET_PROCESSES` etc. |
| `Launch` | `XxmiService` (detect XXMI installs/importers — `xxmi-integration.md`), `D3DMigotoService` (parked, no UI) |
| `Tool` | ModAnalysisService (+Repository) health/duplicates/conflicts; ModFixService (fix-tool runner, diff-based persist); ModFixToolService + FixToolsWatcher (fix-tool library); FileCleanupService (orphan scan/clean); ModIdMigrationService (hash→GUID); ConfigurationService; `ModPackage/` (export/import); `ScreenCapture/` (WGC capture + overlay) |
| `Workflow` | import workflow state machine: `Handlers/ModImportWorkflowHandler`, `Repositories/WorkflowRepository`, WorkflowConcurrencyManager, WorkflowResumeService |
| `Migration` | Python→React migration: MigrationService orchestrator + `Steps/` (1–6, `IMigrationStep` DI) + `Parsers/` |
| `Fluent` | migration framework: `Migration` base, `[Migration(YYYYMMDDHHmm)]`, builders; runs per-profile at startup (`docs/architecture/DATABASE_MIGRATION_ARCHITECTURE.md`) |
| `Plugin` | PluginLoader/PluginRegistry/PluginContext; external plugins in `Plugins/` |

## Key dependencies

SharpSevenZip + native `libs/7z.dll` (archives), Microsoft.Web.WebView2, Microsoft.Data.Sqlite,
SixLabors.ImageSharp, Microsoft.Extensions.DependencyInjection, xUnit/Moq/FluentAssertions (tests).

## Conventions

- PascalCase files; `Modules/{Module}/{Services,Models,Entities,Mappers}/`
- Services emit events (`IProfileEventBus`); facades never do
- Throw `OperationException("CODE", params)` + add BOTH language entries (`/error-with-i18n`)
- Long ops: fire-and-forget from facade + ProcessRegistry progress (`background-task-tracking.md`)
- Never raw `Directory.*`/`File.*` on mod data paths — planner only (`filesystem-operation-serialization.md`)
