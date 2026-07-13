# Remote mod library — site adapters + Cloudreve download (GROUNDED 2026-07-05)

Feature: browse remote mod sites in-app → download → one-click import into the current profile.
**Config-driven, game-agnostic**: every site is a JSON adapter (`RemoteSourceConfig`) so new
libraries can be added without code (regex-based extraction v1; the fetch layer is a seam so a
WebView2-rendered engine can be added for JS-heavy sites later).

## Transport vs parser — `fetcher` ("http" | "webview") is SEPARATE from `engine` (SHIPPED 2026-07-11)
`RemoteSourceConfig.Engine` picks the PARSER (http-regex vs gamebanana-json). A NEW field
`RemoteSourceConfig.Fetcher` picks the TRANSPORT, independent of the parser:
- **`http`** (default) — plain requests via `IDownloadService` (`HttpPageFetcher`).
- **`webview`** — render the page in a single, persistent, OFF-SCREEN WebView2 and read the
  JS-produced DOM (`document.documentElement.outerHTML`) — for JS-heavy/anti-bot sites a plain GET
  returns empty. `WebView2PageFetcher` (modeled on `ExternalLoginService`'s proven off-screen pattern:
  `IFormInteractionService.GetMainForm` + `BeginInvoke` to the UI thread, one hidden window reused,
  navigations serialized by a `SemaphoreSlim`, POST delegates to plain HTTP since JSON APIs need no
  render). Wiring chain: `RemoteSourceConfig.Fetcher` → `IRemotePageFetcherRouter.For(config)`
  (`RemotePageFetcherRouter` picks by `FetcherId`) → `RemoteSiteEngineBase.FetchAsync(config, url, ct)`
  (engines are transport-unaware) → DI in `RemoteServiceExtensions` (both fetchers concrete singletons
  + router; the single `IRemotePageFetcher`→Http stays for the download-host resolvers). Frontend:
  `fetcher` on the `RemoteSourceConfig` type + a "Page loading" Select in `RemoteSourceEditor` Basics
  (`remote.fieldFetcher`/`fetcherHttp`/`fetcherWebview`).
- **UNVERIFIED in-app**: no configured source needs webview today (huihui + GameBanana are plain HTTP),
  so the WebView2 path builds + regression-tests clean (default stays http) but has NOT been run against
  a live JS site. Confirm against a real webview-transport source before relying on it; the settle delay
  (1.5s post-NavigationCompleted) is a const — add a per-site knob if a real site needs tuning.

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
- Hui盘 has **TWO backends** (grounded 2026-07-14): the main host `https://cloudreve.huihui123.org/s/<key>`
  is **Cloudreve v4** (resolver type `cloudreve`); some mods instead link an **IP/VPN mirror**
  `http://174.136.207.5/#s/<key>` that runs **kodbox** (resolver type `kodbox`) — a *different* app +
  API. Both labeled "Hui盘". The kodbox SPA **hash route `/#s/`** is the discriminator in the resolver
  rule. ⚠ An unmatched download anchor is **DROPPED** (`HttpRegexEngine`) — before the kodbox rule, mods
  on the IP mirror showed ONLY the (flaky) Quark button. MEGA (`mega.nz/folder/`) is a third option.
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

## kodbox share API (174.136.207.5 = the IP/VPN "Hui盘" mirror) — anonymous, VERIFIED LIVE 2026-07-14
huihui's IP/VPN Hui盘 mirror is **NOT Cloudreve** — it runs **kodbox 1.62** (可道云/KodExplorer, a PHP
file manager; `<meta name="generator" content="kodbox 1.62">`, `index.php?<route>` API). Totally
different share/download method from Cloudreve (all `/api/v4` + `/api/v3` return 404). Resolver =
`KodboxShareResolver` (type `kodbox`), two anonymous GETs (both work as GET with params in the query):
1. `GET /index.php?explorer/share/get&shareID={key}` → `{code:true, data:{title, sourceInfo:{name,
   path:"{shareItemLink:{key}}/", type:"file"|"folder", size}}}`. `code:false` → `data` is the reason
   string (`分享不存在`/`没有权限`) → throw `KODBOX_SHARE_UNAVAILABLE`. `{key}` rides the SPA hash route
   (`/#s/{key}`); `ParseShareUrl` reads `uri.Fragment` (also accepts a `/s/{key}` path form).
2. download = **`GET /index.php?explorer/share/fileDownload&shareID={key}&path={Uri.EscapeDataString(path)}`**
   — **streams the raw bytes directly** (NO presigned URL like Cloudreve; `Content-Length` == `size`,
   validated 68 455 268 B on `_1I87x0w`). A **folder** share streams as one server-zipped archive via
   `.../share/zipDownload` (same GET shape; size unknown → 0). The resolved URL feeds straight into the
   normal archive branch of `StartDownloadImport` (download → extract → recompress → import) — no
   MEGA-style tree walk (kodbox zips server-side). Probe: `devtools/hui-ip-probe.mjs`.

