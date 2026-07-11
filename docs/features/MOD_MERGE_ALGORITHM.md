# Mod-merge — the GIMI hash-dedup algorithm (v1 reference, REMOVED from codebase)

> Reference only. `MergeIniBuilder` (v1) shipped, was superseded by the namespace-based
> `NamespaceMergeBuilder` (v2), and was **removed from the codebase (2026-06)**. `ModMergeService` now
> uses `NamespaceMergeBuilder` only. This is the original GIMI port spec, kept as the algorithm reference
> in case a hash-dedup merge path is ever revived. The LIVE v2 design + its hard-won cross-namespace
> gotchas live in `.claude/rules/3dmigoto-ini-interface.md`.

## The EXACT GIMI algorithm (GROUNDED 2026-06-18 from `SilentNightSound/GI-Model-Importer` `Tools/genshin_merge_mods.py`)

**It does NOT use namespaces.** It builds ONE merged `.ini` that hash-dedups overrides and gates each
source via a **command list branching on `$swapvar`**. Port faithfully (game-agnostic):

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

**Implementation note (staging):** stage each selected mod's cache into one merge folder (keep each in
its own subfolder so `filename` paths stay valid), run the above to emit `merged.ini`, compress to a NEW
mod archive + register it (originals untouched in the library).

**Trade-off vs v2:** v1 hash-dedups (smaller output) but DROPS each source's `[Key*]`/`[Constants]` → a
merged mod loses per-variant shortcuts. v2 (namespace) is far less rewriting and preserves keybinds/vars.
That's why v2 replaced it.
