# Launcher topology: launcher = D3dxSkinManager.exe, runtime = lib/D3dxSkinManager.App.exe

**The user-run exe is the native C++ launcher (`{install}/D3dxSkinManager.exe`); the .NET runtime lives
in `{install}/lib/D3dxSkinManager.App.exe`. The launcher passes `--app-root "{install}"` so the app
resolves every install-relative path against the install root, NOT `lib/`.**

## Why

Users must always start from the launcher (it applies staged updates + auto-installs .NET), so the
launcher took the top-level `D3dxSkinManager.exe` name and the runtime moved into `lib/`. But
`AppDomain.CurrentDomain.BaseDirectory` is the *running exe's* folder — for the runtime that is now
`{install}/lib`, which would repoint `data/`, `res/`, `libs/`, `.update/` and the WebView2 user-data
folder at `lib/`. The launcher therefore passes the true install root via `--app-root`, and the app uses
THAT as `IAppEnvironment.BaseDirectory`. Everything downstream (`GlobalPathService`, `UpdateService`,
`WebView2EnvironmentPrewarmer`) reads `BaseDirectory`, so this single override fixes all paths.

Full deep-dive: `docs/LAUNCHER_ARCHITECTURE.md` (authoritative). This rule is the invariant checklist.

## How to Apply

**Threading the install root (mandatory):**
- Launcher: `main.cpp` `MAIN_EXE = L"lib\\D3dxSkinManager.App.exe"`; `dotnet_runtime.cpp`
  `LoadAndRunDotNetApp` appends `--app-root "{appDirectory}"` (appDirectory = launcher dir = install root).
- App: `Program.Main(string[] args)` → `ApplicationBootstrapper.Run(args)` → `installDir =
  AppRootArg.Resolve(args, AppDomain.CurrentDomain.BaseDirectory)`. `AppRootArg` falls back to
  BaseDirectory for a dev/direct run (no launcher). NEVER read `AppDomain...BaseDirectory` directly for
  install paths — go through `IAppEnvironment.BaseDirectory`.

**Single-instance** (`SingleInstanceGuard`, runs FIRST in `Run`, before the WebView2 prewarm): the app is
NOT multi-instance-safe (per-profile single-writer SQLite; the mod-cache `FileOperationPlanner` only
serializes WITHIN a process; the WebView2 user-data folder is one OS lock). Named `Local\` Mutex keyed by
the install dir (distinct installs coexist); a 2nd launch broadcasts a per-install registered message and
exits; the running `OptimizedForm` (via `WndProcHook`) foregrounds. Does NOT break the update restart —
`RestartToApplyUpdateAsync` exits the old process (releasing the mutex) before the launcher relaunches.

**Updater migration invariants** (`updater.cpp` `ApplyPendingUpdate`) — the migration flips
`D3dxSkinManager.exe` from *app* (old topology, listed in the old manifest) to *launcher* (new topology,
excluded from the new manifest). Three coordinated guards MUST all hold or migration bricks the install:
1. **robocopy `/XF` excludes only the RUNNING launcher's OWN name** (dynamic, `GetModuleFileNameW`), NOT a
   hardcoded pair. During migration the OLD launcher (`D3dxSkinManager Launcher.exe`) runs, so the staged
   NEW launcher (`D3dxSkinManager.exe`) is a *different* name and DOES copy. In a new self-update the
   running launcher IS `D3dxSkinManager.exe`, so it excludes itself (never self-updates).
2. **The removal step skips BOTH launcher basenames** (`D3dxSkinManager.exe` + `D3dxSkinManager
   Launcher.exe`). Without this the old→new manifest diff deletes the just-copied new launcher.
3. **Close-all before overlay** force-closes any running app-runtime process under the install
   (`lib/D3dxSkinManager.App.exe`, or old `D3dxSkinManager.exe`), skipping the launcher's own PID — a
   safety net for a hung instance so the exe unlocks for robocopy.
- The orphaned old `D3dxSkinManager Launcher.exe` is deleted on the next boot by
  `LegacyLauncherCleanupStep` (an `IStartupCleanupStep`), guarded to run only once the new launcher exists.

**Manifest / build layout:**
- `build-manifest.mjs` `EXCLUDE_BASENAMES` excludes `d3dxskinmanager.exe` (launcher) + the legacy
  `d3dxskinmanager launcher.exe` + `manifest.json`. `lib/D3dxSkinManager.App.exe` IS listed (it's the app).
- `build-production.ps1` + `.github/workflows/release.yml`: launcher → `{release}/D3dxSkinManager.exe`;
  runtime → `{release}/lib/D3dxSkinManager.App.exe`; `res/`, `libs/`, `manifest.json` stay at the root.
- `Launcher.vcxproj` `TargetName` = `D3dxSkinManager`. `UpdateService.LauncherExeName` = `D3dxSkinManager.exe`.

## Edge cases where it does NOT apply
- **Self-contained build** (`-SelfContained`): no launcher, runtime stays at the root as
  `D3dxSkinManager.exe`, `--app-root` unnecessary (BaseDirectory is the root). No `lib/` move.
- **Dev/debug run** (`app-dev.mjs`): the app is launched directly (no launcher, no `--app-root`) → falls
  back to `bin/Debug` BaseDirectory. `project.config.mjs.exe` stays the debug path — the `lib/` topology is
  production-packaging only.

## Verify
- C#: `AppRootArgTests`, `SingleInstanceGuardTests`, `LegacyLauncherCleanupStepTests`.
- Launcher end-to-end: `node devtools/dev.mjs test-update-apply` (two scenarios: new self-update +
  old→new migration) — requires the launcher rebuilt (output `D3dxSkinManager.exe`). The C++ compile +
  real Windows migration must be done locally / on CI (can't build C++ in the agent sandbox).

## Related
- `background-task-tracking.md` (the update download is a fire-and-forget ProcessRegistry op)
- `use-project-paths.md` (all install paths via `IAppEnvironment`/path services, never raw)
- `download-service.md` (the `IStartupCleanupStep` pipeline the legacy-launcher cleanup plugs into)
