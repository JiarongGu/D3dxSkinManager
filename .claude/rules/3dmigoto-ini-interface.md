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
- **Keybinding reorder = MOD METADATA, not .ini order.** A mod's keybindings can span MULTIPLE `.ini`
  files, so a global display order can't be expressed by per-file `[Key*]` section order (the display is
  grouped by file). `ReorderKeybindingsAsync` saves the order as `keybindingOrder` in the mod's
  `ModEntity.Metadata` JSON (the migration-free extension field); `ParseKeybindingsAsync` applies it
  (stable; unknown keys keep place) and **no longer force-sorts** by key priority. The keybinding list is
  drag-reorderable (`KeybindingPreview`, HTML5 DnD — `dataTransfer.setData` IS required to start the drag
  — with a drop-line indicator). IPC `REORDER_KEYBINDINGS`.
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

## Mod-merge — the EXACT GIMI algorithm (GROUNDED 2026-06-18 from `SilentNightSound/GI-Model-Importer` `Tools/genshin_merge_mods.py`)
**It does NOT use namespaces.** It builds ONE merged `.ini` that hash-dedups overrides and gates each
source via a **command list branching on `$swapvar`**. Port this faithfully (game-agnostic):

1. **Collect** every source mod's `.ini` (skip paths containing `disabled`). Each source = an ordered
   **group index** `0..N-1` (index 0 = the default the mod starts on). Parse each `.ini` into sections;
   a section = `{ header (TextureOverride|ShaderOverride|Resource|Constants|Present|CommandList|
   CustomShader), name, ordered key=val lines, conditionals (`x == y`), `endif` }`.
2. **`[Constants]`**: `global persist $swapvar = 0` + `global $active` + `global $creditinfo = 0`.
3. **`[KeySwap]`**: (`condition = $active == 1` when active-only) `key = <k>`, `type = cycle`,
   `$swapvar = 0,1,…,N-1`. **`[Present]`**: `post $active = 0` (active resets each frame).
4. **Overrides** — ONE `[TextureOverride<name>]` per UNIQUE `(hash, match_first_index)` across ALL mods
   (dedup): `hash = …` (+ `match_first_index`), `run = CommandList<name>`. On a `Position`-named override
   add `$active = 1` (so the key only swaps the on-screen character). Same hash seen again ⇒ just append
   that section's data to the hash's command-list group (don't emit another override).
5. **Command lists** — for each hash, `[CommandList<name>]` with `if $swapvar == <group0>` … `else if
   $swapvar == <group1>` … `endif`; each branch = that source's commands (vb/ib/ps/draw/…). **Resource
   refs + vb/ib/ps/vs/th binds are suffixed `.{group}`** (e.g. `vb0 = Resource…X.0`) so groups don't
   collide. Nested `if/endif` from the source are preserved (tab depth tracked).
6. **Resources** — each source's `[Resource*]` re-emitted as `[Resource<name>.{group}]` with `filename`
   pointing at the original file (optionally sha1-dedup identical files when compressing).
7. Final `.ini` order: Constants, Shader, Overrides, CommandLists, Resources. Originals get disabled
   (renamed `DISABLED*.ini`) so only the merged one is active.

**Implementation note for THIS app:** stage each selected mod's cache into one merge folder (keep each in
its own subfolder so `filename` paths stay valid), run the above to emit `merged.ini`, compress to a NEW
mod archive + register it (originals untouched in the library). Needs real two-same-character mods to
verify the in-game swap. This is the model — port `genshin_merge_mods.py` section-for-section.
**v1 (`MergeIniBuilder`) shipped, was superseded by v2 and REMOVED from the codebase (2026-06) —
`ModMergeService` now uses `NamespaceMergeBuilder` only. This section stays as the algorithm reference.**

