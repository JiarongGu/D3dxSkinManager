# Remote library — architecture (shipped 2026-07-06)

Current model: **per-site engines + engine-agnostic storage**, MANY libraries per profile, multi-tag
entries, ordered tag→category import rules. (Replaced the v1 single-binding / single-category / category-
on-binding foundation, now fully removed.) Site/API facts (huihui + GameBanana + Quark/Cloudreve
endpoints) live in `remote-library.md` — this file is the architecture; that one is the reference.

## Architecture: per-site ENGINES + generalized storage

The site-specific logic is a **library engine** — mostly HARDCODED per site, taking only small config
when useful (base URL, game ids). This is the extension point; a "custom engine" is a new engine class
targeting a site. Engines produce **normalized/generalized** data; everything downstream is engine-agnostic.

```
IRemoteSiteEngine {                      // one per site family
  EngineId                               // "http" | "gamebanana" | <custom>
  BrowseAsync(cfg, listId, page)  -> RemoteBrowseResult   // normalized cards (title, url, image, TAGS, date)
  SearchAsync(cfg, query, listId, page) -> RemoteBrowseResult
  GetDetailAsync(cfg, detailUrl)  -> RemoteModDetail        // gallery + download options
}
```
- **HttpRegexEngine** — config-driven regex-over-HTML (huihui). The `RemoteSourceConfig` regex fields ARE
  its "small config".
- **GameBananaEngine** — JSON apiv11 (hardcoded parsing; config = base URL + game ids).
- `RemoteBrowseService` is a thin **dispatcher**: pick the engine by `cfg.Engine`, delegate. No inline
  `if (isGameBanana) … else regex`.

