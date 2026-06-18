# 3DMigoto mod `.ini` interface (for the mod `.ini` editor #3 + future mod-merge)

Verified from real ZZMI character-mod `.ini`s on disk (2026-06-18). 3DMigoto is the runtime every
importer (GIMI/ZZMI/EFMI/…) wraps; mods are driven entirely by `.ini` files + resource binaries. This
is the interface our `.ini` editor edits and a future **mod-merge** must reason about. Game-agnostic.

## Section types (what a mod `.ini` contains)

| Section | Purpose | Editable surface |
|---------|---------|------------------|
| `[Constants]` | Declares vars: `global [persist] $name = N`. `persist` = saved across game sessions. Holds toggle/state (e.g. `$swapkey0`, `$hair`). | Default values |
| `[Present]` | Runs every frame (post-processing of vars). | rarely |
| `[Key*]` | A keybinding/toggle. Lines: `key = <vk>`, `condition = $x == 1`, `type = cycle\|hold\|toggle`, and `$var = a,b,…` (the values it cycles). | **key, type, condition, cycle values — the primary editor target** |
| `[TextureOverride*]` | Binds a `hash` to overrides + a command list (`vb0=`, `override_byte_stride`, `handling=skip`, `draw=…`, `$active=1`). The actual mod mesh/texture swap. | hashes are tool-generated — do NOT hand-edit; only the fix tools regenerate them |
| `[ShaderOverride*]` / `[ShaderRegex*]` | Bind shader hashes / patterns to command lists. | rarely |
| `[Resource*]` | Declares a resource (buffer/texture file) referenced by overrides. | no |

Key format details: `key` uses VK names or chars (`0`, `VK_F1`, `no_ctrl alt j` — space-separated combo,
`no_` = must-not-be-pressed). `type=cycle` + `$var = 1,0` cycles the var through the list on each press.
Comments start `;` (e.g. `;MARK:Key----`). Namespacing is per-file (the filename stem).

## What the editor (#3) should expose — and NOT
- **Edit:** `[Key*]` (rebind `key`, change `type`, edit cycle values / condition) and `[Constants]`
  default values. These are the safe, user-meaningful knobs. Generalize to "edit any `key = value` line"
  but **gate the override/hash sections** behind an "advanced" view — editing a `hash` breaks the mod.
- **Write-back: DONE.** `ModKeybindingService.UpdateKeybindingAsync` (rebind `[Key*]` key) and the
  general **`ModIniService`** (config editor) both do line-level edits + persist via the **fast
  single-file archive patch** (`UpdateFileInArchiveAsync`, NOT a full recompress — see
  `filesystem-operation-serialization.md`). Comments/order/indentation preserved (regex line rewrite).
- **General config editor (`ModIniService`, IPC `GET_INI_FILES` / `UPDATE_INI_ENTRY`, UI
  `ModIniEditor`):** parses every `.ini` → sections → `key=value` entries, each classified
  **editable** (`[Key*]` + `[Constants]` tunables) vs **read-only** (`advancedSection` = any
  `*Override`/`Resource`/`Shader*`/`CommandList*`/`Present`/etc., or `command` = a `run=`/draw line).
  `UpdateEntryAsync` **re-classifies server-side and refuses locked lines** (`INI_ENTRY_READONLY`) —
  the UI gate is not trusted; it also path-contains the target under the cache dir. Parses the
  `namespace = X` directive per file. UI = slide-in, left tab per file (own scroll, equal height to
  the editor pane), friendly labels, `type`→cycle/hold/toggle Select, advanced plumbing collapsed.
  **A `$var` default is NOT a boolean** — its value cycles through the values its `[Key*]` defines, so
  render it as a plain field, never an on/off Switch. Tests: `ModIniServiceTests` (8).

## Command lists + control flow (verified from real ZZMI mod `.ini`s, 2026-06-18)
Sections like `[Present]`, `[Constants]`, `[TextureOverride*]`, `[CommandList*]`, `[CustomShader*]` run
an ordered **command list**. Commands seen in the wild:
- `run = CommandListSkinTexture` / `run = CustomShaderTransparency1` — call a named list/shader.
- **control flow**: `if $swapkey0 == 1` / `elif …` / `else` / `endif` (drives variants by `$var`).
- `$var = a,b,c` (cycle list) or `$var = 1` (assign); `draw = <count>,<off>`; `drawindexed = <i>,<c>,<o>`.
- resource binds: `vb0 = Resource…`, `ps-t0 = …`, `handling = skip`.
- **Comments: `;` OR the fullwidth `；`** — a parser MUST treat both as comments (real mods mix them,
  e.g. `；drawindexed = …`). Don't choke on non-ASCII; files are UTF-8 with CJK names/“credit” spam lines.

## Mod-merge — the GIMI/XXMI merger pattern (verified from a real `*_Merged/Master*.ini`)
A merge is one mod whose master `.ini` starts with **`namespace = MergeName\Master`** and:
- `[Constants]`: `global persist $swapvarZ = 0` (the variant selector) + `global $active`.
- `[KeySwap]`: `key = …`, `type = cycle`, `$swapvarZ = 0,1,2` (cycles through merged variants).
- each merged source's `[TextureOverride*]` is gated (`$active = 1` / `if $swapvarZ == N`) and lives under
  its own namespace; cross-refs use `\namespace\Section`. The merger (GIMI's script) re-namespaces every
  mod so hashes/keys/resources don't collide, then a single key cycles between them.
So **mod-merge = re-namespace each mod + emit a master with a `$swapvar` + `[KeySwap]` cycling them**.
This is the model to implement (game-agnostic — every importer's 3DMigoto supports `namespace`).

## Still flagged (needs more research — wiki scrape was JS-blocked)
- **Generate in-game toggle UI / on-screen menu**: not found in the sampled mods (they cycle via keys, no
  text overlay). 3DMigoto/importers have a text-draw/`[Present]` overlay + a mod-menu; map it from the
  3Dmigoto wiki (fetch was blocked — retry via a working fetch or the repo source) before generating it.
- **DLL plugin interface**: XXMI bundles its 3DMigoto DLL; the `[Loader] loader=XXMI Launcher.exe` hook is
  in `xxmi-integration.md`. The plugin-DLL API itself is not yet mapped.

## Where this lives in our code
- Parse today: `Modules/Mod/Services/ModKeybindingService.cs` (`ParseKeybindingsAsync`, `[Key*]` only).
- `.ini`s live in the extracted cache (`CacheModsDirectory/{id}` or `DISABLED-{id}`), recompressed into
  the archive (source of truth) — see `filesystem-operation-serialization.md` + `use-project-paths.md`.
