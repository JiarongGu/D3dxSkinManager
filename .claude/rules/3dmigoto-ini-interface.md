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
- **Write-back:** our `ModKeybindingService` currently only PARSES `[Key*]` (key/type/$var). #3 adds
  write-back: rewrite the specific line in the cached `.ini`, then **recompress into the archive** (same
  pattern as `ModFixService`/`ModImportService.UpdateModAsync` — stage in `_profilePaths.TempDirectory`,
  recompress, replace via planner). Preserve comments/order (line-level edit, not a full re-serialize).

## Mod-merge (future — why the interface matters)
Merging mod A+B = concatenate their sections into one mod under **distinct namespaces** so hashes/keys/
resources don't collide: 3DMigoto supports `namespace` + `\ns\Section` references. A merge tool would:
parse both, detect key/hash collisions, re-namespace, remap `$var`/`Resource`/`CommandList` references,
and write a combined `.ini`. The `[Key*]`/`[Constants]`/`[Resource*]` graph above is what it rewires.

## Advanced phase (after research) — flagged capabilities
- **Generate in-game toggle UI** (user-requested): some mods draw on-screen toggle hints/menus via
  3DMigoto's overlay (`[Present]` + `draw_text`/notification, or importer command-list helpers). We want
  to *generate* these for a mod's keybindings. Needs the overlay/command-list interface mapped first.
- **General `.ini` editor**: edit arbitrary section `key=value`, with hash/override/resource sections
  gated read-only; respects namespacing.
- **Edit `[Key*]` `type`/`condition`/cycle values** (the easy editor only rebinds `key=` so far).

## Plugins interface (NOT yet researched — flagged)
The user noted 3DMigoto's **plugin interface** matters (could extend merge/other logic). 3DMigoto has a
plugin/command-list/`run = CommandList\…` mechanism + DLL plugins, and XXMI bundles its own DLL. We have
NOT yet mapped this from source/docs — do that (read 3Dmigoto repo + XXMI's DLL package) before building
merge or plugin-aware features. Don't assume; verify like the XXMI/ini work.

## Where this lives in our code
- Parse today: `Modules/Mod/Services/ModKeybindingService.cs` (`ParseKeybindingsAsync`, `[Key*]` only).
- `.ini`s live in the extracted cache (`CacheModsDirectory/{id}` or `DISABLED-{id}`), recompressed into
  the archive (source of truth) — see `filesystem-operation-serialization.md` + `use-project-paths.md`.
