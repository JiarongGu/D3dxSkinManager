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
  `RemoteLibraryStoreTests` (ParamValues round-trip).
  - **Also done:** library **source-SWITCH** — `RemoteLibraryStore.Update` lets a library repoint its
    source/list (validates the target; keeps id + mod FKs + its param overrides), repo persists it, and the
    edit view has a Source/Game picker. **Detail/search** now resolve params too (`GetDetailAsync`/
    `DetailProvidesTags` take `listId` → `Effective`).
  - **Sparse-overlay store rewire — DONE.** `RemoteSourceStore` is now 2-tier: res/remote-sources is the
    runtime BASE, data/remote-sources holds SPARSE overlays (or full custom sources); effective =
    `Resolve(res, overlayRaw)`, so a *changed* res field flows through any overlay that didn't set it (a
    fixed regex, a new game). `GetAll` unions res∪data + resolves; `Save` writes only the DIFF vs res
    (res files never written); `Delete` reverts a res source to default / removes a custom one; a one-time
    load cleanup drops no-op full copies (== res) so they inherit res. The override-vs-stale ambiguity is
    handled conservatively: only copies with NO real override are auto-dropped; copies with overrides are
    kept as-is (never auto-rewritten), and new edits save sparse.
  - **Source picker / manage UI — DONE.** `RemoteSourceInfo.Origin` (`default` | `customized` | `custom`,
    from `RemoteSourceStore.GetOrigins`) drives an origin BADGE in `RemoteSourceManagerScreen`. Editing a
    shipped source is **"use as template"** (Save writes a sparse overlay → it becomes `customized`);
    **reset-to-default** deletes the overlay (a default source shows no delete — nothing to remove). i18n
    `remote.origin.*` / `useAsTemplate` / `resetSource` (en/cn).

## Post-ship additions (2026-07-13) — source-editor UX pass (user review)
- **Test-connection is now a pass/fail INDICATOR, not a toast + text line.** `RemoteSourceTestResult`
  gained `Success` + `Error` (+ `DetailFetched`); `RemoteBrowseService.TestConfigAsync` **catches**
  network/parse/validation failures and returns them as DATA (`Success=false` + `Error`) — only
  cancellation propagates. Frontend `RemoteSourceTestResultView` (L2, pure props) turns it into
  per-check `StatusTag`s (Connected / cards / pages / detail / downloads / images) or a red failure with
  the message; the editor renders it for `testing || testError || testResult`. i18n `remote.test*`.
- **Per-field "compare with default" (re-sync).** New `IRemoteSourceStore.GetDefault(sourceId)` returns
  the shipped res base resolved with NO overlay (params filled from declared defaults, same shape as
  `GetById`) so a field-by-field diff isolates exactly the overlay's overrides; null for a custom source.
  IPC `GET_SOURCE_DEFAULT` → `remoteService.getSourceDefault`. `RemoteSourceCompareDialog` (L2) lists
  only differing fields with checkboxes → "Revert selected" copies the DEFAULT value into the working
  config; **Save then drops it from the sparse overlay automatically** (no new revert path — reuses the
  Diff-vs-res write). Button shown ONLY when `origin === 'customized'`. i18n `remote.compare*`.
- **Editor dirty-tracking — Save disabled when nothing changed** (user ask). `RemoteSourceEditor`
  compares `canonical(currentConfig)` vs `canonical(baseline={...BLANK,...initial})` (sorted-key JSON so
  key order is irrelevant; advanced mode parses the raw JSON). `origin` is threaded
  `RemoteSourceManagerScreen.onEdit(cfg, origin)` → `RemoteLibraryManagementScreen` editSource state →
  `RemoteSourceEditor` prop (enables compare + future origin-aware UX).
- **Tests:** backend `RemoteBrowseServiceTests` (test-connection success/failure/no-lists) +
  `RemoteSourceStoreTests` (`GetDefault` res-base/custom-null); FIRST remote **frontend** vitest suites
  (`RemoteSourceTestResultView` / `RemoteSourceCompareDialog` / `RemoteSourceEditor`). Added a global
  **`ResizeObserver` stub in `setupTests.ts`** (jsdom gap; antd `Select`/`Table`/`Tabs` need it) — reuse
  for any future antd-Select-bearing component test.
