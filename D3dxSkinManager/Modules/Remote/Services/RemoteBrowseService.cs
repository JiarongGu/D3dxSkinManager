using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Browsing layer of the remote mod library: fetches list/search/detail pages of a configured
/// source and extracts cards/details with the source's regex patterns. Pure read — download+import
/// live in <see cref="RemoteImportService"/>. Patterns run with a match timeout so a bad
/// user-supplied config can't hang the app.
/// </summary>
public interface IRemoteBrowseService
{
    Task<List<RemoteSourceInfo>> GetSourcesAsync();
    Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page, CancellationToken ct = default);
    Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, CancellationToken ct = default);
    Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl, CancellationToken ct = default);

    /// <summary>Run a CANDIDATE config (not necessarily saved) against the live site: parse list
    /// page 1 + the first card's detail, report what was extracted — the adapter authoring loop.</summary>
    Task<RemoteSourceTestResult> TestConfigAsync(RemoteSourceConfig config, string? listId, CancellationToken ct = default);
}

public class RemoteBrowseService : IRemoteBrowseService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    private readonly IRemoteSourceStore _sources;
    private readonly IRemotePageFetcher _fetcher;
    private readonly ILogHelper _logger;

    public RemoteBrowseService(IRemoteSourceStore sources, IRemotePageFetcher fetcher, ILogHelper logger)
    {
        _sources = sources;
        _fetcher = fetcher;
        _logger = logger;
    }

    public Task<List<RemoteSourceInfo>> GetSourcesAsync()
    {
        var list = _sources.GetAll().Select(s => new RemoteSourceInfo
        {
            Id = s.Id,
            Name = s.Name,
            BaseUrl = s.BaseUrl,
            Lists = s.Lists,
            HasSearch = !string.IsNullOrWhiteSpace(s.SearchUrlTemplate),
        }).ToList();
        return Task.FromResult(list);
    }

    public Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page, CancellationToken ct = default) =>
        BrowseCoreAsync(_sources.GetById(sourceId), listId, page, ct);

    private async Task<RemoteBrowseResult> BrowseCoreAsync(RemoteSourceConfig source, string listId, int page, CancellationToken ct)
    {
        var template = page <= 1 ? source.ListUrlFirstPage : source.ListUrlTemplate;
        var path = template.Replace("{list}", listId).Replace("{page}", page.ToString());
        var html = await FetchAsync(source, Absolute(source.BaseUrl, path), ct).ConfigureAwait(false);

        var result = new RemoteBrowseResult { Page = Math.Max(1, page), Cards = ExtractCards(source, html) };
        result.TotalPages = ExtractTotalPages(source, listId, html);
        return result;
    }

    public async Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, CancellationToken ct = default)
    {
        var source = _sources.GetById(sourceId);
        if (string.IsNullOrWhiteSpace(source.SearchUrlTemplate))
            throw new OperationException("REMOTE_SEARCH_UNSUPPORTED", "source", source.Name);

        var path = source.SearchUrlTemplate.Replace("{query}", Uri.EscapeDataString(query.Trim()));
        var html = await FetchAsync(source, Absolute(source.BaseUrl, path), ct).ConfigureAwait(false);
        return new RemoteBrowseResult { Page = 1, Cards = ExtractCards(source, html) };
    }

    public Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl, CancellationToken ct = default) =>
        GetDetailCoreAsync(_sources.GetById(sourceId), detailUrl, ct);

    public async Task<RemoteSourceTestResult> TestConfigAsync(RemoteSourceConfig config, string? listId, CancellationToken ct = default)
    {
        var list = listId ?? config.Lists.FirstOrDefault()?.Id
            ?? throw new OperationException("REMOTE_SOURCE_INVALID", "reason", "config has no lists");

        var browse = await BrowseCoreAsync(config, list, 1, ct).ConfigureAwait(false);
        var result = new RemoteSourceTestResult
        {
            CardCount = browse.Cards.Count,
            SampleTitles = browse.Cards.Take(5).Select(c => c.Title).ToList(),
            TotalPages = browse.TotalPages,
        };

        var firstCard = browse.Cards.FirstOrDefault();
        if (firstCard != null)
        {
            var detail = await GetDetailCoreAsync(config, firstCard.DetailUrl, ct).ConfigureAwait(false);
            result.DetailTitle = detail.Title;
            result.DetailDownloads = detail.Downloads;
            result.DetailImageCount = detail.Images.Count;
        }
        return result;
    }

    private async Task<RemoteModDetail> GetDetailCoreAsync(RemoteSourceConfig source, string detailUrl, CancellationToken ct)
    {
        var url = Absolute(source.BaseUrl, detailUrl);
        // Containment: only fetch pages of the configured site with the site's parser.
        if (!url.StartsWith(source.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            throw new OperationException("REMOTE_FETCH_FAILED", "url", detailUrl);

        var html = await FetchAsync(source, url, ct).ConfigureAwait(false);

        var detail = new RemoteModDetail { DetailUrl = url };

        var titleMatch = Match(source.DetailTitlePattern, html);
        detail.Title = titleMatch.Success ? StripTags(titleMatch.Groups["title"].Value) : string.Empty;

        foreach (Match m in Matches(source.DetailImagePattern, html))
        {
            var image = Absolute(source.BaseUrl, m.Groups["image"].Value);
            if (!detail.Images.Contains(image)) detail.Images.Add(image);
        }

        foreach (Match m in Matches(source.DownloadLinkPattern, html))
        {
            var candidate = m.Groups["url"].Value;
            var rule = source.Resolvers.FirstOrDefault(r => SafeIsMatch(r.Match, candidate));
            if (rule == null) continue; // VPN ads / unrelated anchors — only resolver-matched hosts count
            if (detail.Downloads.Any(d => d.Url == candidate)) continue;
            detail.Downloads.Add(new RemoteDownloadOption { Name = rule.Name, Url = candidate, Type = rule.Type });
        }

        return detail;
    }

    // ---- extraction helpers ----------------------------------------------------------------

    private List<RemoteModCard> ExtractCards(RemoteSourceConfig source, string html)
    {
        // Dedup by detail URL (hot/recent sidebars repeat items, some anchors are image-only) —
        // keep the first entry with a non-empty title.
        var byUrl = new Dictionary<string, RemoteModCard>();
        var order = new List<string>();
        foreach (Match m in Matches(source.CardPattern, html))
        {
            var url = Absolute(source.BaseUrl, m.Groups["url"].Value);
            var card = new RemoteModCard
            {
                DetailUrl = url,
                ImageUrl = Absolute(source.BaseUrl, m.Groups["image"].Value),
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

    private int? ExtractTotalPages(RemoteSourceConfig source, string listId, string html)
    {
        if (string.IsNullOrWhiteSpace(source.TotalPagesPattern)) return null;
        var pattern = source.TotalPagesPattern.Replace("{list}", Regex.Escape(listId));
        int max = 0;
        foreach (Match m in Matches(pattern, html))
        {
            if (int.TryParse(m.Groups["pages"].Value, out var n) && n > max) max = n;
        }
        return max > 0 ? max : null;
    }

    private async Task<string> FetchAsync(RemoteSourceConfig source, string url, CancellationToken ct)
    {
        if (!string.Equals(source.Engine, "http", StringComparison.OrdinalIgnoreCase))
            throw new OperationException("REMOTE_ENGINE_UNSUPPORTED", "engine", source.Engine);
        try
        {
            return await _fetcher.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (OperationException ex) when (ex.Code == "DOWNLOAD_FAILED")
        {
            _logger.Warn($"Remote fetch failed: {url} — {ex.Message}", "RemoteBrowseService");
            throw new OperationException("REMOTE_FETCH_FAILED", "url", url);
        }
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

    /// <summary>Resolve a possibly-relative URL against the source origin.</summary>
    private static string Absolute(string baseUrl, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url;
        // AbsoluteUri (not ToString()) — ToString() unescapes non-ASCII query chars, breaking the URL.
        return new Uri(new Uri(baseUrl), url).AbsoluteUri;
    }
}
