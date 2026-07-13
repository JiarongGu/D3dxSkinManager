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

## Mod-merge — v1 (GIMI hash-dedup) is REMOVED; v2 (namespace) is live
`ModMergeService` uses **`NamespaceMergeBuilder` only**. The v1 GIMI hash-dedup port (`MergeIniBuilder`)
shipped, was superseded, and was **removed from the codebase (2026-06)**. Its exact algorithm is kept as
reference in `docs/features/MOD_MERGE_ALGORITHM.md` (read that only if reviving a hash-dedup path). The
v2 design + its hard-won cross-namespace gotchas follow — these are LIVE and load-bearing.

### Mod-merge v2 — NAMESPACE-based (PREFERRED; preserves each variant's keybinds; design 2026-06-19)
The GIMI-port (v1) **rebuilds** everything and **drops each source's `[Key*]`/`[Constants]`** → a merged
mod loses per-variant shortcuts. The user wants merged variants to **keep their own keybinds as separate
sets** until the user unifies them. **Namespaces give this for free** — they isolate every name (sections,
`$vars`, resources, keys) per mod, so nothing collides and each source's content stays intact. Design:
1. **Per source:** prepend `namespace = <MergeName>\mod<N>` as the **first line** of each source `.ini`
   (or rely on folder-path default). Keep the source `.ini` otherwise **intact** — its `[Key*]`,
   `[Constants] $vars`, `[TextureOverride*]`, `[Resource*]` are now all under that namespace, collision-free.
   So every variant's shortcuts/toggles still work independently (the "different sets" the user asked for).
2. **Gate which variant renders:** each source mirrors the master's swapvar into a LOCAL **in its
   `[Present]`** (`$mergeswap = $\<MergeName>\Master\swapvar`) and each gated override branches on the
   LOCAL (`if $mergeswap == N … endif`). This is the ONLY edit to a source — no var/resource/section
   renaming (the namespace already disambiguates). **The cross-ns read MUST live in `[Present]`, not
   inline in the override** — see the invisible-character fix below.

#### Cross-ns read goes in `[Present]`, NOT inline in the override (FIXED 2026-07-06, two rounds)
**Symptom:** after merge **nothing renders — the character is invisible** (default swapvar=0, yet group 0
never draws), across TWO fix attempts. Root cause: the swapvar cross-namespace read was done **inline
inside every `[TextureOverride*]`** — first in the `if` condition (`if $\global\<Merge>\Master\swapvar
== N`), then, after that failed, as an assignment immediately before the `if` (`$mergeswap =
$\<Master>\swapvar` / `if $mergeswap == N`). BOTH left the character invisible. The authoritative INI
docs (`leotorrez.github.io/modding/docs/namespace`) demonstrate a cross-namespace read in exactly ONE
place — **`[Present]`**, mirroring another namespace's var into a LOCAL once per frame:
`[Present] $swapvar = $\global\tracking\isSwimming`. A cross-ns read inline in a per-draw override
command list is UNDOCUMENTED and does not resolve → the `if` body is skipped for every group →
invisible. (Diagnosed + patched against the real broken merge `薇薇安-吸血鬼 (merged)`, 2026-07-06.)
**Fix (`NamespaceMergeBuilder`):** declare `global $mergeswap = 0` in each source's `[Constants]`; add
`$mergeswap = $\<Master>\swapvar` to each source's **`[Present]`** (once per frame — inject a `[Present]`
if the source has none); each gated override does only `if $mergeswap == N` (a same-namespace read,
always works). **RULE: a cross-namespace `$\ns\var` read belongs in `[Present]` (or `[Constants]`)
mirroring into a local — NEVER inline in a TextureOverride/ShaderOverride, and NEVER in an `if`
condition.** Same write-local/read-cross-ns rule that fixed the `$mergeactive` cycle key. **STILL needs a
real two-same-character in-game confirm** (the third grounded attempt); guaranteed fallback is
`activeOnly`=false + no gate (key cycles unconditionally). Tests: `NamespaceMergeBuilderTests` (7).
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
(keyboard + controller share state) — **SUPPORTED since 2026-07-05**: `ModKeybinding.AdditionalKeys`
holds the extra lines and the keybinding editor renders each chord as its own rebindable chip; Xbox
buttons `XB_*` (and `XB2_*` per-pad); combos via spaces with `NO_<mod>` / `NO_MODIFIERS` exclusions.
Chord capture resolves the base key from `KeyboardEvent.code` (`keyChord.baseFromEvent`) — `e.key` is
layout/shift-dependent (Shift+1 → '!') and made digit/symbol combos uncapturable. `run = CommandList…`
for advanced logic. The config editor should grow toward exposing `back`/`wrap`/`smart`/transitions
as it matures.

