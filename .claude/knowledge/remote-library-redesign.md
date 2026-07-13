# Remote library — architecture

Model: **per-site engines + engine-agnostic storage**, MANY libraries per profile, multi-tag entries,
ordered tag→category import rules. Site/API facts (huihui + GameBanana + Quark/Cloudreve endpoints) live
in `remote-library.md` — this file is the ARCHITECTURE; that one is the reference. (Shipped 2026-07-06;
kept current. This file was condensed from a per-change changelog on 2026-07-13 — history is in git.)

## Architecture: per-site ENGINES + generalized storage

Site-specific logic is a **library engine** — mostly HARDCODED per site, taking small config when useful
(base URL, game ids). Engines produce **normalized** data; everything downstream is engine-agnostic.

```
IRemoteSiteEngine {                      // one per site family
  EngineId                               // "http" | "gamebanana" | <custom>
  BrowseAsync(cfg, listId, page)  -> RemoteBrowseResult   // normalized cards (title, url, image, TAGS, date)
  SearchAsync(cfg, query, listId, page) -> RemoteBrowseResult
  GetDetailAsync(cfg, detailUrl)  -> RemoteModDetail       // gallery + download options
}
```
- **HttpRegexEngine** — config-driven regex-over-HTML (huihui); the `RemoteSourceConfig` regex fields ARE its config.
- **GameBananaEngine** — JSON apiv11 (hardcoded parsing; config = base URL + game ids).
- `RemoteBrowseService` is a thin **dispatcher**: pick the engine by `cfg.Engine`, delegate. No inline `if (isGameBanana)`.

**GENERALIZED (engine-agnostic), shared by ALL engines:** the mod entry + its **tags**, sync
status/generation/pruning, the per-profile **index** (SQLite, composite key SourceId+ListId+EntryId,
incremental stop-at-known-page), library config + tag-rules, download+import, image cache (sha1-named,
`app://`-servable). Engines ONLY fetch + normalize; they never touch storage. Adding a site = write one
engine (+ optional small config). Download-host resolvers (cloudreve/direct/external/quark) are SEPARATE
from site engines — a site references hosts; hosts recur across sites.

## Core concepts

- **Sites/adapters = editable DEFINITION, not app-managed.** Shipped configs (`res/remote-sources/*.json`)
  are DEFAULT setups that seed-if-missing (user edits never overwritten). The user manages **libraries**,
  not adapters.
- **A profile has MANY libraries.** A profile owns a list; the main screen SWITCHES between them.
  `RemoteLibrary { id, sourceId, listId, name, tagRules (ORDERED), paramValues, addedAtUtc }`. Legacy
  single-binding auto-upgrades on read.
- **Each remote mod has MULTIPLE TAGS** (not one category). Index entries carry a tag list; the toolbar
  filter is a **tag filter**. GameBanana = 2 tags: super (`_aRootCategory._sName`) + sub (`_aCategory._sName`).
  Index stores `Tags` (JSON array, `json_each` exact-match filter); the index is a re-syncable cache.
- **Import → local category = ORDERED TAG RULES (per library).** Each rule `{ name, tags:[…], categoryId }`;
  evaluate in order, FIRST rule whose tags ALL match wins (`MatchTagRules`); no match → uncategorized.
  Rules can also carry a `titlePattern` (regex over the title).
- **Detail page = LEFT/RIGHT split.** Review (gallery/preview/info) LEFT, actions (download links) RIGHT.
- **Import identity = durable `{sourceId, listId, entryId}` in mod `Metadata.remote`** — NOT detailUrl
  equality (sites move hosts). Imported-lookup is cached (TTL 30s + invalidate-on-import).
- **Import does NOT copy remote tags onto the mod.** A remote "tag" is usually just the character/category
  name the resolved category already carries → noise. Tags STILL drive category resolution; only the
  copy-onto-`mod.Tags` step is gone.

## Storage: JSON = definition, SQLite = runtime store

