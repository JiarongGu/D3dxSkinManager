namespace D3dxSkinManager.Modules.Remote.Models;

/// <summary>
/// A remote mod-library site adapter — one JSON file in {data}/remote-sources/. Everything a site
/// needs is data (URL templates + regex extraction patterns + download resolver rules), so new
/// libraries can be added without code. Grounded on huihui168.org — see
/// .claude/rules/remote-library.md for the verified patterns.
/// </summary>
public class RemoteSourceConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Site origin, e.g. https://huihui168.org — NEVER hard-coded (sites move hosts).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Fetch engine: "http" (plain requests; default) — a "webview" engine (WebView2-rendered,
    /// for JS-heavy/anti-bot sites) can plug into the IRemotePageFetcher seam later.</summary>
    public string Engine { get; set; } = "http";

    /// <summary>The site's browsable lists (usually one per game).</summary>
    public List<RemoteListConfig> Lists { get; set; } = new();

    /// <summary>URL template for page 1 of a list. Placeholders: {list}.</summary>
    public string ListUrlFirstPage { get; set; } = string.Empty;

    /// <summary>URL template for page N (N ≥ 2) of a list. Placeholders: {list}, {page}.</summary>
    public string ListUrlTemplate { get; set; } = string.Empty;

    /// <summary>Search URL template. Placeholders: {query} (URL-encoded). Null = site has no search.</summary>
    public string? SearchUrlTemplate { get; set; }

    /// <summary>Regex over the list/search page HTML with named groups: url, image, title.</summary>
    public string CardPattern { get; set; } = string.Empty;

    /// <summary>Optional regex isolating the MAIN list region (named group: scope) before the card
    /// pattern runs — excludes hot/recent sidebars that repeat identical cards on every page and
    /// would pollute pagination + the synced index. No match → falls back to the whole page
    /// (search pages often have a different layout).</summary>
    public string? CardScopePattern { get; set; }

    /// <summary>Optional regex finding the last page number (named group: pages; {list} substituted).
    /// The MAX numeric match wins. Null = unknown total (UI paginates blindly).</summary>
    public string? TotalPagesPattern { get; set; }

    /// <summary>Regex over the detail page HTML with named group: title.</summary>
    public string DetailTitlePattern { get; set; } = string.Empty;

    /// <summary>Optional regex isolating the detail page's MAIN content region (named group: scope)
    /// before image/download/description extraction runs — excludes sidebar avatars, third-party ads
    /// and related-mod thumbnails (they'd pollute the gallery). No match → whole page.</summary>
    public string? DetailScopePattern { get; set; }

    /// <summary>Regex over the detail page HTML with named group: image (content/preview images).</summary>
    public string DetailImagePattern { get; set; } = string.Empty;

    /// <summary>Optional regex with named group: description — the detail page's rich-text body.
    /// Tags are stripped (br/p become line breaks). Runs inside the detail scope when one is set.</summary>
    public string? DetailDescriptionPattern { get; set; }

    /// <summary>Regex over the detail page HTML with named group: url (candidate download anchors).
    /// Only candidates matching a resolver rule become download options.</summary>
    public string DownloadLinkPattern { get; set; } = string.Empty;

    /// <summary>Maps download URLs to resolver behaviour, first match wins.</summary>
    public List<RemoteResolverRule> Resolvers { get; set; } = new();

    /// <summary>Optional regex extracting a STABLE per-mod id from the detail URL (named group: id).
    /// Null = the absolute detail URL is the id. The id keys the synced index + import identity.</summary>
    public string? EntryIdPattern { get; set; }

    /// <summary>Optional regex extracting a date hint (yyyyMMdd, named group: date) from the card
    /// image URL — many sites embed the upload date in the image path.</summary>
    public string? ImageDatePattern { get; set; }

    /// <summary>Optional regex deriving a TAG from an entry's TITLE (named group: tag) for sites
    /// with no tag taxonomy — huihui titles usually lead with the character name before the first
    /// space, so its seed uses <c>^(?&lt;tag&gt;\S+)\s</c>. Applied centrally by the dispatcher
    /// AFTER the engine normalizes, and ONLY to entries that have no tags of their own; reusable
    /// by any tagless site.</summary>
    public string? TitleTagPattern { get; set; }

    /// <summary>Optional DISPLAY labels for site tag names, PER LANGUAGE (configurable i18n —
    /// supports multiple dialects): outer key = app language code ("cn", "en", …), inner = raw tag →
    /// label (e.g. cn: "Character Skins" → "角色皮肤"). Raw names stay the stored/filter/rule
    /// identity; only the UI maps through the current language's table, falling back to the raw name.</summary>
    public Dictionary<string, Dictionary<string, string>> TagLabels { get; set; } = new();

    /// <summary>Optional per-site card-thumbnail display config (crop position, and room to grow).</summary>
    public RemoteThumbnailConfig? Thumbnail { get; set; }
}

