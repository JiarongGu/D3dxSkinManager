using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The SYNCED local index of the remote source list a profile targets — PER PROFILE, stored in the
/// profile's SQLite DB (RemoteIndexRepository; migration 202607050002). The first sync crawls every
/// list page; every later sync is an incremental UPDATE: it crawls from page 1 and STOPS at the
/// first page containing nothing new (sites list newest-first, so everything beyond it is already
/// indexed). Crawled entries carry a new sync generation so (Generation DESC, SortKey ASC) keeps
/// the site's recency order across partial crawls. Sync is fire-and-forget with ONE cancellable
/// ProcessRegistry entry (background-task-tracking.md).
/// </summary>
public interface IRemoteIndexService
{
    /// <summary>Filtered + paged slice of the index (empty info when never synced).
    /// <paramref name="sort"/>: "site" (default) or "date" (newest DateHint first).</summary>
    Task<RemoteIndexPage> QueryAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null);

    /// <summary>Start a background sync. <paramref name="full"/> forces a complete re-crawl of every
    /// page and prunes entries the site no longer lists (soft-delete); the default is an incremental
    /// UPDATE that stops at the first page with nothing new. The first-ever sync is always full.</summary>
    string StartSync(string sourceId, string listId, bool full = false);
}

public class RemoteIndexService : IRemoteIndexService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    /// <summary>Politeness delay between page fetches during a sync.</summary>
    private static readonly TimeSpan PageDelay = TimeSpan.FromMilliseconds(250);
    /// <summary>Backstop when a site exposes no total-pages hint.</summary>
    private const int MaxPages = 500;

    private readonly IRemoteSourceStore _sources;
    private readonly IRemoteBrowseService _browse;
    private readonly IRemoteIndexRepository _repository;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;
    private readonly HashSet<string> _activeSyncs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    public RemoteIndexService(
        IRemoteSourceStore sources,
        IRemoteBrowseService browse,
        IRemoteIndexRepository repository,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _sources = sources;
        _browse = browse;
        _repository = repository;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public async Task<RemoteIndexPage> QueryAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null)
    {
        var meta = await _repository.GetMetaAsync(sourceId, listId).ConfigureAwait(false);
        var (total, entries) = await _repository.QueryAsync(sourceId, listId, search, sort, page, pageSize).ConfigureAwait(false);
        return new RemoteIndexPage
        {
            Info = new RemoteIndexInfo
            {
                SourceId = sourceId,
                ListId = listId,
                SyncedAtUtc = meta?.SyncedAtUtc,
                TotalPages = meta?.TotalPages ?? 0,
                EntryCount = await _repository.CountAsync(sourceId, listId).ConfigureAwait(false),
            },
            Total = total,
            Entries = entries,
        };
    }

    public string StartSync(string sourceId, string listId, bool full = false)
    {
        var source = _sources.GetById(sourceId); // validates + errors synchronously on bad input
        var listName = source.Lists.FirstOrDefault(l => l.Id == listId)?.Name ?? listId;
        var key = $"{sourceId}_{listId}";

        lock (_syncLock)
        {
            if (_activeSyncs.Contains(key)) return string.Empty; // already syncing — idempotent no-op
            _activeSyncs.Add(key);
        }

        var procId = _processRegistry.Start(ProcessType.Download, $"Syncing remote library: {source.Name} · {listName}",
            cancellable: true, titleKey: "process.remoteSync", titleArg: $"{source.Name} · {listName}");

        _ = Task.Run(async () => // fire-and-forget — progress/result via the registry
        {
            var ct = _processRegistry.GetToken(procId);
            try
            {
                await CrawlAsync(source, listId, listName, procId, full, ct).ConfigureAwait(false);
                _processRegistry.Complete(procId);
            }
            catch (OperationCanceledException)
            {
                _processRegistry.Cancel(procId);
            }
            catch (Exception ex)
            {
                _logger.Error($"[Remote] Index sync failed for {key}: {ex.Message}", "RemoteIndexService", ex);
                _processRegistry.Fail(procId, ex.Message);
            }
            finally
            {
                lock (_syncLock) { _activeSyncs.Remove(key); }
            }
        });

        return procId;
    }

    /// <summary>
    /// Crawl the list: full on first run; an UPDATE afterwards — stop at the first page that
    /// yields no entry we haven't seen before (only newer content sits above it).
    /// </summary>
    private async Task CrawlAsync(RemoteSourceConfig source, string listId, string listName, string procId, bool full, CancellationToken ct)
    {
        var meta = await _repository.GetMetaAsync(source.Id, listId).ConfigureAwait(false);
        // Incremental only when we have a prior sync AND the caller didn't force a full re-crawl.
        var incremental = meta?.SyncedAtUtc != null && !full;
        var generation = (meta?.Generation ?? 0) + 1;
        var known = incremental
            ? await _repository.GetKnownIdsAsync(source.Id, listId).ConfigureAwait(false)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _processRegistry.Report(procId, 1, $"{listName} 1", detailKey: "process.stage.crawling");
        var first = await _browse.BrowseAsync(source.Id, listId, 1, ct).ConfigureAwait(false);
        var totalPages = Math.Min(first.TotalPages ?? MaxPages, MaxPages);
        var crawledPages = 1;
        await UpsertPageAsync(source, listId, first.Cards, 1, generation).ConfigureAwait(false);

        for (var page = 2; page <= totalPages; page++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PageDelay, ct).ConfigureAwait(false);
            var result = await _browse.BrowseAsync(source.Id, listId, page, ct).ConfigureAwait(false);
            if (result.Cards.Count == 0 && first.TotalPages == null) break; // unknown total — stop at the first empty page

            var newCount = result.Cards.Count(c => !known.Contains(ExtractEntryId(source, c.DetailUrl)));
            await UpsertPageAsync(source, listId, result.Cards, page, generation).ConfigureAwait(false);
            crawledPages = page;

            if (incremental && newCount == 0)
            {
                // Everything on this page (and below — the site lists newest first) is already indexed.
                _logger.Info($"[Remote] Update sync stopped at page {page} (no new entries)", "RemoteIndexService");
                break;
            }

            _processRegistry.Report(procId, (int)(page * 100.0 / totalPages),
                $"{listName} {page}/{totalPages}", detailKey: "process.stage.crawling");
        }

        // A full crawl saw every page, so any entry still on the old generation is gone from the site
        // — soft-delete it (keeps the row so a downloaded mod's reference still resolves). An
        // incremental crawl stops early, so it must NEVER prune (would wrongly drop everything below
        // the stop page).
        var pruned = 0;
        if (!incremental)
        {
            pruned = await _repository.PruneStaleAsync(source.Id, listId, generation).ConfigureAwait(false);
        }

        await _repository.SetMetaAsync(new RemoteIndexMetaRow
        {
            SourceId = source.Id,
            ListId = listId,
            SyncedAtUtc = DateTime.UtcNow,
            TotalPages = totalPages,
            Generation = generation,
        }).ConfigureAwait(false);

        var count = await _repository.CountAsync(source.Id, listId).ConfigureAwait(false);
        _logger.Info($"[Remote] Index synced: {source.Id}_{listId} — {count} entries, crawled {crawledPages}/{totalPages} pages, pruned {pruned} ({(incremental ? "update" : "full")})",
            "RemoteIndexService");
    }

    private Task UpsertPageAsync(RemoteSourceConfig source, string listId, IReadOnlyList<RemoteModCard> cards, int pageNumber, long generation)
    {
        var entries = new List<RemoteIndexEntry>(cards.Count);
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            entries.Add(new RemoteIndexEntry
            {
                Id = ExtractEntryId(source, card.DetailUrl),
                Title = card.Title,
                DetailUrl = card.DetailUrl,
                ImageUrl = card.ImageUrl,
                DateHint = ExtractDateHint(source, card.ImageUrl),
                SortKey = pageNumber * 10000L + i,
            });
        }
        return _repository.UpsertEntriesAsync(source.Id, listId, entries, generation);
    }

    /// <summary>The site's stable id for a detail URL (adapter EntryIdPattern), else the URL itself.</summary>
    public static string ExtractEntryId(RemoteSourceConfig source, string detailUrl)
    {
        if (!string.IsNullOrWhiteSpace(source.EntryIdPattern))
        {
            try
            {
                var m = Regex.Match(detailUrl, source.EntryIdPattern, RegexOptions.CultureInvariant, RegexTimeout);
                if (m.Success) return m.Groups["id"].Value;
            }
            catch (RegexMatchTimeoutException) { /* fall through to the URL */ }
        }
        return detailUrl;
    }

    private static string? ExtractDateHint(RemoteSourceConfig source, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(source.ImageDatePattern)) return null;
        try
        {
            var m = Regex.Match(imageUrl, source.ImageDatePattern, RegexOptions.CultureInvariant, RegexTimeout);
            var raw = m.Success ? m.Groups["date"].Value : null;
            if (raw?.Length == 8) return $"{raw[..4]}-{raw[4..6]}-{raw[6..8]}";
            return null;
        }
        catch (RegexMatchTimeoutException) { return null; }
    }
}
