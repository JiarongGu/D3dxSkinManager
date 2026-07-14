using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Site engine for a WooCommerce shop that lists mods as FREE products (adapter <c>engine = "woocommerce"</c>).
/// Reads the public WooCommerce Store API (<c>/wp-json/wc/store/v1/products</c>) — clean paginated JSON —
/// instead of scraping Elementor HTML. Download links (百度盘/夸克/MEGA …) live in each product's
/// <c>short_description</c> as labelled anchors and are matched to the config's resolver rules exactly
/// like the http engine. Reusable for any WooCommerce mod shop. First target: kekehxl.top (可可站), which
/// also sits behind a WordPress password gate — the whole REST API 401s until unlocked — so this engine
/// asks <see cref="IRemoteSiteGate"/> to log in before fetching and re-auths once on a 401.
/// Verified live 2026-07-14 (see remote-library.md). Parsing lives in public statics (unit-tested, no HTTP).
/// </summary>
public class WooCommerceEngine : RemoteSiteEngineBase
{
    public const string EngineName = "woocommerce";
    private const int PerPage = 30;

    private readonly IRemoteSiteGate _gate;

    public WooCommerceEngine(IRemotePageFetcherRouter fetchers, ILogHelper logger, IRemoteSiteGate gate)
        : base(fetchers, logger)
    {
        _gate = gate;
    }

    public override string EngineId => EngineName;

    public override bool SupportsSearch(RemoteSourceConfig config) => true;

    public override async Task<RemoteBrowseResult> BrowseAsync(RemoteSourceConfig config, string listId, int page, CancellationToken ct)
    {
        var json = await GatedFetchAsync(config, BuildListUrl(config.BaseUrl, listId, page, PerPage), ct).ConfigureAwait(false);
        return ParseProducts(json, page, PerPage);
    }

    public override async Task<RemoteBrowseResult> SearchAsync(RemoteSourceConfig config, string query, string? listId, CancellationToken ct)
    {
        var json = await GatedFetchAsync(config, BuildSearchUrl(config.BaseUrl, query, listId, PerPage), ct).ConfigureAwait(false);
        return ParseProducts(json, 1, PerPage);
    }

