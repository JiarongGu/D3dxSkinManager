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

    /// <summary>Run a CANDIDATE config (not necessarily saved) against the live site: parse list
    /// page 1 + the first card's detail, report what was extracted — the adapter authoring loop.</summary>
    Task<RemoteSourceTestResult> TestConfigAsync(RemoteSourceConfig config, string? listId, CancellationToken ct = default);
}

public class RemoteBrowseService : IRemoteBrowseService
{
    private readonly IRemoteSourceStore _sources;
    private readonly Dictionary<string, IRemoteSiteEngine> _engines;

    public RemoteBrowseService(IRemoteSourceStore sources, IEnumerable<IRemoteSiteEngine> engines)
    {
        _sources = sources;
        _engines = engines.ToDictionary(e => e.EngineId, StringComparer.OrdinalIgnoreCase);
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
        }).ToList();
        return Task.FromResult(list);
    }

    public Task<RemoteBrowseResult> BrowseAsync(string sourceId, string listId, int page, CancellationToken ct = default)
    {
        var config = _sources.GetById(sourceId);
        return Resolve(config).BrowseAsync(config, listId, Math.Max(1, page), ct);
    }

    public Task<RemoteBrowseResult> SearchAsync(string sourceId, string query, string? listId = null, CancellationToken ct = default)
    {
        var config = _sources.GetById(sourceId);
        var engine = Resolve(config);
        if (!engine.SupportsSearch(config))
            throw new OperationException("REMOTE_SEARCH_UNSUPPORTED", "source", config.Name);
        return engine.SearchAsync(config, query, listId, ct);
    }

    public Task<RemoteModDetail> GetDetailAsync(string sourceId, string detailUrl, CancellationToken ct = default)
    {
        var config = _sources.GetById(sourceId);
        return GetDetailCoreAsync(config, detailUrl, ct);
    }

    private Task<RemoteModDetail> GetDetailCoreAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct)
    {
        var url = RemoteSiteEngineBase.Absolute(config.BaseUrl, detailUrl);
        // Containment: only fetch pages of the configured site with the site's engine — generic
        // security, enforced here so no engine can forget it.
        if (!url.StartsWith(config.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            throw new OperationException("REMOTE_FETCH_FAILED", "url", detailUrl);
        return Resolve(config).GetDetailAsync(config, url, ct);
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
