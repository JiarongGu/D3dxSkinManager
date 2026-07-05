# Remote mod library — site adapters + Cloudreve download (GROUNDED 2026-07-05)

Feature: browse remote mod sites in-app → download → one-click import into the current profile.
**Config-driven, game-agnostic**: every site is a JSON adapter (`RemoteSourceConfig`) so new
libraries can be added without code (regex-based extraction v1; the fetch layer is a seam so a
WebView2-rendered engine can be added for JS-heavy sites later).

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
- 夸克 (Quark): `https://pan.quark.cn/s/...` ← NOT resolvable anonymously (needs account) — surface
  as "open in browser" only.

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

## Architecture (mirrors the app's module conventions)

```
Modules/Remote/
  Models/            RemoteSourceConfig (the JSON adapter), RemoteModCard, RemoteModDetail,
                     RemoteDownloadOption, RemoteResolveResult
  Services/
    RemoteSourceStore     — loads {data}/remote-sources/*.json (IGlobalPathService), seeds huihui
                            config on first run; user can drop new JSONs (or future UI)
    IRemotePageFetcher    — GetStringAsync(url) seam; HttpPageFetcher (via IDownloadService) is v1;
                            a WebView2PageFetcher can be added for JS-rendered sites (config `engine`)
    RemoteBrowseService   — list/search/detail: fetch page → run the config's regex extraction →
                            DTOs (absolute URLs)
    CloudreveShareResolver— the 3-step API dance above (config resolver type "cloudreve")
    RemoteImportService   — fire-and-forget download+import: ProcessRegistry entry → resolve →
                            IDownloadService.DownloadAsync into {profile}/temp → ModImportService
                            .ImportAsync → rename to detail title + import preview images → events
  RemoteFacade         — GET_SOURCES / BROWSE / SEARCH / GET_DETAIL / RESOLVE_DOWNLOAD /
                          DOWNLOAD_IMPORT (immediate ack; progress via Activity panel)
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
