# Remote mod library — site adapters + Cloudreve download (GROUNDED 2026-07-05)

Feature: browse remote mod sites in-app → download → one-click import into the current profile.
**Config-driven, game-agnostic**: every site is a JSON adapter (`RemoteSourceConfig`) so new
libraries can be added without code (regex-based extraction v1; the fetch layer is a seam so a
WebView2-rendered engine can be added for JS-heavy sites later).

## Engines: "http" (regex-over-HTML) vs "gamebanana" (JSON API)
The adapter `engine` field selects the PARSER (both fetch over plain HTTP via `IRemotePageFetcher`):
- **`http`** (default) — regex extraction over server-rendered HTML (huihui). Uses `cardPattern`/
  `detailTitlePattern`/`downloadLinkPattern`/etc.
- **`gamebanana`** — GameBanana's apiv11 JSON API (`GameBananaEngine`, verified live 2026-07-06).
  The HTML regex fields are unused (empty in the seed); `RemoteBrowseService` dispatches to
  `GameBananaEngine.ParseSubfeed`/`ParseProfilePage`, and `RemoteSourceStore.Validate` skips the
  http-only regex requirements when `engine=="gamebanana"`.

### GameBanana (gamebanana.com) — apiv11 JSON, verified live 2026-07-06
Not scrapeable HTML — a JSON API. `RemoteSources/gamebanana.json` seeds it. Endpoints:
| Page | URL |
|------|-----|
| List, page N | `{base}/apiv11/Game/{gameId}/Subfeed?_nPage={N}&_sSort=new` → `_aRecords[]` + `_aMetadata{_nRecordCount,_nPerpage}` |
| Detail + download | `{base}/apiv11/Mod/{id}/ProfilePage` → `_aFiles[]._sDownloadUrl` (DIRECT `gamebanana.com/dl/{fileId}`) + `_aPreviewMedia._aImages[]` |

- **Record fields:** `_idRow`, `_sModelName` (filter to `"Mod"`), `_sName`, `_sProfileUrl` (detail URL
  `.../mods/{id}` — the `entryIdPattern` `"/mods/(?<id>\d+)"` keys the index), `_tsDateAdded`,
  `_aPreviewMedia._aImages[0]` (`_sBaseUrl` + `/` + `_sFile530` for cards, `_sFile` for the gallery).
- **Download is trivial:** files are direct URLs → resolver `type: "direct"` (already handled by
  `RemoteImportService.ResolveAsync`). No Cloudreve-style multi-step, no auth.
- **NSFW comes FREE:** content-rated mods are ALREADY in the Subfeed (`_sInitialVisibility` = show/warn/
  hide; page 1 of Genshin had 3 content-rated). We index every record → adult content is included with
  NO login or extra param. (A fully-restricted class needing an account may exist; anon subfeed covers
  the common case.)
- **Game ids (the XXMI games, verified record counts 2026-07-06):** Genshin `8552`, ZZZ `19567`,
  WuWa `20357`, HSR `18366`. Feeds are LARGE (Genshin 1243 pages) — first full sync is long but
  cancellable; incremental Update stays cheap (the `MaxPages=500` backstop caps a runaway crawl).
- **Our `D3dxSkinManager` User-Agent is accepted** by apiv11 (live browse+detail confirmed, no 403).
- Search: not wired for GameBanana v1 (`HasSearch=false`); apiv11 has a search endpoint to add later.
- Tests: `GameBananaEngineTests` (parse subfeed/profilepage, mod-id, url shapes).

## First supported site: huihui168.org ("Hui站") — verified by live probes 2026-07-05

**Server-rendered** — plain HTTP with a browser User-Agent works for every page (NO JS rendering,
no cookies, no anti-bot). URL scheme:

| Page | URL |
|------|-----|
| Game list, page 1 | `/?list_{N}/` — N: 1=鸣潮(WuWa), 2=绝区零(ZZZ), 3=星穹铁道(SR), 4=终末地(Endfield) |
| Game list, page P | `/?list_{N}_{P}/` (ZZZ had 146 pages) |
| Search | `/?keyword={query}` (URL-encoded) |
| Mod detail | `/?news_{M}/{id}.html` (M = list-specific, e.g. 12 for ZZZ; treat as opaque href from cards) |