**GENERALIZED (engine-agnostic) — shared by ALL engines:** the mod entry, its **tags**, sync status /
generation / pruning, the per-profile **index** (SQLite, composite key SourceId+ListId+EntryId,
incremental stop-at-known-page), library config + tag-rules, download+import, image cache
(sha1-named, app://-servable). Engines ONLY fetch + normalize their site; they never touch storage. So
adding a site = write one engine (+ optional small config); the mod/tag/sync/import machinery is reused.
Download-host resolvers (cloudreve/direct/external/quark) are SEPARATE from site engines — a site
references hosts; hosts recur across sites.

## Core concepts

### Sites/adapters = READ-ONLY DEFAULTS in `res/`
Shipped site configs (`res/remote-sources/*.json`) are DEFAULT setups — **not edited in the app**; they
seed-if-missing (user edits never overwritten). The user manages **libraries**, not adapters. (A future
"advanced: add a custom site" path may write a user adapter, but it's not the primary flow.)

### A profile has MANY libraries (not one binding)
A profile owns a **list** of libraries; the main screen **switches** between them; add/remove in
**library management**. `remote-libraries.json = { libraries: RemoteLibrary[], activeLibraryId }`;
`RemoteLibrary { id, sourceId, listId, name, tagRules (ORDERED), addedAtUtc }`. Legacy single-binding
auto-upgrades on read.

### Each remote mod has MULTIPLE TAGS (not one category)
Index entries carry a **list of tags**, rendered like mod tags; the toolbar filter is a **tag filter**.
GameBanana = **2 tags**: super (`_aRootCategory._sName`) + sub (`_aCategory._sName`). The index stores
`Tags` (JSON array, `json_each` exact-match filter); the index is a re-syncable cache.

### Import → local category = ORDERED TAG RULES (per library)
Each rule `{ name, tags: [one or more], categoryId }`. On import, evaluate **in order**; the **first**
rule whose tags ALL match wins → its `categoryId`; no match → **uncategorized** (`MatchTagRules`).
```
tagRules: [
  { name: "Skins→Skins",  tags: ["Skins"],          categoryId: "<local Skins>" },
  { name: "Hu Tao skins", tags: ["Skins","Hu Tao"], categoryId: "<local HuTao>" },  // multi-tag
]
```

### Detail page = LEFT/RIGHT split
Review (gallery/preview/info) LEFT, actions (download links — some sites have MANY) RIGHT.

### Import identity = durable `{sourceId, listId, entryId}` in mod `Metadata.remote`
NOT detailUrl equality (sites move hosts — huihui does). Imported-lookup is cached (TTL 30s +
invalidate-on-import) — never the old O(all mods) JSON-parse per page.

## GameBanana specifics (endpoints in remote-library.md)
- **Search**: `apiv11/Util/Search/Results?_sModelName=Mod&_sSearchString={q}&_idGameRow={gameId}&_nPage={n}`
  (response mirrors the Subfeed `_aRecords`).
- **Tags**: super = `_aRootCategory._sName`, sub = `_aCategory._sName`.
- **Download**: direct (`/dl/{id}`), resolver type `direct`, real filename from `_sFile`.
- **Order**: page newest-first by `_tsDateAdded` + capture as DateHint.

IPC surface: `LIBRARY_GET_STATE/ADD/UPDATE/REMOVE/SET_ACTIVE` (+ add-with-sync), `INDEX_TAGS`,
`INDEX_QUERY` (`tag`, `importedOnly`), `DOWNLOAD_IMPORT {listId, entryId, tags}`, `SEARCH`,
`GET_IMPORTED_STATE`.

Rules that bind: `download-service.md`, `background-task-tracking.md`, `use-project-paths.md`,
`enum-serialization.md` (tags/rules camelCase), `ui-component-layers.md`, `in-app-guide.md`.

## Post-ship additions (2026-07-06)
- **Imported tracking = "downloaded" filter + "locate" + local-mod tagging.** `RemoteImportService`
  imported-lookup returns MAPS (`key→modId`, `legacyUrl→modId`); `RemoteFacade.QueryIndexAsync` sets
  `entry.Imported` + `entry.LocalModId` and takes `importedOnly` (imported entry-ids → SQL
  `EntryId IN @ids`, empty set = no rows). Frontend: `remoteUiStore.downloadedOnly` + toolbar toggle;
  card ✓ badge; detail "already imported" banner with **View mod** → `navigateToModSearch`. On import the
  local mod is TAGGED with the remote entry's tags. Remote list auto-refreshes when a
  `process.remoteImport` COMPLETES (watch completed count — NOT `MOD_LIST_UPDATED`, which fires before
  metadata is written).
- **Quark large-file download**: resolver uses the quark **desktop-client** User-Agent
  (`quark-cloud-drive/… Electron … Channel/pckk_other_ch`), NOT a browser UA — Quark gates its download
  size limit (apiv1 code 23018) by product/UA. `REMOTE_QUARK_SIZE_LIMIT` = fallback message. Full Quark
  flow in `remote-library.md`; `download-service.md` (body surfaced on non-2xx).
- **Management edit-view UX (`RemoteLibraryManagementScreen`).** Editing a LIBRARY or a SITE is a
  dedicated `--fill` screen (pinned `← header` + scrollable body + pinned footer), NOT inline. Site
  editing is LIFTED here (`editSource` state); `RemoteSourceManagerScreen` is list-only, routes edit/add
  up via `onEdit`. Main 库/站点 `Tabs` is CONTROLLED (`mainTab`) so returning preserves the tab. List
  rows use `box-sizing: border-box` (a `width:100%` content-box row overflowed + got right-trimmed). Tag
  pickers use `CompactSelect`. Section titles `CompactSection`/`CompactTitle` (14px, `ui-design-rules.md`).

## Post-ship additions (2026-07-10)
- **Title-derived tags for TAGLESS sites** — `RemoteSourceConfig.TitleTagPattern` (regex, named group
  `tag`; huihui seed `^(?<tag>\S+)\s` = the character name before the first space). Applied CENTRALLY in
  `RemoteBrowseService` (Browse/Search cards + GetDetail) after the engine normalizes, ONLY to entries
  with no tags of their own — so browse, search AND index sync agree. A bad regex never breaks browsing
  (`DeriveTitleTag` swallows, 250ms timeout). In the site editor (`remote.fieldTitleTag`);
  `RemoteSourceStore.SeedMissing` ADDITIVE-fills it when null. Existing entries get the tag on next full
  reindex.
- **Detail screen live imported-banner** — IPC `GET_IMPORTED_STATE` {sourceId, listId?, entryId?,
  detailUrl?} → `{ imported, localModIds }`. Detail screen seeds from open-time props + re-queries when a
  `process.remoteImport` COMPLETES — no reopen needed.
- **Mod detail → remote page backlink** — `RemoteSourceLinkIcon` (in `ModPreviewPanel/`) parses
  `metadata.remote` (`shared/utils/modRemoteRef.ts`) → a single `GlobalOutlined` `CompactIconButton`
  (tooltip `mod.remoteSourceView`) at the END of the mod-detail title (`.mod-preview-title__source`),
  opening `RemoteModDetailScreen` directly (cross-module React import, `imported` + `localModIds=[mod.id]`
  preset). Null when no remote identity.

## Browse tag-filter bar (2026-07-07)
`RemoteLibraryView` shows a horizontal **tag-filter chip strip** below the toolbar: `Tag.CheckableTag`
chips from `INDEX_TAGS` (`remote.tagAll` "All" + one per tag with count) → sets `remoteUiStore.tagFilter`
→ re-queries. UX: active chip **scrolls into view** (addressed by child index), trailing **end-padding**,
**mouse-wheel → horizontal scroll** (non-passive `wheel` listener; React's synthetic `onWheel` is passive
and can't `preventDefault`). Only rendered once the index has tags.
- **PARKED (next):** large tag counts (GameBanana = hundreds) → move click-filter into a dropdown/slide-in
  panel. The wheel-scrollable strip is the keeper "for now"; the panel is a future upgrade, not a regression.
- **Management has NO tag search box — deliberately** (tried + removed at user request 2026-07-07). Tag
  find-ability belongs to the browse bar, not the editor. Do not re-add.

## Post-ship additions (2026-07-12)
- **Tag labels/aliases are PER-PROFILE now** (were on the GLOBAL `RemoteSourceConfig.TagLabels` in
  `{data}/remote-sources/` → editing them in one profile changed EVERY profile; user-reported). New
  per-profile store `IRemoteTagLabelStore` / `RemoteTagLabelStore` → `{profile}/remote-tag-labels.json`
  (shape `sourceId → lang → rawTag → label`). **Seed-once semantics:** first access for a source copies
  the source config's shipped/global `TagLabels` into the profile file (nothing lost, shipped defaults
  preserved), then the profile owns an independent copy — edits never leak across profiles. Wiring chain:
  store → **read** in `RemoteBrowseService.GetSources` (`TagLabels = _tagLabels.GetForSource(id, s.TagLabels)`,
  so `RemoteSourceInfo`/all display is per-profile) + `RemoteIndexService.QueryAsync` (alias-search) →
  facade IPC `LABELS_GET {sourceId}` / `LABELS_SET {sourceId, lang, labels}` → `remoteService.labelsGet/labelsSet`
  → `RemoteLibraryManagementScreen` alias editor (dropped the old `saveSource(...tagLabels)` global write;
  `editingConfig` state removed). The global `TagLabels` on the source config is now ONLY a read-only
  seed/default; the site editor (`RemoteSourceEditor`) doesn't edit it. Guard: `RemoteTagLabelStoreTests`
  (cross-profile isolation + seed-once + preserves untouched languages).
- **Per-profile LIBRARIES moved from JSON to SQLite** (`{profile}/remote-libraries.json` → `RemoteLibraries`
  table, migration `202607120001`). Native to SQL + joinable. `RemoteLibraryRepository` (SYNCHRONOUS Dapper —
  a handful of rows, keeps `IRemoteLibraryStore` synchronous) does CRUD; `RemoteLibraryStore` keeps the SAME
  interface and one-time-migrates the legacy JSON (and the older `remote-binding.json`) into the table on
  first access, preserving order + the active row (`Active` flag column), then deletes the JSON. Active
  library = the row with `Active=1`. Site ADAPTERS stay GLOBAL JSON (`{data}/remote-sources`) — that split is
  deliberate: JSON = global site settings, SQLite = per-profile config. Guard: `RemoteLibraryStoreTests`
  (real repo over in-memory DB: CRUD + JSON→SQLite migration + legacy binding).
- **Mod → library FK for search-by-library-name.** `Mods.RemoteLibraryId` column (FK to `RemoteLibraries.Id`,
  migration `202607120001`) — the mod references its library; the library entity owns the name (NOT copied onto
  the mod). Set on import (`RemoteImportService`: `FindBySourceList(sourceId,listId)?.Id`); existing mods
  backfilled once by `ModRepository.BackfillRemoteLibraryReferencesAsync` (native SQL mapping metadata.remote →
  a library row), kicked off fire-and-forget at profile init in `ProfileServiceRouter` after the library JSON
  migrates. `ModEnrichmentService.PopulateLibraryNames` resolves `ModInfo.LibraryName` LIVE from the library
  table by FK (a computed field like `CategoryName`, never stored) → the frontend comprehensive search
  (`ModListPanel` `matchesSearchQuery` `extra`) matches it. Rename → reflects on next load; remove library →
  name gone (+ FK can be nulled). Guard: `RemoteLibraryModLinkTests` (backfill + enrichment).
- **Remote INDEX search already covers title + tags + tag-label aliases** (`RemoteIndexRepository.QueryAsync`
  — free-text `search` LIKEs the title, `json_each` any tag, and expands a term through the alias table). It's
  the SYNCED-index path (`INDEX_QUERY`); the live/unsynced fallback (`SEARCH`) is title-only by nature. Guard:
  `RemoteIndexServiceTests.Query_FreeTextSearch_MatchesTitle_Tag_AndLabelAlias`.
- **ALL per-profile remote data is now SQLite-driven** (migration `202607120002` adds `RemoteTagLabels` +
  `RemoteSources`). Rule: **JSON = editable DEFINITION, SQLite = runtime store**; everything reads SQLite.
  - **Tag labels** → `RemoteTagLabels` table (`RemoteTagLabelRepository`); `RemoteTagLabelStore` keeps its
    interface + seed-once, one-time-migrates `remote-tag-labels.json` then deletes it.
  - **Site adapter configs**: the GLOBAL `{data}/remote-sources/*.json` files STAY as the editable definition
    (+ shipped `res/remote-sources` seeds); each profile mirrors them into `RemoteSources`
    (`RemoteSourceRepository`, full config as JSON in one column). `RemoteSourceStore` keeps its interface:
    `GetAll` computes the JSON mtime signature and, when it changed (edit/drop/seed/Save/Delete), re-`Sync`s
    JSON→SQLite (upsert all + delete rows whose JSON is gone) then reads SQLite; `Save` writes JSON + upserts
    SQLite; `Delete` removes both. "Drop a JSON, no restart" still works, but reads are SQLite. Per-profile
    mirror — NOT a global app.db (that decision stands). Guards: `RemoteSourceStoreTests` (seed/sync/edit/drop
    + Save/Delete), `RemoteTagLabelStoreTests` (two in-memory DBs prove cross-profile isolation + migration).
  - After migration only the remote-source definition JSON remains; `remote-libraries.json` +
    `remote-tag-labels.json` are migrated in and deleted.
- **Tag-label edit → browse refresh + rule/mod-list reflection (bug sweep 2026-07-12).**
  - Editing a tag alias in management updated the per-profile labels but the browse **card tag badge**
    stayed stale until a hard reload (the on-save `onChanged→reloadLibraries` can land while the manage
    panel occludes the grid). Fix: `openManagement` also passes `onClose: () => reloadLibraries()` so a
    final `getSources` runs when the panel closes (badges read `source.tagLabels` reactively). Backend
    has no cache (`RemoteTagLabelStore.GetForSource` reads SQLite live).
  - The tag-RULE editor's tag picker showed RAW tags; it now maps options through the aliases
    (`aliasLabel(tag)`, reflecting unsaved edits) so a rule built against a labeled tag reads naturally —
    stored/matched value stays the raw tag.
  - Mod list shows the origin **library name beside the category chip** for remote-sourced mods
    (`ModList.tsx`, `mod.libraryName` from the FK; cyan + globe).
  - **`SeedMissing` now ADDITIVELY appends newly-shipped `lists` entries** (by id) to an existing
    adapter — existing installs get newly-supported games (e.g. GameBanana `21842` Arknights: Endfield)
    on update WITHOUT re-seeding; user-added/renamed lists (same id) are never overwritten. Same additive
    block as `cardScopePattern`/`titleTagPattern`. Guard: `RemoteSourceStoreTests` append-not-overwrite.
- **Parameterized sources — 3-tier config resolution (2026-07-12, phased).** A source declares
  library-configurable `Params` (`{ key, label, type: input|select, options, default, required }`); a
  library supplies `ParamValues` that substitute for `{param.<key>}` in the EFFECTIVE config. Resolution
  (`IRemoteSourceResolver`, PURE + JSON-level): `res base ← sparse local overlay ← param substitution`
  — sparse overlay = "absent key = inherit" so res updates to untouched fields flow through; `Diff` emits
  the sparse overlay. The overlay is RAW JSON (a typed config can't be sparse). Wiring: `RemoteBrowseService`
  builds the effective config via `Effective(sourceId, listId)` (library found by `FindBySourceList`;
  index crawls via Browse, so params flow to the index too) — inert until a library has values, so zero
  regression. Storage: `RemoteLibrary.ParamValues` (migration `202607120003`, JSON column). IPC:
  `RemoteSourceInfo.Params` + `LIBRARY_ADD` carries `paramValues`; `LIBRARY_UPDATE` persists them. UI:
  `RemoteLibraryManagementScreen` renders a field per param (input/select) on library ADD + an editable
  "Params" tab on edit. Guards: `RemoteSourceResolverTests` (merge/substitute/diff/round-trip),
  `RemoteLibraryStoreTests` (ParamValues round-trip). **Deferred (follow-ups):** the richer source picker
  (res+local origin badges, wider rows), "use global as template" to author a local, library source-SWITCH
  (store keeps identity fixed today), detail/search param substitution (browse+index only), and the store
  sparse-overlay rewire (data/ is still full-copy + additive-merge; the resolver already supports sparse).
