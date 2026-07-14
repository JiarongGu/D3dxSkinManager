# TASKS

> **How to use:** add a task anywhere in **Backlog** as a `- [ ]` line (one line, plain words —
> anyone can add, including the user). Agents work top-down unless told otherwise. When a task is
> finished, DELETE its line — the commit message is the record (no Done section piles up here).
> Detail/design lives in `.claude/rules/*.md` and `docs/` — NOT here. Keep this file a list.
>
> Scope ground rules (unchanged): game-agnostic 3DMigoto/XXMI mod manager; the app = compressed
> library + organize + fix + edit + deploy, XXMI = runtime; everything customizable via config, never
> hard-coded. Architecture context: `.claude/rules/` (start with `xxmi-integration.md`,
> `filesystem-operation-serialization.md`, `background-task-tracking.md`, `remote-library.md`,
> `3dmigoto-ini-interface.md`).

## In progress

(none)

## Backlog
- [ ] **[pre-existing, not from the review]** 2 failing frontend tests in `src/modules/remote/components/__tests__/PaginatedEditList.test.tsx` ("hides the search box/pager at threshold", "pages to next slice") — red on master since `761ddbc1` (remote refactor), unrelated to the code-review fixes. Search box shows when the test expects it hidden.
- [ ] skeleton loading does not reflect actual remote mod detail layout
- [ ] export/import a profile setting, so all setting + thumbnils with out mod or login creds, this include category category thumbnil, remote configs, so bascilly another use can use this to setup a profile without any heavy data
- [ ] (follow-up) remote-source README screenshots: the in-app guide's "Remote Library" page is now a full step-by-step (screenshot-free by rule); add step screenshots WITH highlight boxes to `docs/user-guide/images/` + reference from README (needs framing decisions — do with the user).
- [ ] (user-side) Confirm a real in-app MEGA download+import once — both FOLDER and FILE shares resolve + decrypt are live-validated (`MegaShareResolver`/`MegaCrypto`, remote-library.md); only the actual byte transfer + recompress + import is unrun in-app.

### Features

### Verification (user-side)
- [ ] Mod-state preset (mod `$var` persist): in-game confirm 3DMigoto restores the captured d3dx_user.ini
  toggles on apply — save a preset with "Also save mod state" checked, change toggles in-game, re-apply,
  confirm the saved toggles come back. (Mechanism + tests shipped; only the live 3DMigoto restore is gated.)

### Hygiene (opportunistic — do as-you-touch)
- (none pending) All four hygiene items ASSESSED 2026-07-14 → **leave as-is**, verdicts + reasons
  recorded in the rules so they're not re-litigated: `RunTrackedAsync` adoption (remaining services are
  non-clean fits; see `background-task-tracking.md`), oversized-file splits (`RemoteLibraryView`/
  `ModImportWorkflowHandler` reasonable/no clean seam; see `oversized-file-splits.md`),
  `useEventSubscription` adoption, and `.ini`→`IniParser` rewriter migration.

## Code Review Findings (2026-07-15) — full-codebase review

> From a 31-unit / 62-agent full-codebase review (119 confirmed at >=80 conf; full detail + the 62 near-misses in `CODE_REVIEW_FINDINGS.md`). Fixed items are DELETED as they land (the commit is the record); a few deferred decisions are kept below with a one-line reason. Verify each against code + `.claude/knowledge` rules before fixing.

### Critical (1)
- [ ] **[C]** `D3dxSkinManager/Modules/Tool/Services/ModFixService.cs:133-148` — confine fix-tool scriptPath to `{profile}/fixtools`? _(DEFERRED — UX call: confining breaks "browse to a downloaded tool"; threat needs a compromised first-party WebView)_

