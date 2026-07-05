# Remote library — redesigned foundation (agreed 2026-07-06, rebuild in progress)

The first remote-library cut (single per-profile binding, single category per mod, category-on-binding)
was the wrong foundation. This is the agreed rebuild. Supersedes the binding model in `remote-library.md`
(keep that file for the site/API facts — huihui + GameBanana endpoints — which are still valid).

## Full-code review (2026-07-06, all 17 backend files + 4 components read end-to-end)

**SOUND — keep as-is:**
- Index storage: per-profile SQLite, composite key (SourceId, ListId, EntryId), generation ordering,
  soft-delete pruning, incremental stop-at-known-page, sync meta. Engine-agnostic + tested.
- Image cache (sha1-named, per-profile, app://-servable, gated downloads, cleanup category).
- Download-host resolvers (cloudreve/direct/external) + CloudreveShareResolver — host logic is correctly
  SEPARATE from site engines (a site references hosts; hosts recur across sites).
- Fire-and-forget syncs/imports via ProcessRegistry + frontend auto-refresh on completion.
- `res/` read-only defaults + seed-if-missing; entry-id extraction (stable site id).

**STRUCTURAL FLAWS — the rebuild fixes these:**
1. Single binding per profile (+ DefaultCategoryId ON the binding, picker in the browse toolbar) —
   wrong model entirely → RemoteLibrary[] + tag rules + management screen.
2. One `Category` per mod → must be `Tags[]` (GameBanana super+sub). The Category column/filter
   (migration 202607060002) is already obsolete → 202607060003 swaps it for Tags.
3. **Import identity = detailUrl string equality** — breaks when a site moves hosts (huihui does!).
   → record `{sourceId, listId, entryId}` in mod Metadata; match "imported" by entryId.
4. **Imported-flag is O(all mods) JSON-parse per INDEX_QUERY page** (GetImportedDetailUrlsAsync walks
   every mod row each page flip; 2589 mods = 2589 parses) → in-memory cache invalidated on import/delete.
5. RemoteBrowseService inlines `if (IsGameBanana)` + regex parsing → IRemoteSiteEngine dispatch.
6. RemoteSourceConfig conflates identity + engine choice + http-regex fields + games + resolvers;
   validation special-cases engine names → engines own/validate their config slice.
7. RemoteLibraryView is a 507-line monolith (setup + bound + toolbar + grid) → decompose with the
   switcher work.
8. Search is huihui-only; must be an engine capability (SupportsSearch + listId-scoped).
9. Detail page single-column → left (review/gallery) / right (actions; sites can have MANY links).
10. remoteUiStore keyed by sourceId/listId → key by active library id.

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
- `RemoteBrowseService` becomes a thin **dispatcher**: pick the engine by `cfg.Engine`, delegate, done.
  No more `if (isGameBanana) … else regex` inline.

**GENERALIZED (engine-agnostic) — shared by ALL engines:** the mod entry, its **tags**, sync status /
generation / pruning, the per-profile **index** (SQLite), library config + tag-rules, download+import,
image cache. Engines ONLY know how to fetch + normalize their site; they never touch storage. So adding
a site = write one engine (+ optional small config); the mod/tag/sync/import machinery is reused as-is.

## Core concepts

### Sites/adapters = READ-ONLY DEFAULTS in `res/`
Shipped site configs (`res/remote-sources/*.json`) are DEFAULT setups — **not edited in the app**. They
define the available sites (engine `http`/`gamebanana`, games, download resolvers). "Adapter" and "the
thing the user manages" are NOT the same: the user manages **libraries**, not adapters. (A future
"advanced: add a custom site" path may write a user adapter, but it's not the primary flow.)

### A profile has MANY libraries (not one binding)
Replaces the single `remote-binding.json`. A profile owns a **list** of configured libraries; the main
screen **switches** between them; they're added/removed in **library management**.

```
RemoteLibrary {
  id            // instance id
  sourceId      // which site (from res defaults)
  listId        // which game on that site
  name          // display, e.g. "GameBanana · Genshin"
  tagRules      // ORDERED list — see below
  addedAtUtc
}
remote-libraries.json = { libraries: RemoteLibrary[], activeLibraryId }
```

### Each remote mod has MULTIPLE TAGS (not one category)
Index entries carry a **list of tags**, rendered like mod tags (but for remote), and the toolbar filter
is a **tag filter**. GameBanana = **2 tags**: super-category (`_aRootCategory._sName`) + sub-category
(`_aCategory._sName`). Migration replaces the index `Category` column with `Tags` (JSON array).

### Import → local category = ORDERED TAG RULES (per library)
Each library has an **ordered** rule list. Each rule: `{ name, tags: [one or more], categoryId }`.
On import, evaluate rules **in order**; the **first** rule whose tags ALL match the mod's tags wins →
its `categoryId`. No rule matches → **uncategorized** (the default). This is the configurable
"matching logic" — single-or-multiple-tag rules, named, ordered, default uncategorized.

```
tagRules: [
  { name: "Skins→Skins",  tags: ["Skins"],            categoryId: "<local Skins>" },
  { name: "Hu Tao skins",  tags: ["Skins","Hu Tao"],   categoryId: "<local HuTao>" },  // multi-tag
]
// first match wins; else uncategorized
```

### Detail page = LEFT/RIGHT split
Review (gallery/preview/info) on the LEFT, actions (download links — some sites have MANY) on the RIGHT.
(Replaces the current header-actions + gallery-below layout.)

## GameBanana specifics (endpoints in remote-library.md)
- **Search**: `apiv11/Util/Search/Results?_sModelName=Mod&_sSearchString={q}&_idGameRow={gameId}&_nPage={n}`
  (confirmed exists; response mirrors the Subfeed `_aRecords`). Wire it as the gamebanana engine's search.
- **Tags**: super = `_aRootCategory._sName`, sub = `_aCategory._sName`.
- **Download**: direct (`/dl/{id}`), resolver type `direct`, real filename from `_sFile`. DONE.
- **Order**: sort each page newest-first by `_tsDateAdded` (DONE) + capture as DateHint.

## Phased implementation — ALL PHASES SHIPPED 2026-07-06
1. ✅ **Engine abstraction**: `IRemoteSiteEngine` + `RemoteSiteEngineBase` (shared fetch/URL) +
   `HttpRegexEngine` + `GameBananaEngine` (instance engine, statics kept testable, game-scoped search);
   `RemoteBrowseService` = thin dispatcher (containment check central); DI via TryAddEnumerable.
2. ✅ **Standardized data**: migration 202607060003 drop+recreates RemoteIndexEntries with a `Tags`
   JSON column (index = re-syncable cache); tag filter via `json_each` exact match; INDEX_TAGS distinct
   counts; GameBanana super (subfeed `_aRootCategory`) + sub (ProfilePage `_aCategory`) as tags.
   `RemoteLibraryStore` ({profile}/remote-libraries.json, legacy binding auto-upgrades); import records
   the durable identity {sourceId, listId, entryId} in Metadata.remote; imported-lookup cached
   (TTL 30s + invalidate-on-import); ordered tag-rules → category (`MatchTagRules`).
3. ✅ **IPC**: LIBRARY_GET_STATE/ADD/UPDATE/REMOVE/SET_ACTIVE (+ add-with-sync), INDEX_TAGS, INDEX_QUERY
   `tag`, DOWNLOAD_IMPORT {listId, entryId, tags}, SEARCH listId; binding routes + RemoteBindingStore DELETED.
4. ✅ **Frontend**: library switcher in the toolbar; `RemoteLibraryManagementScreen` (libraries CRUD +
   ORDERED tag-rules editor with reorder + sites/adapters section embedded); tag chips on cards + tag
   filter; detail LEFT (gallery/tags) / RIGHT (actions) split; empty state → "add library".
5. ✅ Binding model removed.
Verified live 2026-07-06: legacy binding upgraded, add+sync (517 entries), switch, tags on cards/detail,
rules editor UI. Tests: 43 backend remote + 204 frontend.

Rules that bind: `download-service.md`, `background-task-tracking.md`, `use-project-paths.md`,
`enum-serialization.md` (tags/rules camelCase on the wire), `ui-component-layers.md` (L1 atoms),
`filesystem-operation-serialization.md` (n/a — this is profile JSON + the index DB, not mod archives).