**Co-exist keyboard+controller EDITING (2026-07-13).** A hotkey can be edited to hold a keyboard key AND
a controller button at once. Backend `ModKeybindingService` has three write ops, all per-mod-queue-locked
+ fast single-file archive patch (never full recompress):
- `AddKeyLineAsync(modId, targetKey, newKey)` — append a `key =` line to the `[Key*]` section(s) binding
  `targetKey` (idempotent; throws `KEYBINDING_ALREADY_BOUND`).
- `RemoveKeyLineAsync(modId, keyToRemove)` — remove a `key =` line, never a section's last (throws
  `KEYBINDING_LAST_KEY`).
- `SetKeyLinesAsync(modId, anchorKey, keys[])` — atomically rewrite a binding's WHOLE `key =` set (the
  keybind-modal row **edit mode** save: rebind + add + remove in one call).
IPC: `ADD_KEYBINDING_ALTERNATE` / `REMOVE_KEYBINDING_ALTERNATE` / `SET_KEYBINDING_KEYS`. Two UI surfaces:
the **keybind modal** (`KeybindingPreview`) uses row edit mode → `SetKeyLines`; the **mod ini editor**
(`ModIniEditor`) hotkey rows add/remove alternates → `Add`/`RemoveKeyLine`. `KeyCaptureInput` (keyboard
capture + `XboxButtonPicker`) is the shared capture control. Tests: `ModKeybindingServiceTests` (add /
remove / set / last-key / not-found / ambiguity).

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
- **Shared read-parser: `Modules/Core/Helpers/IniParser.cs`** (2026-07-05) — sections + entries, BOTH
  comment chars (`;`/`；`), control-flow lines kept raw (never split `if $x == 1` on '='), `namespace =`
  first-line directive, and `IsDisabledPath` (any path segment starting "disabled" — XXMI
  `exclude_recursive = DISABLED*` / GIMI-merge renames; those files never load, so tools must skip
  them). Used by `ModAnalysisService`; migrate other read-paths opportunistically (write-back
  rewriters keep their own line-level edits).
- **Analyzer is grounded in these docs** (`ModAnalysisService.ParseIniStructure`, 2026-07-05): hash =
  8 hex on TextureOverride / 16 on ShaderOverride (wrong → MalformedHash; valid shader hashes join the
  conflict set); an *Override with no `hash` and no `match_*`/`filter_index` → DeadOverride; `[Key*]`
  without `key`/`back` → KeyMissingBinding; per-section if/endif balance → UnbalancedCondition (Error —
  unresolved conditions fail OPEN); duplicate section name in one file → Info (real mods repeat
  `[Constants]`; 3DMigoto merges); DISABLED inis excluded from hashes (fixes false conflicts/duplicates
  on merged mods; all-disabled → AllIniDisabled Warning). Plugin refs map pattern→NAME explicitly and
  the presence check includes the XXMI importer's `<importer>/Core/<plugin>` dir.
- Keybinding parse: `Modules/Mod/Services/ModKeybindingService.cs` (`ParseKeybindingsAsync`, `[Key*]` only).
- `.ini`s live in the extracted cache (`CacheModsDirectory/{id}` or `DISABLED-{id}`), recompressed into
  the archive (source of truth) — see `filesystem-operation-serialization.md` + `use-project-paths.md`.