**Card markup** (list + search pages, also the hot-swiper):
`<a href="/?news_12/2845.html"><img src="/static/upload/image/..." alt="反虚化3.0" ...></a>` with a
sibling `<h3>title</h3>`. Extraction regex (verified 52 cards/page):
`<a[^>]+href="(?<url>/\?news_[^"]+)"[^>]*>[\s\S]{0,600}?<img[^>]+src="(?<image>[^"]+)"[^>]*alt="(?<title>[^"]*)"`
— dedup by url (hot/recent sidebars repeat items). Images are relative → resolve against baseUrl.
**No hotlink protection** (probed with foreign/no Referer → 200), so the frontend `<img>` can load
site images directly.

**Detail page**: `<h1>` = title; content images = `<img src="/static/upload/...">`; download links
are plain anchors in the rich-text body, labeled by surrounding text:
- Hui盘 (Cloudreve): `https://cloudreve.huihui123.org/s/<key>` ← the resolvable one
- 夸克 (Quark): `https://pan.quark.cn/s/...` ← resolver type `quark` (needs a saved login — see below).

**Detail layout is TWO columns (verified `?news_14/9288.html`, 2026-07-06):** left `lg:w-3/4` =
artwork + `<h1>` + rich-text body (downloads, unzip password, switch keys); right `lg:w-1/4` sidebar =
avatar + third-party ad images + related mods. An unscoped `/static/upload/` img scan pulled the
sidebar junk into the gallery — fixed by two OPTIONAL `RemoteSourceConfig` fields (http engine only):
- **`detailScopePattern`** (`lg:w-3/4(?<scope>[\s\S]*?)lg:w-1/4`) — scopes image/download/description
  extraction to the main column; no match → whole page (fixture/other layouts still work).
- **`detailDescriptionPattern`** (`</h1>(?<description>[\s\S]*?)<div class=`) — rich-text body →
  plain text (`HtmlToPlainText`: br/p → newlines, tags stripped); carries switch-key/unzip-password
  info into `RemoteModDetail.Description` (shown by the detail screen).

## Cloudreve v4 share API (cloudreve.huihui123.org = "Hui盘") — fully anonymous, no login/captcha

Cloudreve is open-source; this instance is **v4** (`/api/v4`, confirmed via bundle + probes).
Three-step resolve, all verified live:

1. `GET /api/v4/share/info/{key}` → `{code:0, data:{id, name, visited, downloaded, unlocked,
   expired, source_type, owner…}}`. Gate on `unlocked==true` + `expired==false` (a passworded share
   has `unlocked:false` → not supported v1).
2. `GET /api/v4/file?uri=cloudreve://{key}@share` → `{data:{files:[{type(0=file,1=dir), name, size,
   path:"cloudreve://{key}@share/<name>", …}], parent}}` — works for BOTH single-file and folder
   shares; `path` is the exact URI for step 3. Pick the archive (largest file with an archive
   extension; single file share → that file).
3. `POST /api/v4/file/url` body `{"uris":["cloudreve://{key}@share/<name>"],"download":true}` →
   `{code:0, data:{urls:[{url:"https://pan.huihui123.org/…X-Amz-…"}]}}` — a **presigned S3 URL**,
   downloadable with a plain GET (feed to `IDownloadService`).

**URI shape is `cloudreve://{shareKey}@share/{path}`** — the share key is the URI *userinfo*, the
host is the literal fs name `share`. (`cloudreve://share/{key}` → "failed to decode hash id" — wrong.)
Non-zero `code` in a 200 body = error (`40081` aggregate, per-uri codes inside); message in `msg`.

## Quark pan (夸克网盘) share API — LOGIN cookie + SAVE-then-download-then-delete (VERIFIED E2E 2026-07-06)
No anonymous OR direct share-download endpoint. The working flow is 转存 (save the share file into the
user's OWN drive) → download from there → DELETE the copy (cleanup). All authed (apiv1 `ucpro`, host
`drive-pc.quark.cn`, `Cookie`+UA+Referer `pan.quark.cn`). `QuarkShareResolver`:
1. `POST /1/clouddrive/share/sharepage/token` `{pwd_id, passcode:""}` → `data.stoken` (`pwd_id` = `/s/{id}`).
2. `GET  /1/clouddrive/share/sharepage/detail?...&pwd_id&stoken&pdir_fid&_page&_size` → `data.list`
   (`fid`,`file_name`,`dir`,`size`,`share_fid_token`). **Root is often a FOLDER → recurse `pdir_fid`**; pick largest archive.
