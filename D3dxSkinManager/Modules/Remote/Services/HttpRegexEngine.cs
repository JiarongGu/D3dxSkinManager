using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The generic regex-over-HTML engine (EngineId "http") for server-rendered sites like huihui168.org.
/// Its "small config" is the RemoteSourceConfig regex/url-template fields — the ONLY engine that uses
/// them. Patterns run with a match timeout so a bad user-supplied config can't hang the app.
/// (Extracted verbatim from the old RemoteBrowseService per remote-library-redesign.md.)
/// </summary>
public class HttpRegexEngine : RemoteSiteEngineBase
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    private readonly IKodboxHostDetector _kodboxDetector;

    public HttpRegexEngine(IRemotePageFetcherRouter fetchers, IKodboxHostDetector kodboxDetector, ILogHelper logger)
        : base(fetchers, logger)
    {
        _kodboxDetector = kodboxDetector;
    }

    public override string EngineId => "http";

    public override bool SupportsSearch(RemoteSourceConfig config) =>
        !string.IsNullOrWhiteSpace(config.SearchUrlTemplate);

    public override async Task<RemoteBrowseResult> BrowseAsync(RemoteSourceConfig config, string listId, int page, CancellationToken ct)
    {
        var template = page <= 1 ? config.ListUrlFirstPage : config.ListUrlTemplate;
        var path = template.Replace("{list}", listId).Replace("{page}", page.ToString());
        var html = await FetchAsync(config, Absolute(config.BaseUrl, path), ct).ConfigureAwait(false);

        var result = new RemoteBrowseResult { Page = Math.Max(1, page), Cards = ExtractCards(config, html) };
        result.TotalPages = ExtractTotalPages(config, listId, html);
        return result;
    }

    public override async Task<RemoteBrowseResult> SearchAsync(RemoteSourceConfig config, string query, string? listId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.SearchUrlTemplate))
            throw new OperationException("REMOTE_SEARCH_UNSUPPORTED", "source", config.Name);

        var path = config.SearchUrlTemplate.Replace("{query}", Uri.EscapeDataString(query.Trim()));
        var html = await FetchAsync(config, Absolute(config.BaseUrl, path), ct).ConfigureAwait(false);
        return new RemoteBrowseResult { Page = 1, Cards = ExtractCards(config, html) };
    }

    public override async Task<RemoteModDetail> GetDetailAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct)
    {
        var html = await FetchAsync(config, detailUrl, ct).ConfigureAwait(false);
        var detail = new RemoteModDetail { DetailUrl = detailUrl };

        var titleMatch = Match(config.DetailTitlePattern, html);
        detail.Title = titleMatch.Success ? StripTags(titleMatch.Groups["title"].Value) : string.Empty;

        // Scope to the main content region when the adapter defines one — sidebars carry avatars,
        // third-party ad images and related-mod thumbnails that would pollute the gallery.
        var scoped = html;
        if (!string.IsNullOrWhiteSpace(config.DetailScopePattern))
        {
            var scope = Match(config.DetailScopePattern, html);
            if (scope.Success) scoped = scope.Groups["scope"].Value;
        }

        if (!string.IsNullOrWhiteSpace(config.DetailImagePattern))
        {
            foreach (Match m in Matches(config.DetailImagePattern, scoped))
            {
                var image = Absolute(config.BaseUrl, m.Groups["image"].Value);
                if (!detail.Images.Contains(image)) detail.Images.Add(image);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.DetailDescriptionPattern))
        {
            var desc = Match(config.DetailDescriptionPattern, scoped);
            if (desc.Success) detail.Description = HtmlToPlainText(desc.Groups["description"].Value);
        }

        foreach (Match m in Matches(config.DownloadLinkPattern, scoped))
        {
            var candidate = m.Groups["url"].Value;
            var rule = config.Resolvers.FirstOrDefault(r => SafeIsMatch(r.Match, candidate));
            // No static rule matched → try the opt-in auto-detect fallback (a site may move its download
            // host to a new mirror whose URL shape no rule catches). Probes the host once, cached.
            rule ??= await AutoDetectResolverAsync(config, candidate, ct).ConfigureAwait(false);
            if (rule == null) continue; // VPN ads / unrelated anchors — only resolver-matched hosts count
            if (detail.Downloads.Any(d => d.Url == candidate)) continue;
            detail.Downloads.Add(new RemoteDownloadOption
            {
                Name = rule.Name,
                Url = candidate,
                Type = rule.Type,
                UnzipPassword = rule.UnzipPassword,
                UnwrapNested = rule.UnwrapNested,
            });
        }

        return detail;
    }

    /// <summary>Opt-in fallback for a download link no static resolver rule matched: probe the host for each
    /// type in <see cref="RemoteSourceConfig.AutoDetect"/> and, on a hit, return a synthetic rule of that
    /// type reusing the same-type static rule's Name/password. Only "kodbox" is detectable today.</summary>
    private async Task<RemoteResolverRule?> AutoDetectResolverAsync(RemoteSourceConfig config, string url, CancellationToken ct)
    {
        if (config.AutoDetect.Count == 0) return null;
        foreach (var type in config.AutoDetect)
        {
            if (!string.Equals(type, "kodbox", StringComparison.OrdinalIgnoreCase)) continue; // only kodbox for now
            if (!await _kodboxDetector.IsKodboxAsync(url, ct).ConfigureAwait(false)) continue;
            // Reuse the same-type static rule's display/password metadata when the source defines one.
            var template = config.Resolvers.FirstOrDefault(r => string.Equals(r.Type, "kodbox", StringComparison.OrdinalIgnoreCase));
            Logger.Info($"[Remote] auto-detected kodbox host: {url}", nameof(HttpRegexEngine));
            return new RemoteResolverRule
            {
                Match = string.Empty,
                Type = "kodbox",
                Name = template?.Name ?? "Hui盘",
                UnzipPassword = template?.UnzipPassword,
                UnwrapNested = template?.UnwrapNested ?? false,
            };
        }
        return null;
    }

    // ---- extraction helpers ----------------------------------------------------------------

    private List<RemoteModCard> ExtractCards(RemoteSourceConfig config, string html)
    {
        // Scope to the main list region when the adapter defines one (hot/recent sidebars repeat
        // the same cards on every page). Fallback: whole page (e.g. search layouts).
        if (!string.IsNullOrWhiteSpace(config.CardScopePattern))
        {
            var scope = Match(config.CardScopePattern, html);
            if (scope.Success) html = scope.Groups["scope"].Value;
        }
        // Dedup by detail URL (hot/recent sidebars repeat items, some anchors are image-only) —
        // keep the first entry with a non-empty title.
        var byUrl = new Dictionary<string, RemoteModCard>();
        var order = new List<string>();
        foreach (Match m in Matches(config.CardPattern, html))
        {
            var url = Absolute(config.BaseUrl, m.Groups["url"].Value);
            var card = new RemoteModCard
            {
                DetailUrl = url,
                ImageUrl = Absolute(config.BaseUrl, m.Groups["image"].Value),
                Title = StripTags(m.Groups["title"].Value),
            };
            if (!byUrl.TryGetValue(url, out var existing))
            {
                byUrl[url] = card;
                order.Add(url);
            }
            else if (string.IsNullOrEmpty(existing.Title) && !string.IsNullOrEmpty(card.Title))
            {
                byUrl[url] = card;
            }
        }
        return order.Select(u => byUrl[u]).ToList();
    }

    private int? ExtractTotalPages(RemoteSourceConfig config, string listId, string html)
    {
        if (string.IsNullOrWhiteSpace(config.TotalPagesPattern)) return null;
        var pattern = config.TotalPagesPattern.Replace("{list}", Regex.Escape(listId));
        int max = 0;
        foreach (Match m in Matches(pattern, html))
        {
            if (int.TryParse(m.Groups["pages"].Value, out var n) && n > max) max = n;
        }
        return max > 0 ? max : null;
    }

    private static Match Match(string pattern, string input) =>
        Regex.Match(input, pattern, RegexOptions.CultureInvariant, RegexTimeout);

    private static MatchCollection Matches(string pattern, string input) =>
        Regex.Matches(input, pattern, RegexOptions.CultureInvariant, RegexTimeout);

    private static bool SafeIsMatch(string pattern, string input)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant, RegexTimeout); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", string.Empty, RegexOptions.CultureInvariant, RegexTimeout)
            .Replace("&nbsp;", " ").Replace("&amp;", "&").Trim();

    /// <summary>Rich-text body → readable plain text: br/p boundaries become line breaks, tags go,
    /// blank-line runs collapse.</summary>
    private static string HtmlToPlainText(string html)
    {
        var text = Regex.Replace(html, "<br\\s*/?>|</p>", "\n", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
        text = StripTags(text);
        return Regex.Replace(text, "[ \\t]*\\n(\\s*\\n)+", "\n\n", RegexOptions.CultureInvariant, RegexTimeout).Trim();
    }
}
