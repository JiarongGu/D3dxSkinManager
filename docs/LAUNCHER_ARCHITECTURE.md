# D3dxSkinManager Launcher Architecture

## Overview

D3dxSkinManager uses a native C++ launcher to provide automatic .NET runtime installation and auto-update capabilities while keeping the download size small.

## Architecture

```
┌───────────────────────────────────────────┐
│  D3dxSkinManager Launcher.exe             │
│  (Native C++ Launcher - ~336KB)           │
│                                           │
│  - Check for .NET 10 runtime              │
│  - Auto-install if missing                │
│  - Check for updates (future)             │
│  - Launch main application                │
└──────────────┬────────────────────────────┘
               │
               │ Executes: D3dxSkinManager.exe
               │
               ▼
┌───────────────────────────────────────────┐
│  D3dxSkinManager.exe                      │
│  (.NET 10 Application - ~12MB)            │
│  (Costura-merged single file)             │
│                                           │
│  - Main application logic                 │
│  - Embedded web resources                 │
│  - Embedded managed DLLs (via Costura)    │
└───────────────────────────────────────────┘
```

## Components

### 1. C++ Launcher (`D3dxSkinManager Launcher.exe`)

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

### 2. Main Application (`D3dxSkinManager.exe`)

**Size:** ~12MB (framework-dependent, Costura-merged)

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
    D3dxSkinManager Launcher.exe   (~336KB - C++ launcher)
    D3dxSkinManager.exe            (~12MB  - Main app, Costura-merged)
    data/
      languages/
        cn.json
        en.json
    libs/
      7z.dll                       (~1.9MB - Native 7z library)
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

1. User double-clicks `D3dxSkinManager Launcher.exe`
2. Launcher checks for .NET 10 runtime
3. Runtime not found → Shows dialog asking to install
4. User clicks "Yes"
5. Launcher downloads .NET 10 installer (~50MB)
6. Installer runs silently in background
7. Installation completes
8. Launcher verifies installation
9. Launcher executes `D3dxSkinManager.exe` directly (Costura-merged exe)
10. Main application starts

### Subsequent Launches

1. User double-clicks `D3dxSkinManager Launcher.exe`
2. Launcher checks for .NET 10 runtime
3. Runtime found → Skip to step 4
4. Launcher checks for updates (future feature)
5. Launcher executes `D3dxSkinManager.exe` directly
6. Main application starts

## Auto-Update Architecture (IMPLEMENTED — `updater.cpp`)

The C++ launcher applies updates before launching the app (a running app can't replace its own exe):

### Update Flow (manifest-driven)

`CheckForUpdates(appDir)` runs first in `main.cpp`:
1. Read the local `manifest.json` version (skip if absent — older build).
2. Download the latest release `manifest.json` via GitHub's stable
   `releases/latest/download/<asset>` redirect (`URLDownloadToFileW`, urlmon — no API/JSON-lib needed).
3. Compare versions (numeric `X.Y[.Z]`). Not newer → return, launch current.
4. Newer → **prompt the user** (Yes/No). On consent:
   - Download `D3dxSkinManager-v<ver>-win-x64.zip` (asset name embeds the version).
   - Extract via PowerShell `Expand-Archive` (no external zip lib).
   - **Overlay** the staged files with `robocopy /E /XF "D3dxSkinManager Launcher.exe"** — every
     file replaced/added EXCEPT the launcher itself (it's running).
   - **Removals:** files in the old manifest but not the new one are deleted (only tracked files).
   - The new `manifest.json` is copied in (becomes the next baseline).
5. Launch the now-updated `D3dxSkinManager.exe`.

Non-fatal throughout: any failure (offline, no manifest, download/extract error) falls back to
launching the current version. **The launcher never replaces itself.** sha256 verification of
downloaded files is a future hardening step (the zip is fetched over GitHub https).

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

- **The launcher exe is NEVER listed** — it does not auto-update (it is the stable applier).
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

- [x] Implement auto-update functionality (manifest-driven, `updater.cpp`)
- [ ] sha256-verify downloaded update files before applying
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

Last Updated: 2026-03-05