3. `POST /1/clouddrive/share/sharepage/save` `{fid_list, fid_token_list, to_pdir_fid:"0", pwd_id, stoken,
   pdir_fid, scene:"link"}` → `data.task_id`; poll `GET /1/clouddrive/task?task_id&retry_index` until
   `data.status==2` → `data.save_as.save_as_top_fids[0]` = the saved fid in the user's drive.
4. `POST /1/clouddrive/file/download` `{fids:[savedFid]}` → `data[0].download_url` (CDN `dl-*.pds.quark.cn`;
   the GET needs cookie+UA → `QuarkDownload.Headers`).
5. `POST /1/clouddrive/file/delete` `{action_type:2, filelist:[savedFid], exclude_fids:[]}` → task; poll.
- **Two resolve calls, ONE save:** confirm dialog + background both resolve. `ResolveAsync` is
  metadata-only (token+detail, no save); `PrepareDownloadAsync` (background) does save+url; `CleanupAsync`
  deletes. `RemoteImportService` branches on `type=="quark"`: prepare → download → cleanup right after
  the bytes land (drive freed early) AND in the `finally` (covers cancel/fail) with `CancellationToken.None`.
- **Cookie capture = in-app login window, not typed.** `ExternalLoginService` opens a native WebView2
  Form (persistent per-provider profile under `{data}/settings/webview-login/{provider}`). It reads
  cookies from the **API origin** `https://drive-pc.quark.cn` (NOT `pan.quark.cn` — the session cookies
  `__puus`/`__pus`/`__kps`/`__uid` live on the parent domain `.quark.cn`; `pan.quark.cn` host-only has
  none of them — this was the capture bug). **Window is HIDDEN-until-needed**: opens off-screen
  (`Location=-32000,-32000`, `ShowInTaskbar=false`), polls cookies; already-logged-in (persistent
  profile) → captures + closes WITHOUT ever showing (silent refresh); no session after ~2.4s grace →
  reveals centered for the user to log in. Saves the `Cookie` header to `IOnlineAccountStore`
  (`{data}/settings/online-accounts.json`, GLOBAL). Managed in **Settings → 在线存储 / Online Storage**
  (`OnlineStorageAccountsCard`; IPC `ACCOUNT_LIST`/`ACCOUNT_LOGIN`/`ACCOUNT_REMOVE`). **Logout** removes the
  stored cookie AND `ClearProfile` deletes the WebView2 profile folder (else it silently auto-logs-in).
  Not logged in → resolver throws `QUARK_NOT_LOGGED_IN`.
- **Save target = a DEDICATED drive folder, not root.** `QuarkShareResolver.EnsureAppFolderAsync`
  finds/creates `D3dxSkinManager` (const `AppDriveFolder`, reusable for future cloud resolvers) in the
  drive root and 转存 into it (`to_pdir_fid = folder fid`); cleanup deletes the saved file, folder persists.
  Quark mkdir: `POST /1/clouddrive/file {pdir_fid:"0", file_name, dir_path:"", dir_init_lock:false}` →
  synchronous `data.fid`. Drive root listing: `GET /1/clouddrive/file/sort?pdir_fid=0`.
- **Downloaded file → managed downloads** (`{data}/downloads`, self-cleaning), NOT profile temp; only
  the extract/repack staging is temp.

### huihui "safe keep" disguise → the RECURSIVE-UNWRAP workflow (config-gated, GROUNDED e2e 2026-07-06)
huihui's 夸克 uploads are NOT the mod directly — they're a multi-layer disguise (a 网盘 anti-scan trick):
`1.mp4` = a REAL mp4 with a **zip APPENDED** (polyglot; the zip's offsets are archive-relative so a
whole-file reader rejects it) → inside: `MOD….7z.001` = an **encrypted 7z** (password `huihui`) →
inside: `<name>.7z` (plain 7z) → the real mod (`chen.ini` + `Meshes/*.buf`). So the import must:
- extract by **magic bytes**, not extension; **CARVE** a polyglot (find the first archive signature at a
  non-zero offset, extract from there — `ArchiveHelper.TryCarveEmbeddedArchive`);
- **recursively unwrap** nested archive layers, trying the password **per layer**, flattening the final
  real content to the root — `ArchiveHelper.ExtractArchiveRecursiveAsync` (a "wrapper layer" = only
  archive(s) + trivial junk `.url/.txt`; stop at real content).