- **Compare dialog refinements (same-day review):** it's now **side-by-side** Yours | Default columns,
  each tinted (red/green) with the **changed span highlighted** (`splitDiff` = common prefix/suffix, mid
  highlighted — reads well for URLs/regex). **Select-all** header + **Take all** (sync every differing
  field) alongside **Revert selected**. **Empty-ish equality** (`asText` collapses ``/null/undefined/[]/{}``
  → `''`) so the res default omitting a null no longer shows a bogus diff vs the current's empty string —
  only REAL differences appear. The rows scroll (`__list` max-height) so a big diff stays in the modal.
- **Library editor clarity (same review):** the name-in-tab-bar + bare source/game switcher were replaced
  by a labeled **Name / Source · Game** `CompactField` block (`__edit-fields`), pinned above the tabs; the
  rules tab gained a **column-header row** (order · name · tags · title · category). Library **Save is now
  dirty-gated** too (`canon()` vs a baseline captured at `startEdit` + when aliases load).
- **Origin = REAL diff, not file-existence (2nd review).** `GetOrigins` + `RemoveNoOpOverlays` now judge an
  overlay by its RESOLVED effect: `OverlayHasRealDiff(master, overlayRaw)` = `Diff(master, Resolve(master,
  overlayRaw))` has more than just `id`. So a sparse overlay that resolves back to master (user reverted
  every field, OR a res update caught up to the override) → origin `default` AND the overlay is DROPPED on
  load (refers to master). Only a genuine difference reads `customized`. Chip UX: **default → no chip**;
  `customized` → an amber **"Modified"** chip (`DiffOutlined` + `remote.origin.customizedHint`, invites
  edit→compare-with-default); `custom` → info chip. Guard: `RemoteSourceStoreTests` (real/no-op/custom
  origins + drop-no-op-sparse-overlay).
- **Rules/aliases scale UX.** Both lists in the library editor grow to hundreds (GameBanana tags): a
  `CompactInput`+`SearchOutlined` **filter** (shown once a list exceeds `FILTER_AT (6)`, with a "showing
  X of Y" count) narrows first, then the result is **PAGINATED** (`PAGE_SIZE = 15`, raw antd `Pagination`)
  so only a page of editable rows (each an antd `Select`) mounts — the perf fix for hundreds of records.
  Rows keep their REAL index through `filtered = arr.map((r,i)=>({r,i})).filter(...)` → `.slice(page)`
  (edit/reorder/delete target the right entry); the page clamps (`Math.min(page, max)`); the filter resets
  the page to 1; **Add jumps to the last page** (`setPage(MAX_SAFE_INTEGER)` → clamped) so the new blank
  row shows. **Reorder disabled while a rule filter is active** (`remote.reorderClearFilter`). Rule filter
  matches name/tags/alias-label/title/category name (via `flattenCategoryOptions`). i18n `remote.filter*`.
  Both list tabs share ONE look: **no column-header row** (dropped from Input rules to match Tag labels —
  placeholders convey the fields), the **shown/total count ALWAYS beside the search** (`filterCount`, not
  gated on an active filter), and a subtle row hover. Only difference: Input rules keep the **leading order
  badge** (first-match-wins ordering); Tag labels have none (unordered).
  - **Library editor = 3 tabs: Detail · Input rules · Tag labels** (`editTab` default `detail`, reset on
    `startEdit`). Detail holds Name / Source · Game / the source's Params (the old always-on fields block
    + Params tab folded in). The verbose per-list hints (`tagRulesHint`+Default, `tagAliasesHint`) moved
    OFF the page onto the **tab-label `Tooltip`** (hover) — the user's "remove the (xx), show on tooltip"
    declutter. Footer Add shows only on the two list tabs. i18n `remote.tabDetail/tabInputRule/tabTagLabel`;
    parenthetical rule labels shortened (`ruleTags` "tags", `ruleTitlePattern` "title regex").
  - **Tag-label (alias) row = SEARCHABLE single-select + label, one row per tag.** The tag field is a
    `showSearch` single `CompactSelect` whose options EXCLUDE tags already used by another row
    (`usedAliasTags`) — so a tag can carry only ONE label (translation) and the picker only lists
    **unconfigured tags**, making search relevant. Single-column short rows + filter + pagination.
    (A fixed-chip and a 2-up grid were tried and reverted — the chip killed searchability, the grid
    cramped the picker.) Rules keep their 4-field row (need the width).

## Local mod ↔ remote origin (3rd review, 2026-07-13)
- **Origin indicator moved list → DETAIL panel.** The remote library/source chip was on every `ModList`
  row (cyan `mod.libraryName` Tag); it now lives ONCE in the mod detail header as `RemoteSourceChip`
  (`ModPreviewPanel/`), beside the category (`.mod-preview-category-row`). The chip shows the library name
  (or `remote.remoteLibrary` fallback) and is CLICKABLE when the mod still has `metadata.remote` → opens
  `RemoteModDetailScreen` (this replaces the old `RemoteSourceLinkIcon` globe-only backlink; file renamed).
  `ModList` no longer imports `GlobalOutlined`.
