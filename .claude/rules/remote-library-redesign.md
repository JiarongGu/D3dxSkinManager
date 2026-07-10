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
`filesystem-operation-serialization.md` (n/a — this is profile JSON + the index DB, not mod archives),
`in-app-guide.md` (the user guide documents this feature).

## Post-ship additions (2026-07-06)
- **Imported tracking = "downloaded" filter + "locate" + local-mod tagging.** `RemoteImportService`
  imported-lookup now returns MAPS (`key→modId`, `legacyUrl→modId`, not sets); `RemoteFacade.QueryIndexAsync`
  sets `entry.Imported` + `entry.LocalModId` and takes `importedOnly` (computes this source/list's imported
  entry-ids from the key map → `RemoteIndexService/Repository.QueryAsync(onlyEntryIds)` → SQL
  `EntryId IN @ids`, empty set = no rows). Frontend: `remoteUiStore.downloadedOnly` + a toolbar toggle;
  the card ✓ badge is a compact icon; the detail page shows an "already imported" banner with a **View
  mod** button → `navigateToModSearch(profileId,[localModId])` (closes the slide-in). On import, the local
  mod is TAGGED with the remote entry's tags (`entity.Tags = JSON(existing ∪ allTags)`) so it carries the
  same taxonomy. The remote list auto-refreshes when a `process.remoteImport` process COMPLETES (watch the
  completed count — NOT `MOD_LIST_UPDATED`, which fires before the metadata is written).
- **Quark large-file download**: the resolver uses the quark **desktop-client** User-Agent
  (`quark-cloud-drive/… Electron … Channel/pckk_other_ch`), NOT a browser UA — Quark gates its download
  size limit (apiv1 code 23018) by product/UA. `REMOTE_QUARK_SIZE_LIMIT` is the fallback message. See
  `remote-library.md` for the full Quark flow + `download-service.md` (body surfaced on non-2xx).
- **Management edit-view UX pattern (`RemoteLibraryManagementScreen`).** Editing a LIBRARY or a SITE is a
  dedicated `--fill` screen (pinned `← header` + scrollable body + pinned footer actions), NOT inline —
  the tabs/hint are hidden while editing. Site editing is LIFTED to the management screen (`editSource`
  state); `RemoteSourceManagerScreen` is list-only and routes edit/add up via `onEdit`. The main 库/站点
  `Tabs` is CONTROLLED (`mainTab` state) so returning from an editor preserves the tab. List rows use
  `box-sizing: border-box` (a `width:100%` content-box row overflowed + got right-trimmed). Tag pickers
  use `CompactSelect` (never raw antd `Select`). Section titles use `CompactSection`/`CompactTitle`
  (14px — see `ui-design-rules.md`).

## Post-ship additions (2026-07-10)
- **Title-derived tags for TAGLESS sites** — `RemoteSourceConfig.TitleTagPattern` (regex, named group
  `tag`; huihui seed `^(?<tag>\S+)\s` = the character name before the first space). Applied CENTRALLY in
  `RemoteBrowseService` (Browse/Search cards + GetDetail) after the engine normalizes, ONLY to entries
  with no tags of their own — so browse, search AND the index sync all agree. A bad/user-broken regex
  never breaks browsing (`DeriveTitleTag` swallows, 250ms timeout). Exposed in the site editor form
  (`remote.fieldTitleTag`); `RemoteSourceStore.SeedMissing` does the ADDITIVE upgrade (fills the field
  into an existing user config when null — same mechanism as cardScopePattern). Existing index entries
  get the tag on the next full reindex (incremental stops at known pages).
- **Detail screen live imported-banner** — new IPC `GET_IMPORTED_STATE` {sourceId, listId?, entryId?,
  detailUrl?} → `{ imported, localModIds }` (RemoteFacade queries the cached imported lookup). The
  detail screen seeds from open-time props and re-queries when a `process.remoteImport` process COMPLETES
  (same processStore completed-count trigger as the browse grid) — no reopen needed.
- **Mod detail → remote page backlink** — remote imports record `metadata.remote` on the mod;
  `ModInfoSection` parses it (`shared/utils/modRemoteRef.ts`) and shows a 来源 row whose button opens
  `RemoteModDetailScreen` directly (cross-module React import, headless slide-in, `imported` +
  `localModIds=[mod.id]` preset).

## Browse tag-filter bar (2026-07-07)
`RemoteLibraryView` shows a horizontal **tag-filter chip strip** below the toolbar: `Tag.CheckableTag`
chips from `INDEX_TAGS` (`remote.tagAll` "All" + one per tag with its count), click → sets
`remoteUiStore.tagFilter` → re-queries the index (same path as sort/downloaded). UX baked in: the active
chip **scrolls into view** (addressed by child index, not antd's internal class), trailing **end-padding**
on the strip, and **mouse-wheel → horizontal scroll** (a non-passive `wheel` listener flips vertical wheel
delta to `scrollLeft` — React's synthetic `onWheel` is passive and can't `preventDefault`). Only rendered
once the index has tags.
- **PARKED (next):** with large tag counts (GameBanana = hundreds), move the click-filter into a
  **dropdown or slide-in panel** for better scanning. The wheel-scrollable strip is the agreed keeper
  "for now" — the panel is the future upgrade, not a regression to fix.
- **Management has NO tag search box — deliberately.** A per-alias search filter was tried in the
  `RemoteLibraryManagementScreen` tag-label (aliases) editor and **removed** at the user's request
  (2026-07-07). Do not re-add a search there; tag find-ability belongs to the browse bar, not the editor.