This is a **reusable, CONFIG-GATED workflow**: `RemoteResolverRule.UnwrapNested` (→ `RemoteDownloadOption
.UnwrapNested`) opts a host in; huihui's quark resolver sets `"unwrapNested": true`. Off = a plain single
extract (plain, password-retry). Any other site wrapped the same way just sets the flag.
- **Gotcha (fixed):** `IsPasswordError` treats "data error"/"corrupted" as password-suspect; the carve
  fallback must NOT be pre-empted by that — `ExtractArchive` records the password error, tries carve
  first, and only throws the password error if carve also fails (so the caller's password-retry still runs).
- **Verified e2e** against a real account: silent capture → save into `D3dxSkinManager` folder → 34.9MB
  download via cookie'd CDN → carve+unwrap (mp4→zip→7z/huihui→7z→mod) → repack 7z (7MB, `chen.ini`+Meshes
  at root) → import; saved copy + folder left clean. Adding another auth'd host = new resolver `type` +
  a `LoginTarget` entry in `ExternalLoginService` (+ `unwrapNested` if it disguises the same way).

## Architecture (mirrors the app's module conventions)

```
Modules/Remote/
  Models/            RemoteSourceConfig (the JSON adapter), RemoteModCard, RemoteModDetail,
                     RemoteDownloadOption, RemoteResolveResult, RemoteIndexEntry/Info/Cache/Page
  Services/
    RemoteSourceStore     — loads {data}/remote-sources/*.json; SEEDER copies shipped adapters
                            ({data}/remote-source-seeds/, csproj Content from
                            D3dxSkinManager/RemoteSources/*.json) whose id isn't configured yet —
                            user edits never overwritten; drop a JSON to add a site
    IRemotePageFetcher    — GetStringAsync/PostJsonAsync seam; HttpPageFetcher (via IDownloadService)
                            is v1; a WebView2PageFetcher can back JS-rendered sites (config `engine`)
    RemoteBrowseService   — list/search/detail: fetch page → run the config's regex extraction →
                            DTOs (absolute URLs)
    CloudreveShareResolver— the 3-step API dance above (config resolver type "cloudreve")
    RemoteIndexService    — SYNCED LOCAL INDEX per source+list ({data}/remote-sources/.cache/):
                            background crawl of all pages (cancellable registry entry, 250ms delay,
                            checkpoint saves); entries keyed by the site's stable id
                            (config `entryIdPattern`), date hint from `imageDatePattern`,
                            first/last-seen, site recency order; Query = instant local
                            filter/search/paging
    RemoteImportService   — fire-and-forget download+import: ProcessRegistry entry → resolve →
                            IDownloadService.DownloadAsync into {profile}/temp → **NORMALIZE
                            (2026-07-06): extract + recompress to 7z — downloads are NEVER stored
                            verbatim (passworded/odd containers would fail at load). Plain extract
                            first; on a password-suspect failure (7z reports missing AES password as
                            "Data error"/corrupted — indistinguishable from corruption) retry with
                            the user's confirm-dialog password or the resolver's `unzipPassword`
                            (huihui: "huihui"); still failing → REMOTE_ARCHIVE_PASSWORD** →
                            ModImportService.ImportAsync(normalized) → site title as name + previews
                            + records `remote:{sourceId, detailUrl, sha256, importedAtUtc}` in
                            ModEntity.Metadata (→ the index's `imported` flag / 已导入 badge)
  RemoteFacade         — GET_SOURCES / BROWSE / SEARCH / GET_DETAIL / RESOLVE_DOWNLOAD /
                          DOWNLOAD_IMPORT / INDEX_QUERY / INDEX_SYNC (long ops ack immediately;
                          progress via Activity panel)
```

Rules that bind here: `download-service.md` (ALL HTTP through `IDownloadService` — it grew
`PostJsonAsync`/JSON GET for the Cloudreve API; never `new HttpClient`), `background-task-tracking.md`
(download+import is ONE cancellable process with staged progress), `use-project-paths.md` (downloads
stage in `{profile}/temp`, same volume as the archive store), `enum-serialization.md` (any enums
camelCase on the wire).

## Etiquette / robustness
- Always send a browser-ish User-Agent; the site 200s plain fetches today, but keep requests low —
  cache list pages briefly, never prefetch detail pages in bulk.
- `share/info.visited`/`downloaded` are live counters — don't poll them.
- The site moves hosts (VPN mirrors, IP fallback in the site notice) — baseUrl is config, never
  hard-coded; download host (cloudreve.huihui123.org) comes from the detail page anchor, not config.
- Quark links (and any unresolvable host) render as external-open buttons, not import.