    public override async Task<RemoteModDetail> GetDetailAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct)
    {
        // Detail is fetched by numeric product id (the Store API's slug lookup is unreliable — this site
        // stores already-percent-encoded slugs). The id rides the card's DetailUrl as ?wc_id=.
        var id = ExtractProductId(detailUrl)
            ?? throw new OperationException("REMOTE_FETCH_FAILED", "url", detailUrl);
        var json = await GatedFetchAsync(config, BuildProductUrl(config.BaseUrl, id), ct).ConfigureAwait(false);
        return ParseProductDetail(json, detailUrl, config.Resolvers);
    }

    /// <summary>Ensure the site gate is unlocked, then fetch. A first failure (often a 401 from an expired
    /// gate cookie) invalidates the session and retries ONCE after re-logging-in.</summary>
    private async Task<string> GatedFetchAsync(RemoteSourceConfig config, string url, CancellationToken ct)
    {
        await _gate.EnsureAuthenticatedAsync(config, ct).ConfigureAwait(false);
        try
        {
            return await FetchAsync(config, url, ct).ConfigureAwait(false);
        }
        catch (OperationException) when (config.Gate != null)
        {
            _gate.Invalidate(config.Id);
            await _gate.EnsureAuthenticatedAsync(config, ct).ConfigureAwait(false);
            return await FetchAsync(config, url, ct).ConfigureAwait(false);
        }
    }

    // ---- URL builders + parsers (static — unit-tested without HTTP) -----------------------------

    private static string StoreApi(string baseUrl) => $"{baseUrl.TrimEnd('/')}/wp-json/wc/store/v1/products";

    /// <summary>List one category (the "list" id), newest first. WooCommerce Store API paginates via
    /// <c>page</c> + <c>per_page</c>.</summary>
    public static string BuildListUrl(string baseUrl, string categoryId, int page, int perPage) =>
        $"{StoreApi(baseUrl)}?per_page={perPage}&page={Math.Max(1, page)}&orderby=date&order=desc"
        + (string.IsNullOrWhiteSpace(categoryId) ? string.Empty : $"&category={Uri.EscapeDataString(categoryId)}");

    public static string BuildSearchUrl(string baseUrl, string query, string? categoryId, int perPage) =>
        $"{StoreApi(baseUrl)}?per_page={perPage}&search={Uri.EscapeDataString(query.Trim())}"
        + (string.IsNullOrWhiteSpace(categoryId) ? string.Empty : $"&category={Uri.EscapeDataString(categoryId)}");

    /// <summary>Single product by numeric id — the reliable Store API detail lookup.</summary>
    public static string BuildProductUrl(string baseUrl, string productId) =>
        $"{StoreApi(baseUrl)}/{Uri.EscapeDataString(productId)}";

    /// <summary>The numeric product id from a card DetailUrl (carried as <c>?wc_id=</c> — the permalink
    /// itself has no id and the slug lookup is unreliable on this site).</summary>
    public static string? ExtractProductId(string detailUrl)
    {
        var m = Regex.Match(detailUrl, @"[?&]wc_id=(?<id>\d+)", RegexOptions.CultureInvariant);
        return m.Success ? m.Groups["id"].Value : null;
    }

    /// <summary>Append the numeric id to a permalink so detail can be fetched by id and the index keyed
    /// on the stable id (WP ignores the extra query param when the URL is opened in a browser).</summary>
    public static string WithProductId(string permalink, string id) =>
        string.IsNullOrWhiteSpace(id) ? permalink : $"{permalink}{(permalink.Contains('?') ? '&' : '?')}wc_id={id}";

    public static RemoteBrowseResult ParseProducts(string json, int page, int perPage)
    {
        var result = new RemoteBrowseResult { Page = Math.Max(1, page) };
        using var doc = ParseJsonOrThrow(json, "products");
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

        var count = 0;
        foreach (var p in doc.RootElement.EnumerateArray())
        {
            count++;
            var name = HtmlToText(GetString(p, "name"));
            var link = GetString(p, "permalink");
            var id = GetNumberAsString(p, "id");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(id)) continue;
            var card = new RemoteModCard
            {
                Title = name,
                DetailUrl = WithProductId(link!, id!),
                ImageUrl = FirstImage(p) ?? string.Empty,
            };
            foreach (var tag in ProductTagNames(p)) card.Tags.Add(tag);
            result.Cards.Add(card);
        }
        // No page-count header available through the fetch seam — a short page IS the last page; a full
        // page leaves the total unknown (the index crawl stops when a page returns nothing new).
        if (count < perPage) result.TotalPages = Math.Max(1, page);
        return result;
    }

    public static RemoteModDetail ParseProductDetail(string json, string detailUrl, List<RemoteResolverRule> resolvers)
    {
        using var doc = ParseJsonOrThrow(json, detailUrl);
        // ?slug= returns an array with 0/1 product.
        var p = doc.RootElement.ValueKind == JsonValueKind.Array
            ? (doc.RootElement.GetArrayLength() > 0 ? doc.RootElement[0] : default)
            : doc.RootElement;
        if (p.ValueKind != JsonValueKind.Object)
            throw new OperationException("REMOTE_DETAIL_NOT_JSON", "url", detailUrl);

        var detail = new RemoteModDetail { DetailUrl = detailUrl, Title = HtmlToText(GetString(p, "name")) ?? string.Empty };
        foreach (var tag in ProductTagNames(p)) detail.Tags.Add(tag);

        // Gallery: every product image at full resolution.
        if (p.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
            foreach (var img in imgs.EnumerateArray())
                if (GetString(img, "src") is { } src && !string.IsNullOrWhiteSpace(src) && !detail.Images.Contains(src))
                    detail.Images.Add(src);

        // Download options + description come from the short/long description HTML.
        var descHtml = (GetString(p, "short_description") ?? "") + "\n" + (GetString(p, "description") ?? "");
        foreach (var opt in ExtractDownloads(descHtml, resolvers))
            detail.Downloads.Add(opt);
        var text = HtmlToText(descHtml);
        if (!string.IsNullOrWhiteSpace(text)) detail.Description = text;
        return detail;
    }

    /// <summary>Anchors in the description whose href matches a resolver rule become download options
    /// (first rule wins; unmatched anchors — VPN ads, tutorials — are dropped). Mirrors the http engine.</summary>
    public static IEnumerable<RemoteDownloadOption> ExtractDownloads(string html, List<RemoteResolverRule> resolvers)
    {
        var seen = new HashSet<string>();
        foreach (Match m in Regex.Matches(html, "href=[\"'](?<url>https?://[^\"']+)[\"']", RegexOptions.CultureInvariant))
        {
            var url = WebUtility.HtmlDecode(m.Groups["url"].Value);
            var rule = resolvers.FirstOrDefault(r => SafeIsMatch(r.Match, url));
            if (rule == null || !seen.Add(url)) continue;
            yield return new RemoteDownloadOption
            {
                Name = rule.Name,
                Url = url,
                Type = rule.Type,
                UnzipPassword = rule.UnzipPassword,
                UnwrapNested = rule.UnwrapNested,
            };
        }
    }

    private static IEnumerable<string> ProductTagNames(JsonElement product)
    {
        if (product.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            foreach (var t in tags.EnumerateArray())
                if (GetString(t, "name") is { } n && !string.IsNullOrWhiteSpace(n))
                    yield return HtmlToText(n)!;
    }

    private static string? FirstImage(JsonElement product)
    {
        if (product.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
            foreach (var img in imgs.EnumerateArray())
            {
                var src = GetString(img, "thumbnail") ?? GetString(img, "src");
                if (!string.IsNullOrWhiteSpace(src)) return src;
            }
        return null;
    }

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static bool SafeIsMatch(string pattern, string input)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        try { return Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant, RegexTimeout); }
        catch (RegexMatchTimeoutException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static JsonDocument ParseJsonOrThrow(string json, string url)
    {
        var trimmed = (json ?? string.Empty).AsSpan().TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
            throw new OperationException("REMOTE_DETAIL_NOT_JSON", "url", url);
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { throw new OperationException("REMOTE_DETAIL_NOT_JSON", "url", url); }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>A numeric property (e.g. the product id) as a string.</summary>
    private static string? GetNumberAsString(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetRawText() : null;

    /// <summary>Rendered names/descriptions are HTML-ish — strip tags, decode entities, collapse whitespace.</summary>
    private static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