**All per-profile remote data reads from SQLite; everything reads SQLite** (migrations `202607120001..0003`).
Each store keeps its interface + one-time-migrates the legacy JSON, then deletes it.
- **Libraries** → `RemoteLibraries` table (`RemoteLibraryRepository`, SYNCHRONOUS Dapper — few rows;
  `RemoteLibraryStore` migrates `remote-libraries.json`/legacy `remote-binding.json`, active = `Active=1`).
- **Tag labels/aliases** → `RemoteTagLabels` table, PER-PROFILE (`IRemoteTagLabelStore`; were global on the
  source config → leaked across profiles). **Seed-once:** first access copies the source's shipped
  `TagLabels` into the profile store, then the profile owns an independent copy. Shape `sourceId → lang →
  rawTag → label`. Read in `RemoteBrowseService.GetSources` + alias-search in `RemoteIndexService.QueryAsync`.
- **Site adapter configs** — the GLOBAL `{data}/remote-sources/*.json` STAY the editable definition (+
  `res/remote-sources` shipped seeds); each profile MIRRORS them into `RemoteSources` (`RemoteSourceRepository`,
  full config JSON in one column). `RemoteSourceStore.GetAll` re-syncs JSON→SQLite when the JSON mtime
  signature changed, then reads SQLite. Per-profile mirror, NOT a global app.db (decision stands).
- **Mod → library FK.** `Mods.RemoteLibraryId` (FK) — the mod references its library; the library owns the
  NAME (never copied onto the mod). Set on import; existing mods backfilled once
  (`ModRepository.BackfillRemoteLibraryReferencesAsync`, fire-and-forget at profile init).
  `ModEnrichmentService.PopulateLibraryNames` resolves `ModInfo.LibraryName` LIVE by FK (computed, like
  `CategoryName`) → frontend comprehensive search matches it.

## 3-tier config resolution (parameterized sources)

A source declares library-configurable `Params` (`{ key, label, type: input|select, options, default,
required }`); a library supplies `ParamValues` that substitute for `{param.<key>}` in the EFFECTIVE config.

**Resolution (`IRemoteSourceResolver`, PURE + JSON-level): `res base ← sparse local overlay ← param
substitution`.** Sparse overlay = "absent key = inherit" so res updates to untouched fields flow through;
`Diff` emits the sparse overlay (RAW JSON — a typed config can't be sparse). `RemoteBrowseService.Effective(
sourceId, listId)` builds the effective config (library via `FindBySourceList`; the index crawls via
Browse, so params flow to the index too) — inert until a library sets values, so zero regression.
`GetDetailAsync`/`DetailProvidesTags` take `listId` → `Effective` too.

- **`RemoteSourceStore` is 2-tier:** `res/remote-sources` = runtime BASE, `data/remote-sources` = SPARSE
  overlays (or full custom sources). `GetAll` unions res∪data + resolves; `Save` writes only the DIFF vs
  res (res files never written); `Delete` reverts a res source to default / removes a custom one.
- **`Diff` treats empty ≈ absent** (`RemoteSourceResolver.DiffNode` → no diff when BOTH sides are
  null/absent/``/[]/{}`). The editor's `BLANK` fills optional fields with `""` while master omits them;
  without this that wrote a spurious overlay → a fake "Modified" chip.
