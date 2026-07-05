# XXMI integration — how this app fits the XXMI / Model Importer ecosystem

Research-grounded model (sources: XXMI-Launcher repo + wiki, 2026-06-18). This app is **complementary
to XXMI**, not a replacement. Design launch/deploy/tooling around this picture.

## What XXMI is
**XXMI Launcher** (SpectrumQT) is the dominant, user-recommended tool for modding 3DMigoto-based games.
It:
- Installs + auto-updates a **Model Importer** per game: **GIMI** (Genshin), **SRMI** (Star Rail),
  **WWMI** (Wuthering Waves), **ZZMI** (Zenless Zone Zero), **HIMI** (Honkai Impact), **EFMI** (Endfield).
- Bundles a custom **3DMigoto DLL ("XXMI")** (auto-saving mod settings, UTF-8, buffer resizing, perf).
- **Launches the game with mod support injected.**
- Installs to e.g. `%AppData%\XXMI Launcher`; each importer instance has a **`Mods/` folder** it reads.

## The division of responsibility
| Concern | Owner |
|--------|-------|
| Game launch + 3DMigoto injection + in-game mod toggling | **XXMI** (the runtime) |
| Installing/updating the Model Importer + its DLL | **XXMI** |
| The deployed `Mods/` folder the importer reads | **XXMI** (this app writes into it) |
| Storing mods **compressed**, organizing, categorizing, fixing (hash-fix), previews, presets | **THIS app** |
| Extracting/deploying a selected mod **into** the importer's `Mods/` folder | **THIS app** |

So: **this app = mod library + deploy; XXMI = runtime + launch.** The app's per-profile **work dir
(`CacheModsDirectory` = `WorkDirectory/Mods`) is normally the XXMI importer's `Mods/` folder** (external
work-dir mode). Extracted/active mods land there; XXMI loads them; the game shows them.

## Launching via XXMI (for the Launch tab)
Headless quick-launch (no XXMI GUI), the canonical external-tool command:
```
"<XXMI install>\Resources\Bin\XXMI Launcher.exe" --nogui --xxmi <IMPORTER>
```
`<IMPORTER>` ∈ GIMI / SRMI / WWMI / ZZMI / HIMI / EFMI. Optional custom working dir via
`start "" /D "<dir>" "...XXMI Launcher.exe" --nogui --xxmi <IMPORTER>`.
- Per-game args may be required (e.g. **WWMI** needs `-DisableModule=streamline -dx11 -d3d11`).
- XXMI's own **Settings → Advanced → Custom Launch** controls what it then runs (game exe / external
  tool / Steam `-applaunch <id>`). Steam paths add ~10s.

**Design implication for our Launch tab:** the XXMI-native option is "pick `XXMI Launcher.exe` + choose
the importer → we run `--nogui --xxmi <IMPORTER>`", which is friendlier than asking for a raw command.
The generic "path + args" we have works (user types the exe + `--nogui --xxmi ZZMI`), but an
importer-aware mode is the better UX. The legacy raw-3DMigoto deploy/loader flow (D3DMigotoTab) is
de-emphasized because XXMI supersedes it.

## How the launcher works (from XXMI source — mechanics only, do NOT copy its UI)
`src/xxmi_launcher/core/` modules:
- **path_manager** — resolves the importer instance paths, incl. the `Mods/` folder.
- **package_manager** — downloads/installs/updates the Model Importer (the 3DMigoto + XXMI DLL package).
- **config_manager** — per-importer config: game path, **Custom Launch** command, engine/rendering
  overrides (e.g. WWMI dx11 enforcement), defaults for the XXMI DLL.
- **application** — the launch flow: pick importer → ensure package installed → apply config/overrides
  → inject the XXMI 3DMigoto DLL → start the game (or run the Custom Launch command). `--nogui --xxmi X`
  drives this headlessly.
- **mod_manager** — a **Mods-folder validator/optimizer** ("Optimize Mods"). It parses every mod `.ini`
  (sections, command lists, triggered key slots) and flags/repairs: `RogueIni` (stray/misplaced ini),
  `UnwantedFile` (junk), `UnwantedTrigger` / `GlobalTrigger` (conflicting/global keybind triggers it
  neutralizes). This operates on the **deployed** `Mods/` folder at runtime.

**Implication:** XXMI already validates/optimizes the *deployed* Mods folder and owns install/launch.
This app should NOT duplicate that. Our complementary value: a compressed **library**, organization
(categories/tags/presets), **hash-fixing** (different concern from XXMI's trigger/junk optimizer — ours
re-fixes hashes after a game update), previews, and **deploying** chosen mods into the Mods folder.