### Mod-merge v2 — NAMESPACE-based (PREFERRED; preserves each variant's keybinds; design 2026-06-19)
The GIMI-port (v1) **rebuilds** everything and **drops each source's `[Key*]`/`[Constants]`** → a merged
mod loses per-variant shortcuts. The user wants merged variants to **keep their own keybinds as separate
sets** until the user unifies them. **Namespaces give this for free** — they isolate every name (sections,
`$vars`, resources, keys) per mod, so nothing collides and each source's content stays intact. Design:
1. **Per source:** prepend `namespace = <MergeName>\mod<N>` as the **first line** of each source `.ini`
   (or rely on folder-path default). Keep the source `.ini` otherwise **intact** — its `[Key*]`,
   `[Constants] $vars`, `[TextureOverride*]`, `[Resource*]` are now all under that namespace, collision-free.
   So every variant's shortcuts/toggles still work independently (the "different sets" the user asked for).
2. **Gate which variant renders:** inject `if $\<MergeName>\Master\swapvar == N … endif` around each
   source's `[TextureOverride*]` draw/override (cross-namespace var read — same `$\ns\var` syntax the
   namespace doc shows, e.g. `$\global\tracking\isSwimming`). This is the ONLY edit to a source — no
   var/resource/section renaming (the namespace already disambiguates).
3. **Master `.ini`** (`namespace = <MergeName>\Master`): `[Constants] global persist $swapvar` +
   `[KeySwap] type=cycle $swapvar=0,1,…`.
4. **Unify-keys later:** since each variant's keys are separate namespaced `[Key*]`, a future "unify
   shortcuts" step can rewrite them to share one key — until then they coexist.

#### `activeOnly` on-screen gate — cross-namespace WRITES DON'T WORK; only READS do (FIXED 2026-06-21)
**Symptom:** merged mod swapped variants correctly (default-swapvar change rendered the right one) but
**the cycle key did nothing**. Root cause: the on-screen gate had each SOURCE write the MASTER's global
(`$\global\<MergeName>\Master\active = 1` inside the gated override) and the master's `[KeySwap]` was
`condition = $active == 1`. That is a cross-namespace **WRITE** — and 3DMigoto's namespace docs only ever
demonstrate cross-namespace **READS** (`$x = $\global\tracking\isSwimming`) + **local writes**. The
cross-ns write never took effect → master's `$active` stayed 0 → `condition` permanently false → key dead.
The swap still worked because the gate is a cross-ns **read** (the proven primitive). The `activeOnly=false`
path emitted no condition, so the key already worked there — the bug was only the default `activeOnly=true`.
**Fix (use only proven primitives):** each SOURCE declares its OWN `global $mergeactive = 0` (in its own
`[Constants]`), sets `$mergeactive = 1` LOCALLY inside its gated override, and resets it each frame via
`[Present] post $mergeactive = 0`. The MASTER `[KeySwap]` OR-reads them cross-namespace:
`condition = $\global\<ns0>\mergeactive == 1 || $\global\<ns1>\mergeactive == 1 || …`. So write=local,
read=cross-ns — both proven. `BuildMaster` now takes the source-namespace list (to build the OR). Distinct
var name `$mergeactive` avoids clashing with a source mod's own `$active`. Tests: `NamespaceMergeBuilderTests`.
**RULE for any future cross-namespace coordination: read across namespaces, write locally — never the reverse.**