### High (7)
- [ ] **[H]** `D3dxSkinManager/Modules/Plugin/PluginFacade.cs:93-115` — Business logic (plugin init + event emission) lives inside the facade _(conf 90 · architecture)_
- [ ] **[H]** `D3dxSkinManager/Modules/Core/Event/EventBus.cs:88-95` — profile-scoped subs also match global (no-profileId) events _(DEFERRED — audit every global emit a profile handler subscribes to + tests-first before dropping the `IsNullOrEmpty(ProfileId)` clause)_
- [ ] **[H]** `D3dxSkinManager/Modules/Tool/ToolFacade.cs:314-347` — Package export and import await long ops in the IPC handler — will block and time out _(conf 95 · bug)_
- [ ] **[H]** `D3dxSkinManager/Modules/Launch/LaunchFacade.cs:64` — LAUNCH_DEPLOY awaits DeployVersionAsync synchronously in IPC handler — violates background-task-tracking rule _(conf 95 · bug)_
- [ ] **[H]** `D3dxSkinManager/Modules/Fluent/Services/MigrationRunner.cs:55-85` — GetPendingMigrationsAsync returns a fake list on error → migrates a broken DB _(DEFERRED — rethrow likely right but changes startup-failure behavior; needs a SQLite test first)_
- [ ] **[H]** `D3dxSkinManager/Modules/Migration/MigrationFacade.cs:72-109` — Business logic and event emission in facade — StartMigrationAsync is not a thin delegator _(conf 90 · bug)_
- [ ] **[H]** `D3dxSkinManager/Modules/Plugin/Services/PluginInstallService.cs:189-208` — plugin zip downloaded without sha256 verify _(DEFERRED — cross-repo: plugin manifest must publish per-asset hashes first; current mitigation = releases-prefix trust model)_