**Auto-detect fallback (a site serves MANY download methods + moves hosts).** A static resolver rule only
catches a KNOWN URL shape; huihui's Hui盘 moves to new IP/VPN mirrors. So `RemoteSourceConfig.AutoDetect`
(a list of resolver types; huihui.json = `["kodbox"]`) opts a source into a fallback: in
`HttpRegexEngine.GetDetailAsync`, a download link matching NO `Resolvers` rule is passed to
`IKodboxHostDetector` — which **pre-filters to share-shaped URLs only** (`/s/` or `/#s/`, never ad/social
links), then GETs the host root ONCE (cached per origin) and checks the kodbox fingerprint
(`Powered by kodbox` / `content="kodbox"`). On a hit it resolves as `kodbox`, **reusing the same-type
static rule's Name/password**. This catches a Hui盘 mirror on a new host whose share URL is a `/s/<key>`
path form (not the `/#s/` hash the static rule matches). Opt-in per source (empty `AutoDetect` = off →
other sources never probe). Tests: `KodboxHostDetectorTests`, `HttpRegexEngineAutoDetectTests`.

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
- **CLIENT User-Agent is REQUIRED for large files (code 23018, FIXED 2026-07-06).** `file/download` gates a
  **download size limit by product/UA**: a browser UA gets `HTTP 400 code 23018 "download file size limit"`
  on files the official client downloads fine; the Quark DESKTOP-CLIENT UA is NOT capped. So `UserAgent`
  (used on every API call + the CDN GET) is the quark-cloud-drive Electron UA
  (`…quark-cloud-drive/2.5.20 …Electron… Channel/pckk_other_ch`, matching the AList quark driver) — NOT a
  plain Chrome UA. `REMOTE_QUARK_SIZE_LIMIT` remains as the fallback message if a real cap is ever hit.
- **Two resolve calls, ONE save:** confirm dialog + background both resolve. `ResolveAsync` is
  metadata-only (token+detail, no save); `PrepareDownloadAsync` (background) does save+url; `CleanupAsync`
  deletes. `RemoteImportService` branches on `type=="quark"`: prepare → download → cleanup right after
  the bytes land (drive freed early) AND in the `finally` (covers cancel/fail) with `CancellationToken.None`.
- **Cookie capture = in-app login window, not typed.** `ExternalLoginService` opens a native WebView2
  Form (persistent per-provider profile under `{data}/settings/webview-login/{provider}`). It reads
  cookies from the **API origin** `https://drive-pc.quark.cn` (NOT `pan.quark.cn` — the session cookies
  `__puus`/`__pus`/`__kps`/`__uid` live on the parent domain `.quark.cn`; `pan.quark.cn` host-only has
  none of them — this was the capture bug). **Window is HIDDEN-until-needed, then SPLASH-on-reveal**
  (2026-07-06): opens off-screen (`Location=-32000,-32000`, `ShowInTaskbar=false`); a **pre-navigate
  cookie check** (`GetCookiesAsync` reads the profile store regardless of page state) decides — already
  logged-in → captures + disposes the hidden form WITHOUT ever showing (silent refresh, no flash); NOT
  logged-in → **reveal immediately with a native loading splash** (a white WinForms `Panel` — Marquee
  `ProgressBar` + label — added over the WebView2 via `BringToFront`) so the window pops up fast instead
  of only appearing once WebView2 + the page finish loading. The page loads BEHIND the splash (no
  homepage flash); the splash is dropped on `login-ready` (login box framed), on a nav error (→ retry
  overlay), or a ~7s poll fallback (never splash-forever). On reveal the service emits
  **`SystemEvents.LOGIN_WINDOW_SHOWN`** (a fire-and-forget global event → frontend `SystemEventType`) —
  `OnlineStorageAccountsCard` keeps the login button **busy from click until that event** (or
  `ONLINE_ACCOUNT_CHANGED` for the silent path), with a 30s backstop timeout, so the button no longer
  snaps back before the window shows. `ExternalLoginService` injects `IEventBus` for this. Saves the
  `Cookie` header to `IOnlineAccountStore` (`{data}/settings/online-accounts.json`, GLOBAL).
  **Token is PROTECTED AT REST (2026-07-10):** the file stores only a DPAPI blob (`CookieProtected`,
  CurrentUser scope via Core `SecretProtector`); plaintext never touches disk (legacy files upgrade on
  first load). **Invalidate-on-mismatch:** a blob that fails DPAPI decrypt (copied to another
  machine/user, tampered) → account flips to logged-out + file cleaned; a Quark API **401**
  (`EnsureOk`) → stored account removed + `QUARK_NOT_LOGGED_IN`. Tests: `RemoteAccountTests`. Managed in
  **Settings → 在线存储 / Online Storage** (`OnlineStorageAccountsCard`; IPC
  `ACCOUNT_LIST`/`ACCOUNT_LOGIN`/`ACCOUNT_REMOVE`). **Logout** removes the stored cookie AND
  `ClearProfile` deletes the WebView2 profile folder (else it silently auto-logs-in). Not logged in →
  resolver throws `QUARK_NOT_LOGGED_IN`.
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

