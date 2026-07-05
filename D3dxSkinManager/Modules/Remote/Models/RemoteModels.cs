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