### Medium (48)
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/tool/components/TagManagementTool/TagManagementTool.tsx:40-41` — useState initialized with null instead of undefined for absent data _(conf 100 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/shared/components/compact/CompactButton.css:302-308` — Large button variant uses forbidden 16px font-size _(conf 100 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Tool/ModPackage/Services/ModPackageService.cs:79-251` — Long export/import operations awaited inside IPC handler — violates fire-and-forget rule _(conf 95 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Category/CategoryFacade.cs:89-92` — Facade throws InvalidOperationException instead of OperationException with i18n code _(conf 95 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/shared/components/common/TooltipSystem.tsx:1-113` — AnnotationProvider in common/ (L1/L2 zone) directly imports settingsService — L3 IPC in an atom layer _(conf 90 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/setting/operations/settingsOperations.ts:228` — `.toLowerCase()` applied to a C# enum string violates the camelCase serialization contract _(conf 90 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Context/Services/SecondaryWindowService.cs:125-134` — Service-locator pull of IGlobalSettingService from Setting module violates module boundaries _(conf 89 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/workflow/components/modImport/ModImportWorkflowScreen.tsx:90-101` — Async handler directly awaited inside useEventSubscription callback _(conf 88 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/mod/components/ModEditScreen/MetadataSection.tsx:89-98` — Raw antd AutoComplete used in L3 connected component — violates atom-first rule _(conf 88 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/Models/AppEnvironment.cs:72-81` — Core model directly instantiates `GlobalSettingService` from the Setting module — violates module boundaries _(conf 85 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/shared/components/compact/CompactSwitch.css:68-71` — Unchecked hover color is hardcoded dark-only and breaks in light theme _(conf 85 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Migration/Steps/MigrationStep3MigrateCategories.cs:18-19` — MigrationStep3 directly injects IModRepository for a cross-module read query _(conf 85 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Workflow/WorkflowFacade.cs:107-116` — Facade downcasts to concrete handler type — business logic leaking into facade _(conf 85 · architecture)_
- [ ] **[M]** `D3dxSkinManager/Modules/Profile/ProfileFacade.cs:255-343` — Business logic in facade: UpdateProfileConfigAsync merges, normalizes, and clamps fields _(conf 82 · architecture)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/mod/components/TagManagementDialog/TagManagementDialog.tsx:164-166` — "Deselect All" clears ALL selections rather than only the currently visible (filtered) tags _(conf 95 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Plugin/PluginFacade.cs:96-97` — ENABLE/DISABLE throws InvalidOperationException instead of OperationException with an error code _(conf 95 · bug)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/tool/components/ScreenCaptureTool/ScreenCaptureContext.tsx:173-187` — Init-time race: getScreenResolution() can silently overwrite a loaded saved-profile's form values _(conf 95 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Plugin/Services/PluginContext.cs:76-77` — GetPluginDataPath silently throws NullReferenceException when pluginId is not registered _(conf 92 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Category/Services/CategoryService.cs:116-121` — Fire-and-forget Task.Run in InvalidateTreeCache drops exceptions from EmitAsync silently _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Remote/Services/QuarkShareResolver.cs:209-229` — EnsureAppFolderAsync uses hard-coded page size of 100, silently creating duplicate folders on large drives _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/WebView/IpcHandler.cs:49-67` — IpcHandler._batchTimer is never stopped or disposed — timer fires indefinitely after WebView teardown _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/Utilities/Debounce.cs:44-64` — Replaced CancellationTokenSource is cancelled but never disposed — resource leak per call _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Mod/Services/ModQueryService.cs:61-66` — IMemoryCache subscription is never unsubscribed — ModQueryService leaks an event handler on dispose _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Fluent/Services/MigrationRunner.cs:93-137` — MigrateToLatestAsync and MigrateToVersionAsync are deceptively declared async but block the calling thread synchronously _(conf 90 · bug)_
- [ ] **[M]** `D3dxSkinManager.Client/src/shared/components/dialogs/ConfirmDialog.tsx:49-61` — Rethrown error in async onClick becomes unhandled promise rejection with no UI feedback _(conf 88 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Plugin/Services/PluginLoader.cs:39-96` — _loadFailures list has no lock — concurrent LoadPluginsAsync calls corrupt it _(conf 88 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Context/Services/ProfilePathService.cs:119-123` — Blocking async via Task.Run + GetAwaiter().GetResult() inside a synchronous cache factory can deadlock _(conf 88 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Fluent/Migrations/202603080002_CreateCategoriesTable.cs:16` — Global UNIQUE constraint on Categories.Name prevents two sibling categories with the same name under different parents _(conf 88 · bug)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/workflow/components/modImport/ModImportWorkflowScreen.tsx:141-162` — Auto-resume effect runs on every `workflows` change, causing repeated backend calls after resume _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Mod/Mappers/TagMapper.cs:50-55` — TagMapper.UpdateEntity mutates the primary key and is dead code _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/Helpers/ArchiveHelper.cs:648-722` — CancellationToken is not forwarded to Task.Run — compression cannot be cancelled mid-operation _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Migration/MigrationFacade.cs:79-94` — async void-equivalent lambda passed to Progress<T> swallows exceptions from EmitAsync _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Remote/Services/BaiduShareResolver.cs:239-253` — FindBestFile skips directories entirely, so a share whose root is a wrapper folder always fails _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Infrastructure/ApplicationHost.cs:461` — SaveWindowStateAsync().Wait() on UI thread risks deadlock _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/Services/PerformanceMonitor.cs:36-170` — PerformanceMonitor does not implement IDisposable — Timer and Process handles leak _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Setting/SettingServiceExtensions.cs:18-57` — Static mutable List<Type> (_registerdServices) is not thread-safe _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Launch/Services/D3DMigotoService.cs:263-270` — LaunchAsync falls back to first .exe in work directory — can launch wrong executable in xxmi mode _(conf 85 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Migration/Steps/MigrationStep5MigrateModArchives.cs:164-166` — Raw Directory.CreateDirectory on a mod archive parent path — bypasses IFileOperationPlanner _(conf 82 · bug)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/mod/components/ModListPanel/ModListStatusBar.tsx:108-111` — hasMultiple branch joins all loaded mod names with ', ' before text-overflow truncation — can produce a very long string in the status bar _(conf 82 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/System/Services/SystemSettingsService.cs:128-141` — RememberFileDialogPathAsync has a read-modify-write race with no lock _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager.Client/src/shared/hooks/useContentVeil.ts:23-23` — Module-level `verdictCache` Map is never pruned — unbounded memory growth _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/Services/CustomSchemeHandler.cs:97-120` — HandleRequest uses blocking GetAwaiter().GetResult() on async remote-image path _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Tool/Services/ModFixToolService.cs:377-384` — CopyDirectory uses string.Replace instead of prefix replacement — can produce wrong paths _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Core/WebView/WebViewInitializer.cs:370-378` — Deferral else-branch calls Build() from thread-pool thread if handle not yet created, violating CoreWebView2 UI-affinity _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Workflow/Handlers/ModImportWorkflowHandler.cs:281-329` — CancelAsync fire-and-forget captures a stale workflow snapshot, can re-mark a Completed workflow as Failed _(conf 80 · bug)_
- [ ] **[M]** `D3dxSkinManager/Modules/Migration/Parsers/PythonRedirectionFileParser.cs:84-113` — Path traversal: folder paths from _redirection.ini are combined with base dir without canonicalization _(conf 82 · security)_
- [ ] **[M]** `D3dxSkinManager/Modules/Migration/Steps/MigrationStep4MigrateCategoryThumbnails.cs:110-125` — Thumbnail full path built from parser-supplied relative path is used without canonicalization — path traversal to arbitrary profile subdirectory _(conf 80 · security)_
- [ ] **[M]** `D3dxSkinManager.Client/src/modules/remote/components/RemoteSourceEditor.tsx:195-202` — Resolver type dropdown in the form editor omits quark, baidu, mega, kodbox _(conf 90 · simplification)_

### Low (31)
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/tool/components/PythonMigrationTool/components/ProgressStep.tsx:213-218` — 11px font-size violates the 12px/14px-only rule _(conf 100 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/tool/components/PythonMigrationTool/components/ProgressStep.tsx:168-248` — Hardcoded hex colors instead of CSS variable tokens _(conf 100 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/TagChip/TagChip.css:34` — font-size: 16px on .tag-chip-large violates the 12/14px chrome rule _(conf 95 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/dialogs/FormDialog.css:82-88` — Hardcoded rgba() colors in light-theme close button instead of CSS vars _(conf 92 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/dialogs/InfoDialog.css:61-67` — Hardcoded rgba() colors in light-theme close button instead of CSS vars _(conf 92 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/dialogs/ConfirmDialog.css:92-98` — Hardcoded rgba() colors in light-theme close button instead of CSS vars _(conf 92 · architecture)_
- [ ] **[L]** `D3dxSkinManager/Modules/System/SystemFacade.cs:139-142` — OpenFileInExplorerAsync throws InvalidOperationException instead of OperationException with an error code _(conf 90 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/notification/CustomNotification.css:46` — Close button rendered at 20px via text character, violating font-size rule _(conf 90 · architecture)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/common/DataTable.css:15-17` — DataTable header cell references undefined CSS variable --color-ng-container and --color-text _(conf 95 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Core/Services/PathCache.cs:19-24` — SizeLimit is 50 but comment claims 500 — effective cache is 10x smaller than intended _(conf 95 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/workflow/components/modImport/ModImportWorkflowTable.tsx:225-254` — null sent for optional metadata fields violates the frontend 'undefined for absent data' rule _(conf 95 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Mod/Services/ModKeybindingService.cs:492-499` — ParseKeybindingsAsync swallows all exceptions via Console.WriteLine instead of the project logger _(conf 95 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/services/eventBus.ts:32-37` — SystemEventType.PROCESS_LIST_UPDATED and PROCESS_RESUME_REQUESTED missing from EventPayloadMap _(conf 92 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/services/eventBus.ts:74-82` — WorkflowEventType.DELETED declared in enum but absent from EventPayloadMap — subscribers get unknown payload type _(conf 90 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Core/Utilities/Throttle.cs:64-84` — `ExecuteAsync` missing `_isDisposed` guard — action can fire after disposal _(conf 88 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/profile/components/ProfileSwitcher.tsx:34-52` — Profile switch notification reports the OLD selected profile, not the newly switched one _(conf 88 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Mod/Services/ModMergeService.cs:99-105` — Disabled-file filter in merge uses naïve Contains check instead of project-standard IniParser.IsDisabledPath _(conf 85 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/remote/components/LibraryEditView.tsx:110-126` — Load-once effect ignores language changes — tag labels stay stale if the user switches language mid-session _(conf 85 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Core/WebView/WebViewSession.cs:171-175` — Triple consecutive unclosed XML doc-comment markers — stale dead markup _(conf 85 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Mod/Services/ModMergeService.cs:187-194` — CopyDirectory uses string.Replace which can corrupt paths if the source prefix appears elsewhere in a path segment _(conf 85 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Fluent/Migrations/202607060003_StandardizeRemoteIndexTags.cs:41-46` — Empty Down() on a destructive Up() leaves FluentMigrator version table inconsistent with actual schema after any rollback _(conf 82 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Plugin/Services/PluginInstallService.cs:218-222` — Fresh install calls LoadPluginsAsync() re-scanning all plugins, not just the new pack _(conf 80 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/mod/components/MergeModsDialog/MergeModsDialog.tsx:54-54` — Merge key validation restricts to single ASCII char, rejecting valid 3DMigoto VK combos _(conf 80 · bug)_
- [ ] **[L]** `D3dxSkinManager/Modules/Migration/MigrationFacade.cs:74-89` — Throttle state variable lastProgressEmit is captured by reference across concurrent callbacks without synchronization _(conf 80 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/mod/components/CategoryPanel/CategoryTreeContext.tsx:216-230` — Stale lockedCategories and selectedProfileId captured in handleLockExpanded / handleUnlockExpanded deps but never read _(conf 80 · bug)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/components/TagChip/TagChip.tsx:39-51` — Dead loading state causes wasted re-render on every mount _(conf 90 · simplification)_
- [ ] **[L]** `D3dxSkinManager/Modules/Profile/Models/WorkDirectoryConfiguration.cs:1-63` — WorkDirectoryConfiguration is dead code — superseded by ModWorkConfiguration _(conf 88 · simplification)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/tool/components/ModPackageTool/context/ModPackageContext.tsx:118-137` — findAndCollect found parameter is always false — dead code branch _(conf 85 · simplification)_
- [ ] **[L]** `D3dxSkinManager.Client/src/shared/services/ipc/settingsService.ts:28-65` — SettingsService does not extend BaseModuleService — sole IPC service that bypasses the shared base _(conf 85 · simplification)_
- [ ] **[L]** `D3dxSkinManager.Client/src/modules/workflow/components/modImport/ModImportWorkflowTable.tsx:579-581` — `getStatusTone` is a module-scoped pure function called inside `useMemo` deps but referenced as if it were a stable callback _(conf 82 · simplification)_
- [ ] **[L]** `D3dxSkinManager/Modules/Plugin/PluginFacade.cs:158-172` — GetAllPluginsAsync is synchronous but wraps result in Task.FromResult _(conf 82 · simplification)_

## Parked (with reasons — don't pick up without a decision)
- Merge same-asset dedup — NOT needed: `ModOptimizeService` (mod-optimize) already dedups shared assets;
  run optimize AFTER a merge instead of building dedup into the merge builder.
- In-game on-screen toggle UI — no 3DMigoto primitive (no text/overlay command)
- 3DMigoto plugin-DLL interface — XXMI bundles its own DLL (this is unrelated to the app's OWN
  `Modules/Plugin` C# plugin system, which is now LIVE — see `plugin-system.md`)
- Own 3DMigoto launcher (`D3DMigotoService`) — injection is XXMI's job (kept in code deliberately)
- Global config sqlite (`{data}/app.db`) — ASSESSED 2026-07-10, verdict NO: nothing left needs a DB
  (global.json settings, DPAPI-protected online-accounts.json, hand-editable remote-sources/*.json,
  per-profile sqlite all fit their stores). Revisit only when genuinely relational global data
  arrives (e.g. cross-profile download history)
- Update channel (beta/pre-release) — pointless until the repo publishes pre-releases
- Category color/icon — needs `Category.color` full-stack
- Thumbnail right-click crash — no repro; re-add if it recurs (capture `[ErrorBoundary]`)
- Temp cleanup: opt-in auto-clean on exit; mod-load per-file extraction counts

## Done
Finished tasks are NOT kept here — history lives in `git log` (conventional-commit messages carry the
detail) and `docs/changelogs/`. When you finish a backlog item, DELETE its line and let the commit
message be the record.

## Verification gate (every change)
Backend `dotnet build` + `dotnet test` (all green, no skips); frontend `npx tsc --noEmit` + `npm test` +
`npm run build`; UI changes: native `shot` in BOTH themes; e2e via `devtools/dev.mjs`
(`desktop-app-testing.md`). After multi-file wiring chains: record a `.claude/rules/*.md`.

