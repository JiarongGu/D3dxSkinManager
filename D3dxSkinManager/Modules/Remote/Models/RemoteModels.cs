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

    /// <summary>Regex over the detail page HTML with named group: image (content/preview images).</summary>
    public string DetailImagePattern { get; set; } = string.Empty;

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
}

/// <summary>One mod card on a list/search page. URLs are absolute.</summary>
public class RemoteModCard
{
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
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
}

public class RemoteModDetail
{
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<RemoteDownloadOption> Downloads { get; set; } = new();
}

/// <summary>A resolved direct download (Cloudreve share → presigned URL).</summary>
public class RemoteResolveResult
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

// ---- Synced index (local cache of a source list) -------------------------------------------------

/// <summary>One mod in the synced index — keyed by the site's stable entry id.</summary>
public class RemoteIndexEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Date hint (yyyy-MM-dd) derived from the image path, when the adapter can extract one.</summary>
    public string? DateHint { get; set; }

    /// <summary>Site order at the last sync (page*10000 + position) — ascending = the site's own recency order.</summary>
    public long SortKey { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    /// <summary>Set at query time: a mod in the current profile was imported from this entry. Not persisted meaningfully.</summary>
    public bool Imported { get; set; }
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

/// <summary>Which source + game list a PROFILE targets (a profile is one game).</summary>
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