/// <summary>How a site's mod-card thumbnail is displayed. Cards `object-fit:cover` a fixed box, so tall
/// art gets cropped — this tunes it per site. A nested object (not a bare string) so future knobs
/// (fit, aspect ratio, background) slot in without breaking configs.</summary>
public class RemoteThumbnailConfig
{
    /// <summary>CSS <c>object-position</c> for the crop, e.g. "50% 20%" keeps more of the top (heads)
    /// and trims the bottom. Null/empty = centered.</summary>
    public string? Position { get; set; }
}

public class RemoteListConfig
{
    /// <summary>The value substituted into the URL templates (e.g. "2").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "绝区零").</summary>
    public string Name { get; set; } = string.Empty;
}

public class RemoteResolverRule
{
    /// <summary>Regex matched against a candidate download URL.</summary>
    public string Match { get; set; } = string.Empty;

    /// <summary>"cloudreve" = resolvable to a direct download (Cloudreve v4 share API);
    /// "external" = shown as an open-in-browser option only.</summary>
    public string Type { get; set; } = "external";

    /// <summary>Display label for the option (e.g. "Hui盘", "夸克").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Site-known unzip password for archives from this host (e.g. huihui's "huihui").
    /// Used ONLY when a plain extraction fails with a password error (most archives need none);
    /// a user-entered password at download time overrides it.</summary>
    public string? UnzipPassword { get; set; }

    /// <summary>Opt into the RECURSIVE-UNWRAP download workflow for this host: the downloaded file is
    /// treated as a possibly-DISGUISED, possibly-MULTI-LAYER archive — extract by magic bytes (not
    /// extension), carve an archive appended to a decoy (huihui's "safe keep": a real .mp4 with a zip
    /// appended), and keep unwrapping nested archives (trying the password per layer) until the real
    /// mod content. Reusable — any site whose downloads are wrapped this way can set it. Default off
    /// (a plain single extract).</summary>
    public bool UnwrapNested { get; set; }
}

// ---- DTOs (serialized camelCase to the frontend) ------------------------------------------------

/// <summary>A source as the frontend sees it (config minus the parsing internals).</summary>
public class RemoteSourceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<RemoteListConfig> Lists { get; set; } = new();
    public bool HasSearch { get; set; }

    /// <summary>Per-language display labels for site tag names (see RemoteSourceConfig.TagLabels).</summary>
    public Dictionary<string, Dictionary<string, string>> TagLabels { get; set; } = new();

    /// <summary>Per-site card-thumbnail display config (see RemoteSourceConfig.Thumbnail).</summary>
    public RemoteThumbnailConfig? Thumbnail { get; set; }
}

/// <summary>One mod card on a list/search page. URLs are absolute.</summary>
public class RemoteModCard
{
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>The site's tags for this mod (STANDARDIZED across engines — e.g. GameBanana super
    /// category "Skins"; the sub category joins from the detail page). Empty when the site/engine has
    /// no per-card taxonomy (e.g. huihui, where the list itself is the game).</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Date hint (yyyy-MM-dd) when the engine can extract one directly (e.g. GameBanana
    /// _tsDateAdded). Null lets the index fall back to the adapter's imageDatePattern.</summary>
    public string? DateHint { get; set; }

