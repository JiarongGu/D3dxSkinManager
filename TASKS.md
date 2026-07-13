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
- [ ] I updated the latest d3dx app, and the old plugin could not loaded (you might need to update how we call the function of plugin, might use reflection to parse it)
- [ ] create a way for full remote sync that only sync the difference for list, and a way to sync updated detail content
- [ ] update main page guide for download (with chinese)
- [ ] update guide for how to add and use remote source (you probably can do a step by step screen shot with highlight box area for a lot of guide)
- [ ] load preset got some issue that load mod from decompress failed
- [ ] load preset does not load mod state properly on first run, but after mod f10 refresh then load again the state loaded
- [ ] some mod state does not loaded properly you might have to check its original state file properly
- [ ] in mod detail due to we have key bind button at the same row of remote source this ui is not consitant so 2 things we need to update 1. move down the source chip to the same row as category still align right 2 make the header and keybind botton heigh more aligned and lets do not hide the keybind button if its not avaliable just disable it and show tooltip for reason why it disabled on hover
- [ ] key bind update ui need to be updated (mostly for the controller option) currently its not consistant
- [ ] more huihui download support https://huihui168.org/?news_12/6647.html include different location of hui盘 and new provider MEGA
- [ ] so the plugin issue is the plugin looks like loaded but it does not show loaded on interface, but worked after I download 1.1 and restart the game (so this might not be an issue)
 [2026-07-12 22:28:42.206] [ERROR  ] [PluginLoader] Failed to load plugin from E:\Mods\MOD 管理器\data\profiles\ee122576-a8c0-4864-bc4a-0cb1165b4d1c\plugins\content-veil-ai\D3dxSkinManager.Plugins.ContentVeil.dll: Unable to load one or more of the requested types.
Could not load type 'D3dxSkinManager.Modules.Plugin.Interfaces.IImageReviewPlugin' from assembly 'D3dxSkinManager, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null'.
  Exception: ReflectionTypeLoadException: Unable to load one or more of the requested types.
Could not load type 'D3dxSkinManager.Modules.Plugin.Interfaces.IImageReviewPlugin' from assembly 'D3dxSkinManager, Version=4.2.0.0, Culture=neutral, PublicKeyToken=null'.
  StackTrace:    at System.Reflection.RuntimeModule.GetDefinedTypes()
   at System.Reflection.RuntimeModule.GetTypes()
   at D3dxSkinManager.Modules.Plugin.Services.PluginLoader.LoadPluginFromAssemblyAsync(String assemblyPath)
   at D3dxSkinManager.Modules.Plugin.Services.PluginLoader.LoadPluginsAsync()
[2026-07-12 22:28:42.718] [WARN   ] [Performance] Slow operation detected: WebView2.Initialize took 698ms
[2026-07-12 22:28:51.244] [ERROR  ] [PluginLoader] Plugin 'D3dxSkinManager.Plugins.ContentVeil.dll' type-load failed (Core contract mismatch?): Could not load type 'D3dxSkinManager.Modules.Plugin.Interfaces.IImageReviewPlugin' from assembly 'D3dxSkinManager, Version=4.3.0.0, Culture=neutral, PublicKeyToken=null'.
  Exception: ReflectionTypeLoadException: Unable to load one or more of the requested types.
Could not load type 'D3dxSkinManager.Modules.Plugin.Interfaces.IImageReviewPlugin' from assembly 'D3dxSkinManager, Version=4.3.0.0, Culture=neutral, PublicKeyToken=null'.
  StackTrace:    at System.Reflection.RuntimeModule.GetDefinedTypes()
   at System.Reflection.RuntimeModule.GetTypes()
   at D3dxSkinManager.Modules.Plugin.Services.PluginLoader.LoadPluginFromAssemblyAsync(String assemblyPath)
[2026-07-12 22:28:51.251] [WARN   ] [PluginLoader] No plugin types found in D3dxSkinManager.Plugins.ContentVeil.dll
[2026-07-12 22:28:51.605] [WARN   ] [Performance] Slow operation detected: WebView2.Initialize took 603ms
[2026-07-12 22:29:45.624] [WARN   ] [RemoteIndexService] [Remote] Detail enrichment failed for https://gamebanana.com/mods/686817: 'W' is an invalid start of a value. LineNumber: 1 | BytePositionInLine: 0.
[2026-07-12 22:29:46.207] [WARN   ] [RemoteIndexService] [Remote] Detail enrichment failed for https://gamebanana.com/mods/686817: 'W' is an invalid start of a value. LineNumber: 1 | BytePositionInLine: 0.
[2026-07-12 22:29:46.207] [WARN   ] [RemoteIndexService] [Remote] Enrichment aborted: batch made no progress
[2026-07-12 22:29:49.307] [ERROR  ] [PluginLoader] Failed to load plugin from E:\Mods\MOD 管理器\data\profiles\2a90074f-c976-498e-a541-c7d98ec9841d\plugins\content-veil-ai\D3dxSkinManager.Plugins.ContentVeil.dll: Could not load file or assembly 'D3dxSkinManager.Plugins.ContentVeil, Version=1.1.0.0, Culture=neutral, PublicKeyToken=null'. Assembly with same name is already loaded
  Exception: FileLoadException: Could not load file or assembly 'D3dxSkinManager.Plugins.ContentVeil, Version=1.1.0.0, Culture=neutral, PublicKeyToken=null'. Assembly with same name is already loaded
  StackTrace:    at System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath(String assemblyPath)
   at D3dxSkinManager.Modules.Plugin.Services.PluginLoader.LoadPluginFromAssemblyAsync(String assemblyPath)
   at D3dxSkinManager.Modules.Plugin.Services.PluginLoader.LoadPluginsAsync()
- [ ] Consolidate 3DMigoto + XXMI into a dedicated 3DMigoto module (3DMigoto is the CORE; XXMI is just one
  way users set it up — user framing 2026-07-13). Move `XxmiService` (→ a detector/adapter that points at a
  3DMigoto instance) + `D3dmigotoUserConfigService` + d3dx.ini / deploy-target / launch resolution into a
  `Modules/Migoto` (or similar); update DI/IPC/imports. Cross-cutting reorg — scope deliberately, tests-first.
  See `3dmigoto-ini-interface.md` (framing section).

### Features

### Verification (user-side)
- [ ] Mod-state preset (mod `$var` persist): in-game confirm 3DMigoto restores the captured d3dx_user.ini
  toggles on apply — save a preset with "Also save mod state" checked, change toggles in-game, re-apply,
  confirm the saved toggles come back. (Mechanism + tests shipped; only the live 3DMigoto restore is gated.)

### Hygiene (opportunistic — do as-you-touch)
- [ ] `RunTrackedAsync` ProcessRegistry wrapper (16 services repeat Start/try/Complete/Fail — extract when next touching several producers; risky as a big-bang, do incrementally)
- [ ] Oversized-file splits: ModImportWorkflowHandler (1213), ModAnalysisService (~950), ModList.tsx (891), CategoryGrid.tsx (745), RemoteLibraryView.tsx (~570)
- [ ] `useEventSubscription` adoption (~15 components hand-wire `eventBus.subscribe`)
- [ ] Migrate remaining `.ini` write-back rewriters' read paths opportunistically (parse layer done — `IniParser`)

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