#### Gate address MUST equal the DECLARED namespace — no extra `global\` (FIXED 2026-06-21)
**Symptom:** BOTH variants rendered at once (the swapvar gate failed open). Root cause: the gate read was
`if $\global\<MergeName>\Master\swapvar == N` but the master was declared `namespace = <MergeName>\Master`
(no `global`). A cross-namespace read resolves as **`$\<namespace>\<var>`** — the leading `global` in the
docs' example (`namespace = global\tracking` → `$\global\tracking\isSwimming`) is **part of the namespace
name, not a magic prefix**. So `$\global\<MergeName>\Master\swapvar` ≠ the declared namespace → the var
never resolved → 3DMigoto ran the `if` body anyway (**unresolved condition fails OPEN**) → every variant drew.
**Fix:** ModMergeService now roots all merge namespaces under `global\` (`global\<MergeName>\Master`,
`global\<MergeName>\mod<N>` — mirrors the one proven docs example) and the builder reads `$\<namespace>\<var>`
verbatim (no hardcoded `global\`). Declared namespace == read address. Same correction applied to the
`$mergeactive` reads. **Invariant: the `$\…\var` address a gate/condition reads must be BYTE-FOR-BYTE the
target's declared `namespace = …` + `\` + var.** Tests assert this (incl. a no-double-`global\` guard).
**STILL unverified in-game** (the OR-condition + cross-ns reads on a `[Key]`); if it misbehaves, toggling
`activeOnly` OFF emits no condition → the key cycles unconditionally (guaranteed-working fallback).
**Trade-off:** v2 is far less rewriting (prepend namespace + inject one gate) and preserves keybinds/vars;
v1 hash-dedups (smaller output, no per-variant keys). **STILL NEEDS:** a real two-same-character in-game
test to confirm the cross-namespace `if $\Master\swapvar` gating renders only the active variant (the one
unverified assumption). Confirm against a real namespaced merged mod before shipping v2.

### Namespace contract (GROUNDED 2026-06-18 — leotorrez INI docs `/modding/docs/namespace`)
- `namespace = a\b\c` MUST be the **first line** of the `.ini`. Default namespace = the mod's folder path.
- Cross-file refs use `[type]\[namespace]\[name]`: `$\ns\var`, `run = CommandList\ns\name`,
  `vb0 = Resource\ns\Texture`. So a merger just: assign each source mod a **unique** namespace
  (creator\char\mod to avoid collisions) + write the master that drives `$\ns\$swapvar` / runs each
  `CommandList\ns\…` gated by the swap var. No hash/resource renaming needed — the namespace isolates them.
- **Mod-merge is buildable now** with the config-editor parse layer; it's the next big `.ini` feature.

### Key section — full option set (GROUNDED — `/modding/docs/key`)
Beyond the `key`/`type`/`$var` the editor exposes today, a `[Key*]` supports: `type` = (default load) /
`hold` / `toggle` / `cycle`; `back =` (a 2nd key that cycles **backward**); `wrap =` (cycle wrap, default
true); `smart =` (cycle resync, default true); `delay`/`transition`/`transition_type`/`release_delay`/
`release_transition`/`release_transition_type` (ms easing); **multiple `key =` lines** in one section
(keyboard + controller share state); Xbox buttons `XB_*` (and `XB2_*` per-pad); combos via spaces with
`NO_<mod>` / `NO_MODIFIERS` exclusions. `run = CommandList…` for advanced logic. The config editor should
grow toward exposing `back`/`wrap`/`smart`/transitions + multi-key as it matures.

## Still flagged
- **Generate in-game toggle UI / on-screen menu — NO stock primitive (GROUNDED 2026-06-18).** The INI
  docs (`command-list`, `present`, `custom-shader`) have NO text/font/notification/overlay command;
  `CustomShader` is raw DX11 (topology/cull/blend/ps/vs/cs). On-screen text would require shipping a font
  texture + a custom text-render shader — far beyond a generate-from-`.ini` feature. **De-prioritised.**
  (Mods convey state by cycling keys; the only built-in readout is 3DMigoto's own debug/hunting overlay,
  configured in `d3dx.ini`, not the mod.)
- **DLL plugin interface**: still unmapped; not in the INI docs. Lives in `bo3b/3Dmigoto` source
  (`deepwiki.com/bo3b/3Dmigoto`). Low priority — XXMI bundles its own DLL (see `xxmi-integration.md`).

## Authoritative INI reference (scrapeable — use for future grounding)
`leotorrez.github.io/modding/docs/*` is the maintained 3DMigoto/XXMI INI reference. Pages: `namespace`,
`command-list`, `present`, `constants`, `key`, `override`, `texture-override`, `shader-override`,
`shader-regex`, `custom-shader`, `resource`, `draw-calls`, `operators`, `flags`, `system-values`,
`fuzzy-matching`, `3dm-statics`. It's a JS/VitePress SPA → use `node devtools/dev.mjs research scrape <url>`
(puppeteer), NOT WebFetch.

## Where this lives in our code
- Parse today: `Modules/Mod/Services/ModKeybindingService.cs` (`ParseKeybindingsAsync`, `[Key*]` only).
- `.ini`s live in the extracted cache (`CacheModsDirectory/{id}` or `DISABLED-{id}`), recompressed into
  the archive (source of truth) — see `filesystem-operation-serialization.md` + `use-project-paths.md`.
