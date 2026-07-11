# D3dxSkinManager Launcher Architecture

## Overview

D3dxSkinManager uses a native C++ launcher to provide automatic .NET runtime installation and auto-update capabilities while keeping the download size small.

## Architecture

**Topology (v4.0+):** the launcher IS the top-level `D3dxSkinManager.exe` the user runs; the .NET runtime
moved into `lib/D3dxSkinManager.App.exe`. The launcher passes its own directory (the install root) to the
app via `--app-root` so every install-relative path resolves against the install root, not `lib/`.
(Before v4.0 the runtime was `D3dxSkinManager.exe` at the root and the launcher was
`D3dxSkinManager Launcher.exe` — see [Migration](#migration-old-topology--new-topology).)

```
{install}/
┌───────────────────────────────────────────┐
│  D3dxSkinManager.exe                      │  ← user runs THIS (native C++ launcher, ~336KB)
│  (Native C++ Launcher)                    │
│                                           │
│  - Apply any staged update (updater.cpp)  │
│  - Check for .NET 10 runtime, auto-install│
│  - Launch lib/D3dxSkinManager.App.exe     │
│    with  --app-root "{install}"           │
└──────────────┬────────────────────────────┘
               │  CreateProcess: lib\D3dxSkinManager.App.exe --app-root "{install}"
               ▼
┌───────────────────────────────────────────┐
│  lib/D3dxSkinManager.App.exe              │  ← the .NET 10 app (~12MB, single-file, Costura-merged)
│  (.NET 10 Application)                     │
│                                           │
│  - Reads --app-root → BaseDirectory       │
│  - Single-instance guard (per install)    │
│  - Main application logic + embedded web  │
└───────────────────────────────────────────┘
   data/  res/  libs/7z.dll  manifest.json      ← at the INSTALL ROOT (resolved via --app-root)
```

## Components

### 1. C++ Launcher (`D3dxSkinManager.exe`)

**Size:** ~336KB (native, statically linked)

**Responsibilities:**
- Detect if .NET 10 Desktop Runtime is installed
- Download and silently install .NET 10 if missing
- Check for application updates (to be implemented)
- Launch the main .NET application (`D3dxSkinManager.exe`)
- Handle launcher self-updates (future)

**Technology:**
- Native C++ (no dependencies)
- Windows API for runtime detection
- URLMon for downloading .NET installer
- Process creation for launching .NET app directly (not via `dotnet.exe`)

**Source:** `D3dxSkinManager.Launcher/`

**Key Files:**
- `main.cpp` - Entry point and launch flow
- `dotnet_runtime.cpp` - Runtime detection and installation
- `updater.cpp` - Auto-update logic (manifest-driven; implemented)
- `launcher.rc` - Windows resource file (icon, version info)
- `favicon.ico` - Application icon
- `Launcher.vcxproj` - Visual Studio C++ project

### 2. Main Application (`lib/D3dxSkinManager.App.exe`)

**Size:** ~12MB (framework-dependent, single-file, Costura-merged)

**Responsibilities:**
- All application logic
- UI rendering (React + WebView2)
- Mod management
- Database operations
- Plugin system

**Technology:**
- .NET 10 (framework-dependent)
- Costura.Fody merges all managed DLLs into single executable
- Single-file with embedded resources

**Source:** `D3dxSkinManager/`

### 3. Additional Files

- `data/languages/*.json` - Translation files (user-editable)
- `libs/7z.dll` - Native 7-Zip library for fast extraction

## Build Process

### Default Build (Framework-Dependent with Launcher)

```powershell
.\build-production.ps1
```

**Output:**
```
publish/
  win-x64/
    D3dxSkinManager.exe            (~336KB - C++ launcher; user runs this)
    lib/
      D3dxSkinManager.App.exe      (~12MB  - Main app, single-file, Costura-merged)
    res/
      languages/{cn,en}.json       (shipped translations)
      remote-sources/*.json        (remote-library site seeds)
    libs/
      7z.dll                       (~1.9MB - Native 7z library)
    manifest.json                  (auto-update file list)
```

**Total Size:** ~14 MB

### Self-Contained Build (No Launcher Needed)

```powershell
.\build-production.ps1 -SelfContained $true
```

**Output:**
```
publish/
  win-x64/
    D3dxSkinManager.exe   (~150MB - Includes .NET runtime)
    data/
      languages/*.json
    libs/
      7z.dll
```

**Total Size:** ~150-160 MB

## Runtime Flow

### First Launch (No .NET Runtime)

1. User double-clicks `D3dxSkinManager.exe` (the launcher)
2. Launcher checks for .NET 10 runtime
3. Runtime not found → Shows dialog asking to install
4. User clicks "Yes"
5. Launcher downloads .NET 10 installer (~50MB)
6. Installer runs silently in background
7. Installation completes
8. Launcher verifies installation
9. Launcher executes `lib\D3dxSkinManager.App.exe --app-root "{install}"`
10. Main application starts (resolves data/res/libs/.update against the install root)

### Subsequent Launches

1. User double-clicks `D3dxSkinManager.exe` (the launcher)
2. Launcher applies any staged update (updater.cpp), then checks for .NET 10 runtime
3. Runtime found → Skip to step 4
4. Launcher executes `lib\D3dxSkinManager.App.exe --app-root "{install}"`
5. Main application starts; its single-instance guard refocuses an already-running instance and exits

## Auto-Update Architecture (IMPLEMENTED — two-phase)

A running .NET app can't replace its own exe (file lock), so the update is split: the **app downloads
+ stages**, the **launcher applies** on the next startup. The updater only runs when there is work —
no GitHub check or prompt on every boot.

### Phase 1 — App downloads + stages (`UpdateService`, System module)

User-requested (Settings → "Check for updates") or opt-in startup auto-check:
1. `CheckForUpdateAsync` — query GitHub releases, compare version, compute the manifest changeset
   (count + download size) for the update screen.
2. On "Download" → `DownloadUpdateAsync` (fire-and-forget, progress via `ProcessRegistry` → Activity
   panel): download `D3dxSkinManager-v<ver>-win-x64.zip` (stable `releases/latest/download/` redirect),
   extract to `{install}/.update/staged`, **verify every staged file's sha256 against the staged
   manifest** (a mismatch aborts the stage — the launcher never applies a corrupt download), then write
   `{install}/.update/ready.json`.
3. The update screen flips to "Update downloaded — restart to apply."

### Phase 2 — Launcher applies (`updater.cpp`, `ApplyPendingUpdate`)

Runs first in `main.cpp`, before the app starts. **No network, no prompt:**
1. No `{install}/.update/ready.json` → no-op (the common case).
2. **Close all instances:** force-close any running app-runtime process under the install
   (`{install}/lib/D3dxSkinManager.App.exe`, or the old `{install}/D3dxSkinManager.exe` during a
   migration), skipping the launcher's own PID — a safety net for a hung instance so the exe unlocks
   for the overlay (single-instance + the graceful pre-restart exit already handle the normal case).
3. Overlay `{install}/.update/staged` onto the install with `robocopy /E /XF "<own exe name>"` — replace/
   add every file EXCEPT the **launcher's own running image** (computed dynamically via
   `GetModuleFileNameW`, NOT hardcoded). Excluding only the *own* name is deliberate: during a migration
   the OLD launcher is running, so the staged NEW launcher (`D3dxSkinManager.exe`, a different name) still
   copies.
4. **Removals:** files in the old manifest but not the new one are deleted (only tracked files; the old +
   new `manifest.json` are read before the overlay overwrites it) — **except any launcher basename**
   (`D3dxSkinManager.exe` and the legacy `D3dxSkinManager Launcher.exe`). This guard is what makes the
   migration safe: `D3dxSkinManager.exe` flips from *app* (in the old manifest) to *launcher* (absent from
   the new manifest), so without it the diff would delete the just-copied new launcher.
5. Clear `{install}/.update`, then launch the launcher (which starts `lib/D3dxSkinManager.App.exe`).

Non-fatal throughout: any failure falls back to launching the current version. **The launcher never
replaces itself.** The app sha256-verifies every staged file (against the staged manifest) before
writing ready.json, so the launcher only ever applies a fully-verified stage.

### Testing the flow

- **Backend** (`UpdateServiceTests`): staged-update state (ready.json) + sha256 verification
  (match / tamper / missing file / missing manifest).
- **Manifest diff** (`ManifestDiffTests`): added / updated / removed + download size.
- **Frontend** (`UpdateDialog.test`): dialog phases — check → available → download → ready, plus
  ready-on-open and prefetched/failed.
- **Launcher apply, end-to-end** (`node devtools/dev.mjs test-update-apply`): runs the REAL launcher with
  `--apply-and-exit` over two sandbox scenarios — (1) a **new self-update** (asserts runtime/data overlay +
  removal + that the running launcher is NOT overwritten by the /XF self-exclude) and (2) the **old→new
  migration** (asserts the new launcher lands at `D3dxSkinManager.exe`, the runtime lands in `lib/`, and the
  removal guard does NOT delete the just-landed launcher). Requires the launcher rebuilt (output name
  `D3dxSkinManager.exe`).

### Benefits

- Launcher remains unchanged (stable update mechanism)
- Only application code gets updated
- Smaller update downloads (only ~10-12MB)
- Atomic updates (no partial state)
- Rollback capability

### Update Package Structure

```
update-1.1.0.zip
  D3dxSkinManager.exe   (new version, Costura-merged)
  data/
    languages/*.json    (updated translations)
  libs/
    7z.dll              (if needed)
  version.json          (metadata)
```

### Update Manifest (differential updates)

Every release publishes a **`manifest.json`** — both inside the zip (the installed baseline) and as a
standalone release asset (so the updater can fetch just the manifest without the whole zip). It lists
every auto-updatable file with its relative path, byte size, and sha256:

```json
{
  "version": "2.5",
  "generatedAt": "2026-06-19T05:41:14.057Z",
  "files": [
    { "path": "D3dxSkinManager.exe",        "size": 14694798, "sha256": "75452156…" },
    { "path": "data/languages/en.json",     "size": 59800,    "sha256": "72f3f274…" },
    { "path": "libs/7z.dll",                 "size": 1908736,  "sha256": "bbd705e3…" }
  ]
}
```

- **The launcher exe is NEVER listed** — it does not auto-update (it is the stable applier). The build
  manifest excludes both the current launcher basename (`D3dxSkinManager.exe`) and the legacy
  `D3dxSkinManager Launcher.exe` (`devtools/scripts/build-manifest.mjs` `EXCLUDE_BASENAMES`). The runtime
  `lib/D3dxSkinManager.App.exe` IS listed — it is the app, and it is what auto-updates.
- **Diff** the installed manifest against a release manifest to get the changeset:
  - *Added* = path in release, not installed → download.
  - *Updated* = path in both, sha256 differs → download + replace.
  - *Removed* = path installed, not in release → delete.
- Only changed files are downloaded → minimal update size.

**Generation:** `node devtools/dev.mjs manifest <payloadDir> <version> [outFile]`
(`devtools/scripts/build-manifest.mjs`). Wired into `.github/workflows/release.yml` (Step 10) — runs
over the packaged `release/win-x64/` and publishes `manifest.json` alongside the zip.

**Consumption:** the .NET app's `UpdateService` (System module) fetches the release manifest asset,
diffs it against the local `manifest.json` (next to the running exe), and reports the changed-file
count + download size in the in-app update dialog. The C++ launcher applies the changeset on restart
(it can replace files the running app holds open; the running .NET process cannot replace its own exe).
The diff model + pure `ManifestDiff.Compute` live in `Modules/System/Models/UpdateManifest.cs`.

## Single-Instance Enforcement

The app is **not multi-instance-safe** (each profile is a single-writer SQLite DB, the mod-cache
`FileOperationPlanner` only serializes ops WITHIN one process, and the WebView2 user-data folder is a
single OS lock). `SingleInstanceGuard` (runs FIRST in `ApplicationBootstrapper.Run`, before the WebView2
prewarm) takes a named `Local\` Mutex keyed by the install dir. A 2nd launch of the same install
broadcasts a per-install activation message and exits; the running instance's main form catches it and
comes to the foreground. **Keyed per install** so distinct installs still run side-by-side. The Mutex is
released when the process exits (normal quit or an update restart), so the relaunched instance re-acquires
cleanly — the update restart exits the old process before the launcher relaunches, so there is no overlap.

## Migration (old topology → new topology)

Installs from **before v4.0** have `D3dxSkinManager.exe` = the runtime and `D3dxSkinManager Launcher.exe`
= the launcher. Auto-updating to v4.0+ migrates the layout in a single apply, driven by the OLD launcher
already on disk:

1. The old app stages the new-topology payload (`D3dxSkinManager.exe` = new launcher,
   `lib/D3dxSkinManager.App.exe` = runtime) and restarts via its own `D3dxSkinManager Launcher.exe`.
2. That old launcher's apply overlays staged → install. `/XF` excludes only its **own** running name
   (`D3dxSkinManager Launcher.exe`), so the staged `D3dxSkinManager.exe` (new launcher) DOES copy over the
   old runtime, and `lib/D3dxSkinManager.App.exe` is added.
3. The removal step would flag `D3dxSkinManager.exe` for deletion (app→launcher role flip across the
   manifests); the **launcher-basename guard** prevents it, so the new launcher survives.
4. The old launcher then starts `D3dxSkinManager.exe` (now the new launcher), which starts the runtime.
   A harmless one-time double-hop; subsequent launches go straight through the new launcher.
5. The orphaned `D3dxSkinManager Launcher.exe` is deleted on the next boot by the app's
   `LegacyLauncherCleanupStep` (only once the new `D3dxSkinManager.exe` launcher is present).

## Advantages

### Over Self-Contained

- **93% smaller download** (12MB vs 150MB)
- Shares .NET runtime with other apps
- Faster updates (only app code, not runtime)
- Users benefit from .NET security updates automatically

### Over Framework-Dependent Without Launcher

- **No manual .NET installation required**
- Seamless first-time user experience
- Auto-update capability
- Professional installer-like experience

## Building the Launcher

### Requirements

- **Local Development:** Visual Studio 2025+ (v145 toolset) or Visual Studio 2022 (v143 toolset)
- **CI/CD (GitHub Actions):** Visual Studio 2022 (v143 toolset) - automatically configured
- Windows 10 SDK
- MSBuild

### Platform Toolset Configuration

The launcher project uses **conditional toolset selection** to support both modern local development and CI/CD environments:

```xml
<!-- Launcher.vcxproj -->
<PlatformToolset Condition="'$(CI)'=='true'">v143</PlatformToolset>
<PlatformToolset Condition="'$(CI)'!='true'">v145</PlatformToolset>
```

**How it works:**

| Environment | Toolset | Visual Studio Version |
|-------------|---------|----------------------|
| **Local Development** | v145 | VS 2025+ (recommended) |
| **GitHub Actions CI** | v143 | VS 2022 (windows-latest runner) |

**Why?**
- v145 (VS 2025+) provides latest C++ features and optimizations for local development
- v143 (VS 2022) is required for GitHub Actions `windows-latest` runners
- Conditional toolset allows seamless builds in both environments
- No manual configuration needed - automatically detects CI environment via `CI=true` variable

**For CI/CD:**
The GitHub Actions workflow automatically sets `CI=true`:
```yaml
- name: Build C++ Launcher (x64)
  env:
    CI: true  # Use v143 toolset for GitHub Actions compatibility
  run: msbuild Launcher.vcxproj /p:Configuration=Release /p:Platform=x64
```

### Manual Build

```powershell
cd D3dxSkinManager.Launcher
.\build.ps1 -Platform x64
```

### Integrated Build

The main build script automatically builds the launcher:

```powershell
# Build everything (default)
.\build-production.ps1

# Skip launcher build
.\build-production.ps1 -SkipBootstrapper $true
```

## Deployment

### Recommended Distribution

1. **Primary Distribution:** Framework-dependent with C++ launcher
   - Smallest download size
   - Best for most users
   - Auto-installs .NET on first run

2. **Alternative Distribution:** Self-contained (optional)
   - Larger download
   - For users in restricted environments
   - No internet connection needed

### Distribution Platforms

- **Direct Download:** Host both versions on your website
- **GitHub Releases:** Attach both zip files
- **Package Managers:** Consider chocolatey, winget, etc.

## Development Notes

### Testing Launcher Without .NET

To test the .NET installation flow:

1. Temporarily rename your .NET installation
2. Run the launcher
3. Verify installation prompts work
4. Restore .NET installation after testing

### Debugging the Launcher

The C++ launcher can be debugged with Visual Studio:

1. Open `D3dxSkinManager.Launcher\Launcher.vcxproj`
2. Set breakpoints in launcher code
3. Press F5 to debug

### Modifying Launcher Behavior

Edit these files:
- `main.cpp` - Entry point and main flow
- `dotnet_runtime.cpp` - .NET detection and installation
- `updater.cpp` - Auto-update logic (future)
- `launcher.rc` - Icon and version information
- `favicon.ico` - Application icon (174KB)

## Future Enhancements

- [x] Implement auto-update functionality (manifest-driven, two-phase)
- [x] sha256-verify downloaded update files before applying (app verifies the stage)
- [ ] Add crash reporting to launcher
- [ ] Launcher self-update capability
- [ ] Telemetry (opt-in)
- [ ] Download progress UI
- [ ] Multiple runtime version support
- [ ] Offline installer bundling option

## FAQ

**Q: Why not use a .NET launcher?**
A: A .NET launcher would require .NET to already be installed, defeating the purpose. Native C++ requires no dependencies.

**Q: Can I distribute without the launcher?**
A: Yes, use `-SkipBootstrapper $true` or `-SelfContained $true` when building.

**Q: What if .NET installation fails?**
A: The launcher shows an error and provides a manual download link.

**Q: Does the launcher work offline?**
A: The launcher requires internet to download .NET if it's not installed. Consider self-contained builds for offline scenarios.

**Q: Can I customize the launcher?**
A: Yes, the launcher is open source and can be modified. Edit the C++ files in `D3dxSkinManager.Launcher/`.

---

Last Updated: 2026-07-11 (v4.0 topology: launcher = D3dxSkinManager.exe, runtime = lib/D3dxSkinManager.App.exe, --app-root, single-instance, migration + close-all)
