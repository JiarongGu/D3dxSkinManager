# Launcher topology: launcher = D3dxSkinManager.exe, runtime = libs/D3dxSkinManager.App.exe

**The user-run exe is the native C++ launcher (`{install}/D3dxSkinManager.exe`); the .NET runtime lives
in `{install}/libs/D3dxSkinManager.App.exe` (the same `libs/` folder as `7z.dll`). The launcher passes
`--app-root "{install}"` so the app resolves every install-relative path against the install root, NOT
`libs/`.**

## Why

Users must always start from the launcher (it applies staged updates + auto-installs .NET), so the
launcher took the top-level `D3dxSkinManager.exe` name and the runtime moved into `libs/`. But
`AppDomain.CurrentDomain.BaseDirectory` is the *running exe's* folder — for the runtime that is now
`{install}/libs`, which would repoint `data/`, `res/`, `libs/`, `.update/` and the WebView2 user-data
folder at `libs/`. The launcher therefore passes the true install root via `--app-root`, and the app uses
THAT as `IAppEnvironment.BaseDirectory`. Everything downstream (`GlobalPathService`, `UpdateService`,
`WebView2EnvironmentPrewarmer`) reads `BaseDirectory`, so this single override fixes all paths.

Full deep-dive: `docs/LAUNCHER_ARCHITECTURE.md` (authoritative). This rule is the invariant checklist.

## How to Apply

**Threading the install root (mandatory):**
- Launcher: `main.cpp` `MAIN_EXE = L"libs\\D3dxSkinManager.App.exe"`; `dotnet_runtime.cpp`
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

**Migration is driven by the OLD (immutable, already-released) launcher.** Its apply overlays the staged
payload then DELETES every path in the old manifest but not the new one — with NO launcher guard (that
code is already shipped and cannot be changed). The name `D3dxSkinManager.exe` was the APP in the old
manifest. So the invariant that actually makes migration safe is:

- **The build manifest LISTS `D3dxSkinManager.exe`** (`build-manifest.mjs` excludes ONLY the never-shipped
  legacy `d3dxskinmanager launcher.exe` + `manifest.json`). If the new manifest omitted the launcher, the
  OLD launcher's removal step would delete the freshly-copied new launcher — "the launcher removed itself"
  (the real bug hit on the first 4.0 migration). **Do NOT exclude `d3dxskinmanager.exe` from the manifest.**

**Updater guards** (`updater.cpp` `ApplyPendingUpdate`, in the NEW launcher — steady state + future updates):
1. **robocopy `/XF` excludes the RUNNING launcher's OWN name** (dynamic, `GetModuleFileNameW`). The launcher
   is now IN the manifest+staged, so without the self-exclude robocopy would try to overwrite the running
   launcher → fail on the locked exe. During migration the OLD launcher runs, so `/XF` excludes IT and the
   staged NEW `D3dxSkinManager.exe` copies.
2. **The removal step also skips both launcher basenames** — belt-and-suspenders (the manifest inclusion
   already keeps the launcher; this defends against a future manifest that wrongly omits it).
3. **Close-all before overlay** force-closes any running app-runtime process under the install
   (`libs/D3dxSkinManager.App.exe`, or old `D3dxSkinManager.exe`), skipping the launcher's own PID — a
   safety net for a hung instance so the exe unlocks for robocopy.

**Orphan cleanup by the LAUNCHER (not the app).** The new launcher deletes the orphaned
`D3dxSkinManager Launcher.exe` on boot — `RemoveLegacyLauncher` in `main.cpp` (best-effort, short retry
for the exit race, runs every boot). It runs first + before the app, and only ever deletes the
differently-named legacy launcher (never itself). There is NO app-level cleanup step for this.

**Build layout:**
- `build-production.ps1` + `.github/workflows/release.yml`: launcher → `{release}/D3dxSkinManager.exe`;
  runtime → `{release}/libs/D3dxSkinManager.App.exe` (MERGE into `libs/` — copy `libs\*`, not the folder,
  so `7z.dll` and the runtime coexist without a nested `libs\libs\`); `res/`, `manifest.json` at the root.
- `Launcher.vcxproj` `TargetName` = `D3dxSkinManager`. `UpdateService.LauncherExeName` = `D3dxSkinManager.exe`.

## Edge cases where it does NOT apply
- **Versions predating the two-phase updater cannot auto-migrate.** Verified 2026-07-12: 3.5→4.0
  migrates cleanly end-to-end (real binaries — new launcher lands byte-exact + is NOT deleted, runtime →
  `libs/`, orphan swept, `data/` at the install root). But 1.0 has NO `manifest.json`/`res/` and its
  launcher has no apply phase / no `--apply-and-exit`, so it can't self-update at all — a 1.0 user must
  MANUALLY reinstall 4.0. Only versions that already shipped the manifest-driven apply (3.x) auto-migrate.
- **Self-contained build** (`-SelfContained`): no launcher, runtime stays at the root as
  `D3dxSkinManager.exe`, `--app-root` unnecessary (BaseDirectory is the root). No `libs/` move.
- **Dev/debug run** (`app-dev.mjs`): the app is launched directly (no launcher, no `--app-root`) → falls
  back to `bin/Debug` BaseDirectory. `project.config.mjs.exe` stays the debug path — the `libs/` topology is
  production-packaging only.

## Verify
- C#: `AppRootArgTests`, `SingleInstanceGuardTests`.
- Launcher end-to-end: `node devtools/dev.mjs test-update-apply` (two scenarios: new self-update +
  old→new migration; both list the launcher in the manifest) — requires the launcher rebuilt (output
  `D3dxSkinManager.exe`). The C++ compile + real Windows migration must be done locally / on CI (can't
  build C++ in the agent sandbox). Real migration path: install a prior release (e.g. 3.5) + run its
  auto-update to the new-topology release.

## Related
- `background-task-tracking.md` (the update download is a fire-and-forget ProcessRegistry op)
- `use-project-paths.md` (all install paths via `IAppEnvironment`/path services, never raw)
