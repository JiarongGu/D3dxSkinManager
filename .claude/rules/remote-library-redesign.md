# Remote library — redesigned foundation (agreed 2026-07-06, rebuild in progress)

The first remote-library cut (single per-profile binding, single category per mod, category-on-binding)
was the wrong foundation. This is the agreed rebuild. Supersedes the binding model in `remote-library.md`
(keep that file for the site/API facts — huihui + GameBanana endpoints — which are still valid).

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

## Phased implementation
1. **Engine abstraction**: `IRemoteSiteEngine` + `HttpRegexEngine` (extract current regex logic from
   RemoteBrowseService) + `GameBananaEngine` (wrap the existing static parser); RemoteBrowseService →
   thin dispatcher. Registered by EngineId in DI.
2. **Data structure (backend)**: `RemoteLibrary` model + `RemoteLibraryStore` (per-profile
   remote-libraries.json; add/remove/update/setActive/getActive) — additive alongside the old binding
   first so nothing breaks. Index `Category`→`Tags` (migration) + GameBanana super+sub capture +
   generalized tag filter. Import applies the active library's ordered tag-rules.
3. **Facade + IPC**: library CRUD + switch; query/filter by tag; gamebanana search.
4. **Frontend**: main-screen library switcher; library management (add: site→game→tag-rules; edit/remove);
   tag filter + tag chips; detail left/right split. Retire the single-binding UI + the per-binding
   default-category picker.
5. Remove the old binding model once all callers move to libraries.

Rules that bind: `download-service.md`, `background-task-tracking.md`, `use-project-paths.md`,
`enum-serialization.md` (tags/rules camelCase on the wire), `ui-component-layers.md` (L1 atoms),
`filesystem-operation-serialization.md` (n/a — this is profile JSON + the index DB, not mod archives).