- **Import no longer copies remote tags onto the mod.** `RemoteImportService` used to
  `entity.Tags = tags ∪ detail.Tags` — but a remote "tag" is usually just the character/category name,
  which the resolved category already carries, so it was noise (user call). The tags STILL drive category
  resolution (`ResolveCategory`); only the copy-onto-mod-tags step is gone (dead `ParseTags` helper removed).
- **`source:` search field.** `searchQueryParser` gained a `source` field (+ `SearchableRecord.source`,
  `source:` prefix, `getFieldValues`/all-values/unqualified wiring); `ModListPanel` maps `mod.libraryName`
  to `record.source` (out of `extra`), registers the localized `来源:` prefix, and lists it in the search
  help. Guard: `searchQueryParser.test` (source parse/match/unqualified/negate). i18n
  `mods.search.syntaxSource` / `helpFieldSource`. The search-help EXAMPLES are i18n keys
  (`mods.search.ex*`) so the CN hint shows CN examples (`标签:头发` etc.).
- **Origin chip placement (settled):** the `RemoteSourceChip` sits at the RIGHT end of the mod-detail
  TITLE row (`margin-left:auto`, `flex:0 0 auto`, ellipsis, `cursor:pointer` when clickable) AND as a
  cyan tag beside the category on the list row — both kept per user.

## Post-ship additions (test modal + editor polish, 2026-07-13)
- **Test-connection is a MODAL** (`RemoteSourceTestDialog`, built on `FormDialog`) — obvious spinner →
  pass/fail instead of an inline line. It PICKS the game/list (when the source has >1) and supplies the
  source's PARAMS, then runs; auto-runs once on open with defaults. Backend `TestConfigAsync` gained
  `paramValues` and resolves `{param.*}` via `IRemoteSourceResolver.Resolve(config, null, paramValues)`
  BEFORE testing (so a parameterized source is tested as a specific library would run it); facade +
  `remoteService.testSource` + `RemoteSourceConfig.params` (FE type) thread it. The editor's "Test"
  button just snapshots the (form or raw-parsed) config and opens the dialog.
- **Library editor polish:** the numbered order-dot is suppressed in the rule/alias **header rows**
  (`.rule-head .rule-order { background:none; border:none }`); editable rows get a subtle hover; the
  aliases tab got column headers (raw tag · label) for parity with rules. Aliases are UNORDERED, so their
  rows have NO order badge at all (only rules do). i18n `remote.testDialogTitle` / `remote.testRun`.
- **"Resync a source to master" = revert via Compare→Take-all → Save DROPS the overlay. No reset button.**
  The single way to revert a Modified source is the source editor's **Compare with default → Take all**,
  then Save. `RemoteSourceStore.Save` now, for a res-backed id whose `Diff(master,config)` has no real
  override (only `id`), **DELETES the overlay file** instead of writing a no-op — so the source reads
  `default` again (no misleading "Modified" chip). The per-source reset/resync icon on the Sites row was
  tried and **removed at user request** ("you don't really need the reset button") — Custom sources keep a
  delete; Modified/Default sources have only Edit. A GLOBAL "rebuild the mirror" Resync button was also
  tried and **reverted** (wrong reading of "resync"). Guards: `RemoteSourceStoreTests`
  (`Save_RevertsToMaster_DropsOverlay_...`, `Save_EmptyStringVsMasterAbsent_...`).
- **`Diff` treats empty ≈ absent (resolver).** `RemoteSourceResolver.DiffNode` returns "no diff" when BOTH
  sides are empty-ish (null/absent/``/[]/{}`) — the editor's `BLANK` fills optional fields with `""` while
  master omits them (null), which used to write a spurious overlay → a fake "Modified" chip. Now those are
  not a diff, matching the compare dialog's frontend `asText` normalization.
- **A library only REFERENCES its source — it never mirrors it.** `RemoteLibrary` = `{ id, sourceId,
  listId, name, tagRules, paramValues, addedAtUtc }` — a reference (`sourceId`/`listId`) + the input setup
  (`paramValues`) + local rules; it stores NO copy of the source config. The effective config is resolved
  live (`RemoteBrowseService.Effective` = base + paramValues). Do NOT add source-config mirroring, origin
  chips, or resync to libraries — those are source-level concerns (Sites tab / source editor) only.