    /// <summary>The SITE's content rating, when it has one (GameBanana _sInitialVisibility).
    /// true = site-rated sensitive, false = site-rated safe, null = the site doesn't say — the
    /// content-veil image analysis decides. Site metadata is AUTHORITATIVE over the local
    /// analysis (content-veil.md).</summary>
    public bool? Sensitive { get; set; }
}

public class RemoteBrowseResult
{
    public List<RemoteModCard> Cards { get; set; } = new();
    public int Page { get; set; }
    public int? TotalPages { get; set; }
}

/// <summary>A download option on a detail page (a resolver-rule match).</summary>
public class RemoteDownloadOption
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>Resolver type: "cloudreve" (in-app download+import) or "external" (open in browser).</summary>
    public string Type { get; set; } = "external";

    /// <summary>The matched resolver's site-known unzip password. Import normalization tries a
    /// PLAIN extraction first and reaches for this only on a password failure.</summary>
    public string? UnzipPassword { get; set; }

    /// <summary>The matched resolver's recursive-unwrap opt-in (carve disguised + unwrap nested layers).</summary>
    public bool UnwrapNested { get; set; }
}

public class RemoteModDetail
{
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<RemoteDownloadOption> Downloads { get; set; } = new();

    /// <summary>Site tags visible on the detail page (e.g. GameBanana sub category). Merged with the
    /// index entry's tags for display + import tag-rules.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Plain-text description from the page, when the engine can extract one (GameBanana _sText).</summary>
    public string? Description { get; set; }
}

/// <summary>A resolved direct download (Cloudreve share → presigned URL).</summary>
public class RemoteResolveResult
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Extra request headers the DOWNLOAD of <see cref="DownloadUrl"/> must carry (e.g. a
    /// Quark session Cookie + UA — its CDN rejects the presigned URL without them). Null for hosts
    /// whose resolved URL is unauthenticated (Cloudreve presigned, GameBanana direct).</summary>
    public Dictionary<string, string>? DownloadHeaders { get; set; }
}

// ---- Online storage accounts (credentials for auth'd download hosts, e.g. Quark) -----------------

/// <summary>A saved login for an online-storage host whose downloads need authentication. GLOBAL
/// (not per-profile) — a host recurs across sites/profiles. The cookie is captured by an in-app
/// login window (WebView2), never typed. Stored in {data}/online-accounts.json.</summary>
public class OnlineStorageAccount
{
    /// <summary>Host key, e.g. "quark". Matches a resolver's <c>type</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Friendly account label captured at login (e.g. the Quark nickname), for the UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The captured Cookie header value used to authenticate host API/CDN requests.
    /// IN-MEMORY plaintext only — persisted as <see cref="CookieProtected"/> (the store nulls this
    /// field when writing; it stays deserializable for legacy plaintext files, upgraded on load).</summary>
    public string Cookie { get; set; } = string.Empty;

    /// <summary>DPAPI-protected cookie (base64; SecretProtector, CurrentUser scope) — what the file
    /// actually stores. Decrypt failure = made by another user/machine or tampered → invalidated.</summary>
    public string? CookieProtected { get; set; }

    public DateTime SavedAtUtc { get; set; }
}

/// <summary>UI view of a saved account — never ships the raw cookie to the frontend.</summary>
public class OnlineStorageAccountInfo
{
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool LoggedIn { get; set; }
    public DateTime? SavedAtUtc { get; set; }
}

// ---- Synced index (local cache of a source list) -------------------------------------------------