## MEGA (mega.nz) FOLDER shares — anonymous, client-side crypto (VALIDATED live 2026-07-13)
huihui recommends MEGA over Quark ("夸克经常失效，推荐使用MEGA"). A folder link
`mega.nz/folder/<shareId>#<keyB64>` is a directory TREE of the mod's files (NOT one archive), so the
import downloads every file (decrypt) into a staging dir → recompress → import (skip extract). Resolver
`type: "mega"` (huihui.json), no login. Validated end-to-end against a real folder by
`devtools/mega-probe.mjs` (decrypted real filenames + a read-me's UTF-8 body + a live C# resolver test).
- **API:** `POST https://g.api.mega.co.nz/cs?id=<seq>&n=<shareId>` body `[{"a":"f","c":1,"r":1,"ca":1}]`
  → `[{f:[nodes]}]` (bare array; a bare/first NUMBER = error, e.g. -9 removed). Download URLs: batch
  `[{"a":"g","g":1,"n":<fileHandle>}]` → `[{g:url,s:size}]`. Fetch via the shared `IRemotePageFetcher`
  (`PostJsonAsync`), same as Cloudreve.
- **Crypto (`MegaCrypto`, unit-tested):** base64url (no pad); node key = AES-ECB(parentKey, encKey);
  FILE key = 32B → 8 big-endian u32 words → aesKey `[w0^w4,w1^w5,w2^w6,w3^w7]`, nonce `[w4,w5]`; attrs =
  AES-CBC(aesKey, zeroIV, no-pad) → `"MEGA"`+JSON `{n:name}`; file bytes = AES-CTR(aesKey, IV=nonce‖0).
- **THE GOTCHA — key HIERARCHY (cost 4 debug rounds):** a node's `k` is `h1:key1/h2:key2/…` (its key
  encrypted under EACH sharing ancestor). The link key is the SHARE ROOT folder's key (the folder whose
  parent isn't in the tree — NOT `shareId`), and nested nodes are keyed under a SUBFOLDER, not the root.
  So: seed `keys[rootHandle]=linkKey`, then decrypt folder keys top-down to a fixed point (a subfolder's
  key needs its parent first), then decrypt each node with whichever ancestor key its `k` lists. Naively
  splitting the first `handle:key` + decrypting with the link key corrupts every nested file.
  `MegaShareResolver.ListFolderAsync` does this; `RemoteImportService` has a `type=="mega"` branch
  (`DownloadMegaTreeAsync`: CTR-decrypt each file into staging, path-contained) → recompress → import
  (content sha = the normalized .7z). Errors: `MEGA_LINK_UNSUPPORTED` / `MEGA_EMPTY_SHARE` /
  `MEGA_SHARE_UNAVAILABLE`. File shares (`mega.nz/file/…`) NOT handled yet.

## Architecture (mirrors the app's module conventions)

```
Modules/Remote/
  Models/            RemoteSourceConfig (the JSON adapter), RemoteModCard, RemoteModDetail,
                     RemoteDownloadOption, RemoteResolveResult, RemoteIndexEntry/Info/Cache/Page
  Services/
    RemoteSourceStore     — loads {data}/remote-sources/*.json; SEEDER copies shipped adapters
                            ({data}/remote-source-seeds/, csproj Content from
                            D3dxSkinManager/RemoteSources/*.json) whose id isn't configured yet —
                            user edits never overwritten; drop a JSON to add a site. GetAll/GetById
                            are mtime-signature CACHED (2026-07-10 — they sit on every browse/index
                            query; unchanged files → cached list, edited/dropped/removed file →
                            reload, so the no-restart contract holds)
    IRemotePageFetcher    — GetStringAsync/PostJsonAsync seam; HttpPageFetcher (via IDownloadService)
                            is v1; a WebView2PageFetcher can back JS-rendered sites (config `engine`)
    RemoteBrowseService   — list/search/detail: fetch page → run the config's regex extraction →
                            DTOs (absolute URLs)
    CloudreveShareResolver— the 3-step API dance above (config resolver type "cloudreve")
    RemoteIndexService    — enrichment = Phase 1 backfill (unenriched entries) + Phase 2 PROACTIVE
                            re-sync of STALE cached detail (DetailFetchedUtc null/older than
                            DetailStaleAfter=30d, bounded StaleReSyncCap=50/sync, stalest-first) so
                            tags/description/downloads don't rot; non-JSON detail (removed mod) →
                            REMOTE_DETAIL_NOT_JSON → skipped, not a poison-loop.
                          — SYNCED LOCAL INDEX per source+list ({data}/remote-sources/.cache/):
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