- **Origin = REAL diff, not file-existence.** `GetOrigins`/`RemoveNoOpOverlays` judge an overlay by its
  RESOLVED effect: `HasRealDiff(master, effective)` = `Diff` has more than just `id`. A sparse overlay that
  resolves back to master (reverted every field, OR res caught up) → origin `default` AND the overlay is
  DROPPED on load/save. `HasRealDiff` is the ONE shared test (Save's drop-on-revert + origin both call it —
  don't inline a second `id`-only check). `Save` DELETES the overlay when it matches master (no no-op file).
- Serialization for all of the above uses the shared `RemoteJson` (`Compact`/`Sparse`/`Pretty`) — never a
  fresh per-service `JsonSerializerOptions`.

## Source manager + editor UX

- **Origin chip:** `default` → NO chip; `customized` → amber **"Modified"** (`DiffOutlined` +
  `remote.origin.customizedHint`, invites edit→compare); `custom` → info chip. From `RemoteSourceInfo.Origin`.
  Custom sources keep a delete; Modified/Default have only Edit.
- **Test-connection = a MODAL** (`RemoteSourceTestDialog` on `FormDialog`): picks the game/list (when >1) +
  supplies the source's params, auto-runs once on open. `RemoteBrowseService.TestConfigAsync(config, listId,
  paramValues)` resolves `{param.*}` BEFORE testing and returns network/parse/validation failures as DATA
  (`Success=false` + `Error`; only cancellation propagates). `RemoteSourceTestResultView` (L2, pure props)
  → per-check `StatusTag`s or a red failure.
- **Per-field "compare with default"** (`RemoteSourceCompareDialog`, L2): side-by-side Yours | Default,
  changed span highlighted (`splitDiff`), empty-ish equality, **Take all** / **Revert selected** copy the
  DEFAULT value into the working config (no new revert path — Save then drops the field from the sparse
  overlay). Shown ONLY when `origin === 'customized'`. Backed by `IRemoteSourceStore.GetDefault(sourceId)`
  (res base resolved with NO overlay; null for a custom source; IPC `GET_SOURCE_DEFAULT`).
- **Dirty-tracking** — Save disabled when nothing changed. Both editors compare `canonicalJson(current)` vs a
  baseline (sorted-key JSON; the shared `shared/utils/canonicalJson.ts`).

## Library editor UX

- **3 tabs: Detail · Input rules · Tag labels** (`RemoteLibraryManagementScreen`). Detail = Name / Source ·
  Game / the source's Params. Verbose per-list hints live on the tab-label `Tooltip` (hover), NOT on the
  page. Editing a library or a site is a dedicated `--fill` screen (pinned `← header` + scroll body + pinned
  footer); site editing is LIFTED here (`editSource` state), `RemoteSourceManagerScreen` is list-only.
- **Scale:** rules + aliases grow to hundreds → `PaginatedEditList` (a search filter shown once a list
  exceeds 6 rows with an always-on shown/total count, then pagination at 15/page so only a page of antd
  `Select` rows mounts). Rows keep their REAL index; Add jumps to the last page; the rule filter matches
  name/tags/alias-label/title/category and **disables reorder while active**.
- **NO column-header row** on either list — placeholders convey the fields. Input rules keep a **leading
  order badge** (first-match-wins); Tag labels have NONE (unordered).
- **Tag-label row = searchable single-select + label, ONE row per tag.** The tag `CompactSelect` EXCLUDES
  tags already used by another row (`usedAliasTags`) so a tag carries only one label and the picker lists
  only unconfigured tags. The rule tag-picker maps options through `aliasLabel(tag)` (reflects unsaved
  edits); the stored/matched value stays the RAW tag.

## Local mod ↔ remote origin

- **`RemoteSourceChip`** (`ModPreviewPanel/`) sits at the RIGHT of the mod-detail **category row** (moved off
  the title row in #33 — it was crowding the title). It **sizes to its content and uses the free space**
  (`flex:0 1 auto; min-width:0; margin-left:auto`), so a short category shows the FULL library name; it
  shrinks + ellipsizes (capped `max-width:70%`) only when the category + chip can't both fit — do NOT
  re-add a fixed `max-width:45%` (that truncated early while the row had slack). Shows the library name (or
  `remote.remoteLibrary` fallback); CLICKABLE when the mod still has `metadata.remote` → opens
  `RemoteModDetailScreen`. The `ModList` ROW keeps its OWN cyan `libraryName` `<Tag>` (a raw inline tag in
  the wrapping `mod-list-item-tags` Space, NOT this component). (Replaced the old globe-only `RemoteSourceLinkIcon`.)
- **`source:` search field** — `searchQueryParser` has a `source` field; `ModListPanel` maps
  `mod.libraryName` → `record.source`, registers the localized `来源:` prefix. Search-help examples are i18n
  keys (`mods.search.ex*`) so the CN hint shows CN examples.
- **Browse tag-filter bar** (`RemoteLibraryView`): horizontal `Tag.CheckableTag` strip from `INDEX_TAGS`
  (mouse-wheel → horizontal scroll via a non-passive listener; active chip scrolls into view). PARKED: for
  hundreds of GameBanana tags, move filtering into a dropdown/slide-in panel (future upgrade, not a regression).

## GameBanana specifics (endpoints in remote-library.md)
- Search: `apiv11/Util/Search/Results?_sModelName=Mod&_sSearchString={q}&_idGameRow={gameId}&_nPage={n}`.
- Download: direct (`/dl/{id}`), resolver type `direct`, real filename from `_sFile`. Order: newest-first by
  `_tsDateAdded` (captured as DateHint). `SeedMissing` ADDITIVELY appends newly-shipped `lists` by id.

## IPC surface
`LIBRARY_GET_STATE/ADD/UPDATE/REMOVE/SET_ACTIVE` (+ add-with-sync), `INDEX_TAGS`, `INDEX_QUERY` (`tag`,
`importedOnly`), `DOWNLOAD_IMPORT {listId, entryId, tags}`, `SEARCH`, `GET_IMPORTED_STATE`, `GET_SOURCE_CONFIG`,
`GET_SOURCE_DEFAULT`, `TEST_SOURCE`, `LABELS_GET`/`LABELS_SET`, `SAVE_SOURCE`/`DELETE_SOURCE`.

The import↔index orchestration (`QueryAnnotatedAsync` = query + imported-annotation, `MergeDetailTags`) lives
in `RemoteIndexService`, NOT the facade — the facade is a thin delegate (DESIGN_DECISIONS §5).

## DO NOT re-add (tried + reverted — re-proposing these repeats resolved reviews)
- **A management tag SEARCH box** — tag find-ability belongs to the browse bar, not the editor (removed 2026-07-07).
- **Column-header rows** on the rule/alias lists — dropped for parity; placeholders convey the fields.
- **A per-source reset/resync icon** on the Sites row — "you don't really need the reset button". The one way
  to revert a Modified source is the editor's **Compare with default → Take all → Save** (Save drops the overlay).
- **A GLOBAL "rebuild the mirror" Resync button** — wrong reading of "resync"; reverted.
- **A fixed-chip alias field or a 2-up alias grid** — the chip killed searchability, the grid cramped the picker.
- **Source-config mirroring, origin chips, or resync ON libraries.** A library only REFERENCES its source
  (`sourceId`/`listId` + `paramValues` + local rules); it stores NO copy of the source config. Effective config
  is resolved live (`RemoteBrowseService.Effective`). Those are source-level concerns (Sites tab / source editor) ONLY.

## Guards (tests)
`RemoteSourceStoreTests` (seed/sync/edit/drop, Save/Delete, real/no-op/custom origins, drop-no-op overlay,
`GetDefault`, append-not-overwrite), `RemoteSourceResolverTests` (merge/substitute/diff/empty≈absent/round-trip),
`RemoteIndexServiceTests` (sync + `QueryAnnotated`), `RemoteLibraryStoreTests` (CRUD + JSON→SQLite migration +
ParamValues), `RemoteTagLabelStoreTests` (cross-profile isolation + seed-once), `RemoteLibraryModLinkTests`
(FK backfill + name enrichment), `RemoteBrowseServiceTests` (test-connection), frontend
`canonicalJson`/`PaginatedEditList`/`RemoteSourceTestResultView`/`RemoteSourceCompareDialog`/`RemoteSourceEditor`.

## Rules that bind
`remote-library.md` (site/API facts), `download-service.md`, `background-task-tracking.md`,
`use-project-paths.md`, `enum-serialization.md` (tags/rules camelCase), `ui-component-layers.md`,
`in-app-guide.md`, `module-boundaries.md` (facade→service).
