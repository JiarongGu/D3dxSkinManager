using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// A per-site library engine (remote-library-redesign.md): mostly HARDCODED logic targeting one site
/// family, taking only small config — the base URL + game ids, or the regex fields for the generic
/// HTML engine. Engines fetch + NORMALIZE into the shared DTOs (cards with tags/date, detail with
/// download options); everything downstream — index, sync status, tags, import — is engine-agnostic
/// and never re-implemented per site. Adding a custom site = implementing this interface and shipping
/// a res/remote-sources default config whose <c>engine</c> names it.
/// </summary>
public interface IRemoteSiteEngine
{
    /// <summary>Matched against <see cref="RemoteSourceConfig.Engine"/> (case-insensitive).</summary>
    string EngineId { get; }

    /// <summary>Whether this source can search (drives the UI's search box for live search).</summary>
    bool SupportsSearch(RemoteSourceConfig config);

    Task<RemoteBrowseResult> BrowseAsync(RemoteSourceConfig config, string listId, int page, CancellationToken ct);

    /// <summary><paramref name="listId"/> scopes the search to one game where the site supports it
    /// (e.g. GameBanana <c>_idGameRow</c>); engines without game-scoping ignore it.</summary>
    Task<RemoteBrowseResult> SearchAsync(RemoteSourceConfig config, string query, string? listId, CancellationToken ct);

    Task<RemoteModDetail> GetDetailAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct);
}

/// <summary>Shared plumbing: fetch with the REMOTE_FETCH_FAILED wrap + URL resolution.</summary>
public abstract class RemoteSiteEngineBase : IRemoteSiteEngine
{
    protected readonly IRemotePageFetcher Fetcher;
    protected readonly ILogHelper Logger;

    protected RemoteSiteEngineBase(IRemotePageFetcher fetcher, ILogHelper logger)
    {
        Fetcher = fetcher;
        Logger = logger;
    }

    public abstract string EngineId { get; }
    public abstract bool SupportsSearch(RemoteSourceConfig config);
    public abstract Task<RemoteBrowseResult> BrowseAsync(RemoteSourceConfig config, string listId, int page, CancellationToken ct);
    public abstract Task<RemoteBrowseResult> SearchAsync(RemoteSourceConfig config, string query, string? listId, CancellationToken ct);
    public abstract Task<RemoteModDetail> GetDetailAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct);

    protected async Task<string> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            return await Fetcher.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (OperationException ex) when (ex.Code == "DOWNLOAD_FAILED")
        {
            Logger.Warn($"Remote fetch failed: {url} — {ex.Message}", GetType().Name);
            throw new OperationException("REMOTE_FETCH_FAILED", "url", url);
        }
    }

    /// <summary>Resolve a possibly-relative URL against the source origin.</summary>
    public static string Absolute(string baseUrl, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url;
        // AbsoluteUri (not ToString()) — ToString() unescapes non-ASCII query chars, breaking the URL.
        return new Uri(new Uri(baseUrl), url).AbsoluteUri;
    }
}