## Real install layout (verified on disk, 2026-06-18)
`E:\Mods\XXMI-Launcher\` (the folder the user picks):
- `XXMI Launcher Config.json` — source of truth. `Launcher.active_importer`, `Launcher.enabled_importers`,
  and `Importers.<NAME>.Importer.{importer_folder, game_folder}`.
- `Resources\Bin\XXMI Launcher.exe` — the launcher binary (what we run to launch).
- `<IMPORTER>\` per importer (e.g. `ZZMI\`, `EFMI\`) = a self-contained 3DMigoto: `d3d11.dll`,
  `d3dx.ini`, `Core\`, `ShaderFixes\`, `ShaderCache\`, and **`Mods\`** (deploy target).
- `d3dx.ini` has `[Loader] loader = XXMI Launcher.exe`; per-importer `custom_launch_inject_mode` =
  `Hook` or `Inject`. Launch = XXMI injects that DLL into the game — NOT a plain exec.

## How THIS app integrates it (implemented 2026-06-18)
- **Backend** `Launch/Services/XxmiService.DetectAsync(folder)` (IPC `LAUNCH_XXMI_DETECT`) parses the
  config → `XxmiDetectResult { found, launcherExe, configPath, importers[] }`; each `XxmiImporter` has
  `importerDir` (trailing-sep trimmed), `modsDir` (= importerDir\Mods), `gameFolder`, `isActive`,
  `isInstalled`. Read-only; throws `XXMI_CONFIG_NOT_FOUND`. Accepts the exe/lnk path too (walks up).
- **`ModWork.Mode` is a first-class 3-value type: `internal` | `external` | `xxmi`.** `xxmi` is its own
  type (NOT "external"), but resolves its path the same way — `ModWorkConfiguration.IsExternal()` returns
  true for both `external` and `xxmi`, so `ProfilePathService.WorkDirectory` uses `Directory` for either.
  `ProfileFacade` UPDATE_CONFIG stores `Directory` for both. Frontend `ModWorkConfiguration.mode` union +
  `settingsOps.saveProfileConfig` treat external/xxmi the same (persist+validate the dir).
- **`ModWorkSettingsTab` work-dir = a `Segmented` bound DIRECTLY to `workMode`** (App default=internal /
  XXMI Launcher=xxmi / Custom folder=external). No derivation — the saved mode IS the source. Each mode
  reveals its control: internal→readonly internal path; xxmi→`XxmiImporterPicker`; external→manual path+browse.
  (2026-07-05: SettingsView flattened to 4 tabs — ModWorkSettingsTab / ModImportSettingsTab /
  FixToolSettingsCard / GlobalSettingsTab; the old ProfileSettingsTab was split and removed.)
- **Importer list is DISCOVERED FROM DISK, not a fixed game set.** `XxmiService.DetectAsync` scans the
  root's top-level subfolders and treats any with importer markers (`Mods\` dir, or `d3dx.ini`/`d3d11.dll`
  — see `LooksLikeImporter`) as an importer; config only *enriches* (active flag, game_folder). So custom
  / future importers appear and config-only-uninstalled games don't. Two-level pick: choose root folder
  (选择文件夹) → choose importer sub (dropdown).
- **One pick in the XXMI source sets BOTH locations. There is no Launch tab.** The picker lives in
  **Settings → Mod Work** (`src/modules/setting/components/XxmiImporterPicker.tsx`).
  Choosing an importer stages a **ConfirmDialog** (work dir / deploy target / launcher / launch args —
  B5 UX fix, no more silent instant-apply); confirming saves
  `updateProfileConfig({ workMode:'xxmi', workDirectory:<importerDir>, launchPath:<launcherExe>,
  launchArgs:'--nogui --xxmi <NAME>' })` → `CacheModsDirectory` becomes `<importerDir>\Mods` AND the
  launch command is set. It then re-baselines the settings store so the form isn't left dirty.
- **Picker/state indicators (2026-07-05):** the picker shows a "Reading XXMI configuration…" line while
  `DetectAsync` runs (auto-detect never toasts — inline warning on failure), a green "detected — N
  importers" result, and a **Bound / Not-applied** `StatusTag` comparing the selection against the SAVED
  baseline dir. Under it, `ModWorkSettingsTab` renders a **binding summary** (`KeyValueRows`, boxed):
  work dir, deploy target, and — when detect covers the bound importer — game folder + config path.
- **Launch command is user-editable:** `ModWorkSettingsTab` has a "Game launch" field (path + browse +
  args) persisted via `updateProfileConfig` launchPath/launchArgs (sent only when changed; empty string
  clears, omitted preserves). Store mirrors it (`launchPath`/`launchArgs` + `initialLaunchConfig`
  baseline; `setLaunchConfig` re-baselines on load/bind/save).
- **Launching** is the **status-bar `LaunchButton`** only (runs `launch.path` + `launch.args`). The
  Launch nav tab + `LaunchView`/`GameLaunchTab`/`D3DMigotoTab` were **removed** — config belongs with
  the setting it drives, not a separate tab.
- The own-3DMigoto route (`D3DMigotoService` backend) stays parked — it would require replicating XXMI's
  inject. Its UI (the legacy 3DMigoto tab) is gone.

## Why this matters for design
- Don't reinvent XXMI's job (installing importers, injecting, launching). Lean on it.
- Frame the app as "organize + fix + deploy your mod library → XXMI runs it."
- Work-dir / mod-path UX should make pointing at the XXMI `Mods/` folder obvious (future: auto-detect
  `%AppData%\XXMI Launcher\<IMPORTER>\Mods`).
- Game-agnostic: same flow for every importer (ZZZ/Endfield/Genshin/…); never hard-code one game.