## Framing: 3DMigoto is the core, XXMI is one way to set it up (user 2026-07-13)
Treat d3dx.ini / d3dx_user.ini / deploy target / launch as **3DMigoto** concerns; XXMI is a common
3DMigoto *management* app (bundles the DLL, installs importers, launches) — i.e. one way a user sets up a
3DMigoto instance. So var-persist/config code is named/organised around 3DMigoto (`D3dmigotoUserConfigService`),
with `XxmiService` acting as a detector that points at a 3DMigoto instance. **DONE 2026-07-14:** all
3DMigoto/XXMI backend concerns now live in ONE module — `XxmiService` (detector), `D3DMigotoService`
(parked own-launcher) and `D3dmigotoUserConfigService` (d3dx_user.ini var store, moved out of Mod) sit
together in **`Modules/Launch`** (kept that name per user — the module name is internal; the `LAUNCH_*`
IPC + `launchService.ts` are unchanged). Mod's `ModPresetService` injects the config service cross-module
(`AddModsServices` → `AddLaunchServices`; `AddLaunchServices` → `AddProfileServices` only, so no cycle).
**Why NOT rename it `Migoto`/`3DMigoto` (don't re-propose this):** the app targets 3DMigoto *today* but is
designed mod-system-agnostic — a later version may deploy/launch OTHER mod runtimes. A 3DMigoto-specific
module name would over-fit and need renaming then; the generic **`Launch`** survives that. Name the
*concern* around 3DMigoto (`D3dmigotoUserConfigService`, d3dx.ini handling); keep the *container* generic.

## d3dx_user.ini — 3DMigoto's persist store ("mod state" presets, 2026-07-13)
3DMigoto auto-saves every `global persist $var` to **`d3dx_user.ini`** on exit / F10 (`config_reload`) and
RESTORES it on next load (`Ctrl+Alt+F10` = wipe). This is how a mod's toggle/variant selection survives a
reload WITHOUT editing the mod's `[Constants]` default — XXMI's DLL "auto-save mod settings" IS this.

**Location:** next to the 3DMigoto DLL / `d3dx.ini` (the importer dir), NOT inside `Mods/`. In this app that's
`IProfilePathService.WorkDirectory` (xxmi/external mode = the importer dir). `D3dmigotoUserConfigService`
resolves it by finding `d3dx.ini` at the work dir (or its parent, if the work dir was pointed at `Mods/`).

**Real format** (verified vs a live ZZMI install):
```
; AUTOMATICALLY GENERATED FILE - DO NOT EDIT
[Constants]
$\zzmiv1\first_run = 0
$\mods\<modId>\<folder>\<file>.ini\swapkey3 = 1
```
A var's namespace = its deployed path `mods\<modId>\…` (3DMigoto's default namespace = folder path,
lowercased), so a line is attributable to a mod by its `\<modId>\` segment (case-insensitive — app ids are
upper-GUID, the file lowercases). Merge is keyed by the FULL LHS (`$\…\var`) — copy 3DMigoto's exact line,
never reconstruct the namespace.

**"Mod state" preset feature.** A preset can also snapshot each active mod's persisted vars
(`ModPresetEntity.ModState`, migration `202607130001`) — **MANAGED mods only** (capture is keyed by the
loaded managed mod ids; an anonymous/unmanaged mod dropped straight into `Mods/` can't be redeployed from a
managed archive, so its state is never captured) — and, on apply, merge them back into d3dx_user.ini
(replace by LHS, preserve the header + other mods/importer vars) so mods load carrying that state.
`D3dmigotoUserConfigService.CaptureVarLines`/`ApplyVarLines` (format-agnostic ini merge) +
`ModPresetService` capture-on-save / restore-on-apply; UI = the preset save dialog's "Also save mod state"
checkbox + a marker on presets that carry it. **NEVER rewrite the mod's own `.ini` default for this** (user
directive) — the persist store is the mechanism. RUNTIME-GATED final confirm: verify 3DMigoto actually
restores from the written d3dx_user.ini in-game. Guards: `D3dmigotoUserConfigServiceTests` (capture-filter /
merge / round-trip / drift / ambiguity), `ModPresetServiceTests`.

### The three preset-restore bugs + fixes (2026-07-13)
1. **"load mod from decompress failed" on apply** — a preset member with a surviving DB row but NO archive
   AND no retained cache can't be decompressed, so `LoadAsync` throws `MOD_EXTRACTION_FAILED` on EVERY apply.
   Fix: `ModPresetService.ApplyAsync` now filters targets to `_archiveService.ArchiveExists(id) ||
   _cacheService.HasCache(id)` and folds the rest into `SkippedCount` (same self-heal as the #36 no-DB-row
   skip) — never a hard failure. (Transient locks are a DIFFERENT case: the planner's retry +
   `MOD_FOLDER_IN_USE` already handle those, and a locked-but-present archive is NOT skipped.)
2. **State not applied on FIRST run, needs an F10 + re-apply** — 3DMigoto SAVES its running persist state to
   d3dx_user.ini on exit/F10, so an F10 clobbers our external write; and it only binds a var once the owning
   mod's namespace is loaded. There is no re-read handshake we can force. Mitigations: (a) the drift fix in #3
   makes the first apply land on the line 3DMigoto actually emits; (b) apply now returns `VarsApplied` and the
   UI shows `statusBar.presets.modStateHint` — **relaunch the game, NOT F10** — when it's >0.
3. **Some mod state silently not loaded** — the captured var LHS is the FULL path
   `$\mods\<id>\<folder>\<file>.ini\<var>`; only `\<id>\` and the trailing `<var>` are app-stable. A
   re-fix/merge/rename drifts the inner `<folder>\<file>.ini`, so a dumb full-LHS merge appends a GHOST line
   under the stale path that 3DMigoto never reads. Fix: `ApplyVarLines(lines, modIds)` matches per var —
   (1) exact LHS → overwrite value; (2) else same-mod + same-var-NAME (drift), unambiguous on BOTH sides →
   overwrite THAT current-namespace line's value (keep its LHS); (3) else append. The modId set is passed so a
   var's owner can be identified. **RULE: restore mod-state by (modId + var name), not by the whole stale LHS.**
