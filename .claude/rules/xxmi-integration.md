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

## Why this matters for design
- Don't reinvent XXMI's job (installing importers, injecting, launching). Lean on it.
- Frame the app as "organize + fix + deploy your mod library → XXMI runs it."
- Work-dir / mod-path UX should make pointing at the XXMI `Mods/` folder obvious (future: auto-detect
  `%AppData%\XXMI Launcher\<IMPORTER>\Mods`).
- Game-agnostic: same flow for every importer (ZZZ/Endfield/Genshin/…); never hard-code one game.
