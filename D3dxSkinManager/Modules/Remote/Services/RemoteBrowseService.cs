using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Browsing layer of the remote mod library — a thin DISPATCHER (remote-library-redesign.md): a
/// source config names its engine (the `engine` field = the remote-resource type), the matching
/// <see cref="IRemoteSiteEngine"/> fetches + normalizes, and everything downstream (index, tags,
/// sync, import) is engine-agnostic. Pure read — download+import live in RemoteImportService.
/// </summary>
public interface IRemoteBrowseService
{
    Task<List<RemoteSourceInfo>> GetSourcesAsync();
    Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page, CancellationToken ct = default);
    Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, string? listId = null, CancellationToken ct = default);
    Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl, CancellationToken ct = default);

    /// <summary>True when this source's detail pages carry tags the list feed doesn't — the sync
    /// then runs a detail-enrichment phase (engine capability).</summary>
    bool DetailProvidesTags(string sourceId);

    /// <summary>Run a CANDIDATE config (not necessarily saved) against the live site: parse list
    /// page 1 + the first card's detail, report what was extracted — the adapter authoring loop.</summary>
    Task<RemoteSourceTestResult> TestConfigAsync(RemoteSourceConfig config, string? listId, CancellationToken ct = default);
}

public class RemoteBrowseService : IRemoteBrowseService
{
    private readonly IRemoteSourceStore _sources;
    private readonly IRemoteTagLabelStore _tagLabels;
    private readonly IRemoteSourceResolver _resolver;
    private readonly IRemoteLibraryStore _libraries;
    private readonly Dictionary<string, IRemoteSiteEngine> _engines;

    public RemoteBrowseService(IRemoteSourceStore sources, IRemoteTagLabelStore tagLabels,
        IRemoteSourceResolver resolver, IRemoteLibraryStore libraries, IEnumerable<IRemoteSiteEngine> engines)
    {
        _sources = sources;
        _tagLabels = tagLabels;
        _resolver = resolver;
        _libraries = libraries;
        _engines = engines.ToDictionary(e => e.EngineId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The EFFECTIVE config for a library operation = base source with the library's param
    /// values substituted (<c>{param.key}</c>). <paramref name="listId"/> identifies WHICH library's
    /// params (one source can back several libraries); no listId (a source-level op) = the base config.
    /// A library with no param values resolves to the base unchanged — so this is inert until params are
    /// set. (Local sparse overlay is Phase 4.) See remote-library-redesign.md.</summary>
    private RemoteSourceConfig Effective(string sourceId, string? listId)
    {
        var baseCfg = _sources.GetById(sourceId);
        var paramValues = listId != null ? _libraries.FindBySourceList(sourceId, listId)?.ParamValues : null;
        return paramValues == null || paramValues.Count == 0 ? baseCfg : _resolver.Resolve(baseCfg, null, paramValues);
    }

    /// <summary>The engine a config names via its `engine` field (empty/missing = the "http" default).</summary>
    private IRemoteSiteEngine Resolve(RemoteSourceConfig config)
    {
        var id = string.IsNullOrWhiteSpace(config.Engine) ? "http" : config.Engine;
        return _engines.TryGetValue(id, out var engine)
            ? engine
            : throw new OperationException("REMOTE_ENGINE_UNSUPPORTED", "engine", id);
    }

    public Task<List<RemoteSourceInfo>> GetSourcesAsync()
    {
        var list = _sources.GetAll().Select(s => new RemoteSourceInfo
        {
            Id = s.Id,
            Name = s.Name,
            BaseUrl = s.BaseUrl,
            Lists = s.Lists,
            HasSearch = _engines.TryGetValue(string.IsNullOrWhiteSpace(s.Engine) ? "http" : s.Engine, out var engine)
                        && engine.SupportsSearch(s),
            // PER-PROFILE labels (seeded once from the source's shipped defaults) — never the raw global config.
            TagLabels = _tagLabels.GetForSource(s.Id, s.TagLabels),
            Thumbnail = s.Thumbnail,
            Params = s.Params,
        }).ToList();
        return Task.FromResult(list);
    }

    public async Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page, CancellationToken ct = default)
    {
        var config = Effective(sourceId, listId);
        var result = await Resolve(config).BrowseAsync(config, listId, Math.Max(1, page), ct).ConfigureAwait(false);
        ApplyTitleTags(config, result.Cards);
        return result;
    }

    public async Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, string? listId = null, CancellationToken ct = default)
    {
        var config = Effective(sourceId, listId);
        var engine = Resolve(config);
        if (!engine.SupportsSearch(config))
            throw new OperationException("REMOTE_SEARCH_UNSUPPORTED", "source", config.Name);
        var result = await engine.SearchAsync(config, query, listId, ct).ConfigureAwait(false);
        ApplyTitleTags(config, result.Cards);
        return result;
    }

    public Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl, CancellationToken ct = default)
    {
        var config = _sources.GetById(sourceId);
        return GetDetailCoreAsync(config, detailUrl, ct);
    }

    public bool DetailProvidesTags(string sourceId)
    {
        var config = _sources.GetById(sourceId);
        return Resolve(config).ProvidesDetailTags;
    }

    private async Task<RemoteModDetail> GetDetailCoreAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct)
    {
        var url = RemoteSiteEngineBase.Absolute(config.BaseUrl, detailUrl);
        // Containment: only fetch pages of the configured site with the site's engine — generic
        // security, enforced here so no engine can forget it.
        if (!url.StartsWith(config.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            throw new OperationException("REMOTE_FETCH_FAILED", "url", detailUrl);
        var detail = await Resolve(config).GetDetailAsync(config, url, ct).ConfigureAwait(false);
        if (detail.Tags.Count == 0)
        {
            var derived = DeriveTitleTag(config.TitleTagPattern, detail.Title);
            if (derived != null) detail.Tags.Add(derived);
        }
        return detail;
    }

    /// <summary>Title-derived tags for TAGLESS sites (config `titleTagPattern`, named group: tag —
    /// huihui leads titles with the character name). Runs centrally after the engine normalizes so
    /// browse, search AND the index sync all get the same tags; entries with real site tags are
    /// never touched.</summary>
    private static void ApplyTitleTags(RemoteSourceConfig config, List<RemoteModCard> cards)
    {
        if (string.IsNullOrWhiteSpace(config.TitleTagPattern)) return;
        foreach (var card in cards.Where(c => c.Tags.Count == 0))
        {
            var derived = DeriveTitleTag(config.TitleTagPattern, card.Title);
            if (derived != null) card.Tags.Add(derived);
        }
    }

    /// <summary>Null when the pattern/title is empty, doesn't match, or the regex is invalid —
    /// a bad user pattern must never break browsing. Public static for direct testability.</summary>
    public static string? DeriveTitleTag(string? pattern, string? title)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(title)) return null;
        try
        {
            var match = global::System.Text.RegularExpressions.Regex.Match(
                title, pattern,
                global::System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
            var tag = match.Success ? match.Groups["tag"].Value.Trim() : null;
            return string.IsNullOrWhiteSpace(tag) ? null : tag;
        }
        catch { return null; }
    }

    public async Task<RemoteSourceTestResult> TestConfigAsync(RemoteSourceConfig config, string? listId, CancellationToken ct = default)
    {
        var list = listId ?? config.Lists.FirstOrDefault()?.Id
            ?? throw new OperationException("REMOTE_SOURCE_INVALID", "reason", "config has no lists");

        var browse = await Resolve(config).BrowseAsync(config, list, 1, ct).ConfigureAwait(false);
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
}