/// <summary>One mod in the synced index — keyed by the site's stable entry id.</summary>
public class RemoteIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>The site's tags for this mod (standardized across engines; filter + chips).</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Raw DB value (JSON array) backing <see cref="Tags"/> — repository plumbing only.</summary>
    [global::System.Text.Json.Serialization.JsonIgnore]
    public string? TagsJson { get; set; }

    /// <summary>Date hint (yyyy-MM-dd) derived from the image path, when the adapter can extract one.</summary>
    public string? DateHint { get; set; }

    /// <summary>The site's content rating (see <see cref="RemoteModCard.Sensitive"/>): true = veil,
    /// false = don't veil, null = no site rating — the image analysis decides frontend-side.</summary>
    public bool? Sensitive { get; set; }

    /// <summary>Site order at the last sync (page*10000 + position) — ascending = the site's own recency order.</summary>
    public long SortKey { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    /// <summary>Set at query time: a mod in the current profile was imported from this entry. Not persisted meaningfully.</summary>
    public bool Imported { get; set; }

    /// <summary>Set at query time (when Imported): the local mod id(s) imported from this entry, so the UI
    /// can jump to them ("locate"). A list because an entry can be downloaded multiple times. Not persisted.</summary>
    public List<string>? LocalModIds { get; set; }
}

/// <summary>A distinct site tag present in the index + how many mods carry it (filter dropdown).</summary>
public class RemoteTagCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Metadata about a synced index (per source+list cache file).</summary>
public class RemoteIndexInfo
{
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public DateTime? SyncedAtUtc { get; set; }
    public int TotalPages { get; set; }
    public int EntryCount { get; set; }
}

/// <summary>A filtered/paged slice of the index for the UI.</summary>
public class RemoteIndexPage
{
    public RemoteIndexInfo Info { get; set; } = new();
    public List<RemoteIndexEntry> Entries { get; set; } = new();
    /// <summary>Total entries matching the filter (before paging).</summary>
    public int Total { get; set; }
}

// ---- Redesigned library model (remote-library-redesign.md) --------------------------------------

/// <summary>One ordered import rule. A mod matches when it carries ALL of <see cref="Tags"/> (if any
/// are set) AND its title matches <see cref="TitlePattern"/> (if set) — at least one criterion must be
/// set. Rules are evaluated in order; first match wins, else uncategorized. Title regex is the lever
/// for sites with no tag taxonomy (huihui has none — verified 2026-07-06).</summary>
public class RemoteTagRule
{
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    /// <summary>Optional case-insensitive regex matched against the mod title.</summary>
    public string? TitlePattern { get; set; }
    public string CategoryId { get; set; } = string.Empty;
}

/// <summary>A configured remote library a profile can browse (site + game + import rules). A profile
/// owns MANY of these (switchable), managed in library management — replaces the single binding.</summary>
public class RemoteLibrary
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    /// <summary>Display name, e.g. "GameBanana · Genshin".</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Ordered tag→category rules; first match wins, no match = uncategorized.</summary>
    public List<RemoteTagRule> TagRules { get; set; } = new();
    public DateTime AddedAtUtc { get; set; }
}

/// <summary>The profile's configured libraries + which one the main screen is showing.</summary>
public class RemoteLibrariesState
{
    public List<RemoteLibrary> Libraries { get; set; } = new();
    public string? ActiveLibraryId { get; set; }
}

/// <summary>Which source + game list a PROFILE targets (a profile is one game).
/// LEGACY — being replaced by <see cref="RemoteLibrary"/> (remote-library-redesign.md).</summary>
public class RemoteBinding
{
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public DateTime BoundAtUtc { get; set; }

    /// <summary>Local category id that mods downloaded from this library are imported into. Null =
    /// uncategorized (the old "unknown" behaviour). Set per-profile since categories are per-profile.</summary>
    public string? DefaultCategoryId { get; set; }
}

/// <summary>What a candidate adapter config extracted from the live site — the authoring feedback loop.</summary>
public class RemoteSourceTestResult
{
    public int CardCount { get; set; }
    public List<string> SampleTitles { get; set; } = new();
    public int? TotalPages { get; set; }
    /// <summary>Detail parse of the first card (null when no cards matched).</summary>
    public string? DetailTitle { get; set; }
    public List<RemoteDownloadOption> DetailDownloads { get; set; } = new();
    public int DetailImageCount { get; set; }
}
