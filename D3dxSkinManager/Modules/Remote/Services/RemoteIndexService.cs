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
    /// <paramref name="sort"/>: "site" (default) or "date" (newest DateHint first).
    /// <paramref name="tag"/>: filter to entries carrying one site tag (null = all).</summary>
    Task<RemoteIndexPage> QueryAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, IReadOnlyCollection<string>? onlyEntryIds = null);

    /// <summary>Distinct site tags in the index (for the filter dropdown), by frequency.</summary>
    Task<List<RemoteTagCount>> GetTagsAsync(string sourceId, string listId);

    /// <summary>Merge extra tags into an entry (keyed by its detail URL) — e.g. the sub category a
    /// GameBanana detail page reveals; the subfeed only carries the super. Flat tags, no hierarchy.</summary>
    Task MergeEntryTagsByUrlAsync(string sourceId, string listId, string detailUrl, IReadOnlyList<string> tags);

    /// <summary>QueryAsync + import annotation in ONE step — the index-query orchestration that used to
    /// live in the facade. <paramref name="importedOnly"/> first restricts the query to this source+list's
    /// imported entry ids, then every returned entry is flagged/located against the import lookup (cached
    /// in RemoteImportService — no per-page rescan). Keeps the facade a thin delegate.</summary>
    Task<RemoteIndexPage> QueryAnnotatedAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, bool importedOnly = false);

    /// <summary>Learn any tags a DETAIL page revealed that its list feed didn't carry (a GameBanana sub
    /// category) so the tag filter picks them up over time. Fire-and-forget (the detail view never waits
    /// on the write); no-op without a listId or tags. Encapsulates the merge POLICY the facade used to inline.</summary>
    void MergeDetailTags(string sourceId, string? listId, RemoteModDetail detail);

    /// <summary>Fetch a detail page LIVE, persist its content for offline fallback, and learn any
    /// detail-only tags. If the LIVE fetch fails (site down / scraping blocked) fall back to the last
    /// persisted copy when one exists (live-first, cache-fallback); otherwise the error surfaces.</summary>
    Task<RemoteModDetail> GetDetailAsync(string sourceId, string? listId, string detailUrl, bool preferCache = false, CancellationToken ct = default);

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
    private readonly IRemoteTagLabelStore _tagLabels;
    private readonly IRemoteBrowseService _browse;
    private readonly IRemoteImportService _import;
    private readonly IRemoteIndexRepository _repository;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;
    private readonly HashSet<string> _activeSyncs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    public RemoteIndexService(
        IRemoteSourceStore sources,
        IRemoteTagLabelStore tagLabels,
        IRemoteBrowseService browse,
        IRemoteImportService import,
        IRemoteIndexRepository repository,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _sources = sources;
        _tagLabels = tagLabels;
        _browse = browse;
        _import = import;
        _repository = repository;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public Task<List<RemoteTagCount>> GetTagsAsync(string sourceId, string listId) =>
        _repository.GetTagsAsync(sourceId, listId);

    public Task MergeEntryTagsByUrlAsync(string sourceId, string listId, string detailUrl, IReadOnlyList<string> tags)
    {
        if (tags.Count == 0) return Task.CompletedTask;
        var source = _sources.GetById(sourceId);
        return _repository.MergeEntryTagsAsync(sourceId, listId, ExtractEntryId(source, detailUrl), tags);
    }

    public async Task<RemoteIndexPage> QueryAnnotatedAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, bool importedOnly = false)
    {
        // "Downloaded only" restricts the query to this source+list's imported entry ids (import-domain
        // lookup, cached in RemoteImportService — no per-page mod rescan); then every returned entry is
        // flagged/located against the same lookup.
        var onlyEntryIds = importedOnly
            ? await _import.GetImportedEntryIdsAsync(sourceId, listId).ConfigureAwait(false)
            : null;
        var result = await QueryAsync(sourceId, listId, search, page, pageSize, sort, tag, onlyEntryIds).ConfigureAwait(false);
        await _import.AnnotateImportedAsync(result.Entries, sourceId, listId).ConfigureAwait(false);
        return result;
    }

    public void MergeDetailTags(string sourceId, string? listId, RemoteModDetail detail)
    {
        // Detail pages can reveal tags the list feed doesn't carry (a GameBanana sub category) — merge
        // them into the index entry so the tag filter learns them over time. Flat tags, no hierarchy;
        // fire-and-forget so the detail view never waits on the index write.
        if (string.IsNullOrWhiteSpace(listId) || detail.Tags.Count == 0) return;
        _ = Task.Run(() => MergeEntryTagsByUrlAsync(sourceId, listId!, detail.DetailUrl, detail.Tags));
    }

    public async Task<RemoteModDetail> GetDetailAsync(string sourceId, string? listId, string detailUrl, bool preferCache = false, CancellationToken ct = default)
    {
        var source = _sources.GetById(sourceId);
        var entryId = ExtractEntryId(source, detailUrl);

        // Cache-first (per-library option): serve the last-saved copy immediately when present (fast +
        // offline); a live re-fetch is on the detail page's Refresh button. A cache MISS falls through to
        // a live fetch. The detail page's Refresh always passes preferCache=false to force a live pull.
        if (preferCache && !string.IsNullOrWhiteSpace(listId))
        {
            var cachedFirst = await ReadCachedDetailAsync(sourceId, listId!, entryId).ConfigureAwait(false);
            if (cachedFirst != null) return cachedFirst;
        }

        try
        {
            var detail = await _browse.GetDetailAsync(sourceId, detailUrl, listId, ct).ConfigureAwait(false);
            // Persist the fresh content for offline fallback + learn detail-only tags. Only when the entry
            // is indexed (the UPDATE no-ops otherwise); a cache-write failure must NOT break a live hit.
            if (!string.IsNullOrWhiteSpace(listId))
            {
                try
                {
                    await _repository.UpsertDetailAsync(sourceId, listId!, entryId,
                        global::System.Text.Json.JsonSerializer.Serialize(detail)).ConfigureAwait(false);
                }
                catch (Exception px)
                {
                    _logger.Warn($"[Remote] Failed to cache detail for {detailUrl}: {px.Message}", "RemoteIndexService");
                }
                if (detail.Tags.Count > 0)
                    _ = MergeEntryTagsByUrlAsync(sourceId, listId!, detailUrl, detail.Tags);
            }
            return detail;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Live fetch failed → serve the last-persisted copy if we have one (live-first, cache-fallback).
            if (!string.IsNullOrWhiteSpace(listId))
            {
                var cached = await ReadCachedDetailAsync(sourceId, listId!, entryId).ConfigureAwait(false);
                if (cached != null)
                {
                    _logger.Warn($"[Remote] Live detail fetch failed for {detailUrl} ({ex.Message}) — served cached copy", "RemoteIndexService");
                    return cached;
                }
            }
            throw;
        }
    }

    /// <summary>The last-persisted detail for an entry, deserialized — or null if none/corrupt.</summary>
    private async Task<RemoteModDetail?> ReadCachedDetailAsync(string sourceId, string listId, string entryId)
    {
        var cached = await _repository.GetDetailJsonAsync(sourceId, listId, entryId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(cached)) return null;
        try { return global::System.Text.Json.JsonSerializer.Deserialize<RemoteModDetail>(cached!); }
        catch { return null; }
    }

    public async Task<RemoteIndexPage> QueryAsync(string sourceId, string listId, string? search, int page, int pageSize, string? sort = null, string? tag = null, IReadOnlyCollection<string>? onlyEntryIds = null)
    {
        var meta = await _repository.GetMetaAsync(sourceId, listId).ConfigureAwait(false);
        // Pass the PER-PROFILE tag-alias table so search terms matching an alias (any language) hit the
        // raw tag too (aliases are searchable, not display-only). Seeded from the source's defaults.
        var tagLabels = _tagLabels.GetForSource(sourceId, _sources.GetById(sourceId).TagLabels);
        var (total, entries) = await _repository.QueryAsync(sourceId, listId, search, tag, sort, page, pageSize, tagLabels, onlyEntryIds).ConfigureAwait(false);
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
                // The LIST SYNC is now complete — the index is up to date. Detail ENRICHMENT is a
                // SEPARATE process (missing-data backfill: e.g. GameBanana sub-category tags), chained
                // after the crawl so requests to the site stay serialized.
                StartEnrichment(source, listId, listName);
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

    /// <summary>Kick the detail-enrichment backfill as its OWN cancellable process (idempotent).</summary>
    private void StartEnrichment(RemoteSourceConfig source, string listId, string listName)
    {
        if (!_browse.DetailProvidesTags(source.Id, listId)) return;

        var key = $"enrich:{source.Id}_{listId}";
        lock (_syncLock)
        {
            if (_activeSyncs.Contains(key)) return;
            _activeSyncs.Add(key);
        }

        var procId = _processRegistry.Start(ProcessType.Download, $"Filling mod details: {source.Name} · {listName}",
            cancellable: true, titleKey: "process.remoteEnrich", titleArg: $"{source.Name} · {listName}");

        _ = Task.Run(async () =>
        {
            var ct = _processRegistry.GetToken(procId);
            try
            {
                await EnrichDetailsAsync(source.Id, listId, listName, procId, ct).ConfigureAwait(false);
                _processRegistry.Complete(procId);
            }
            catch (OperationCanceledException)
            {
                _processRegistry.Cancel(procId);
            }
            catch (Exception ex)
            {
                _logger.Error($"[Remote] Detail enrichment failed for {key}: {ex.Message}", "RemoteIndexService", ex);
                _processRegistry.Fail(procId, ex.Message);
            }
            finally
            {
                lock (_syncLock) { _activeSyncs.Remove(key); }
            }
        });
    }

    /// <summary>How many consecutive fully-known pages an incremental needs before it may stop —
    /// feeds aren't strictly ordered (GameBanana mixes featured/recently-updated), so a single
    /// known page can precede pages with unseen entries.</summary>
    private const int IncrementalStopAfterKnownPages = 2;

    /// <summary>How long incremental-only updating is allowed before the next "update" is forced to
    /// a FULL pass. Incremental syncs stop early and never prune, so site-DELETED entries would
    /// linger forever; a periodic full pass bounds that staleness (and re-fills any missed deep
    /// pages) without slowing the common fast path.</summary>
    private static readonly TimeSpan FullResyncInterval = TimeSpan.FromDays(7);

    /// <summary>
    /// Crawl the list. A pass is INCREMENTAL (early-stopping) only when a COMPLETE pass finished
    /// before — otherwise (first sync, forced full, or a previously interrupted/partial crawl) it
    /// walks every page, so the index can never keep a permanent hole.
    /// </summary>
    private async Task CrawlAsync(RemoteSourceConfig source, string listId, string listName, string procId, bool full, CancellationToken ct)
    {
        var meta = await _repository.GetMetaAsync(source.Id, listId).ConfigureAwait(false);
        // Incremental ONLY after a completed full pass — SyncedAtUtc alone isn't enough (a cancelled
        // first crawl wrote entries; stopping early on top of that would never fill the deep pages).
        // AND only while that full pass is recent: once it's older than FullResyncInterval, the next
        // update walks every page again so PruneStaleAsync can drop entries removed from the site.
        var fullPassStale = meta?.FullSyncCompletedUtc is { } lastFull
            && DateTime.UtcNow - lastFull >= FullResyncInterval;
        var incremental = meta?.FullSyncCompletedUtc != null && !full && !fullPassStale;
        if (fullPassStale && !full)
            _logger.Info($"[Remote] Forcing a full re-crawl for {source.Id}_{listId} (last full pass >{FullResyncInterval.TotalDays:0}d ago) to prune stale entries", "RemoteIndexService");
        var generation = (meta?.Generation ?? 0) + 1;
        var known = incremental
            ? await _repository.GetKnownIdsAsync(source.Id, listId).ConfigureAwait(false)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _processRegistry.Report(procId, 1, $"{listName} 1", detailKey: "process.stage.crawling");
        var first = await _browse.BrowseAsync(source.Id, listId, 1, ct).ConfigureAwait(false);
        var totalPages = Math.Min(first.TotalPages ?? MaxPages, MaxPages);
        var crawledPages = 1;
        var stoppedEarly = false;
        var consecutiveKnown = 0;
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

            consecutiveKnown = newCount == 0 ? consecutiveKnown + 1 : 0;
            if (incremental && consecutiveKnown >= IncrementalStopAfterKnownPages)
            {
                // Several fully-known pages in a row — anything deeper is already indexed.
                _logger.Info($"[Remote] Update sync stopped at page {page} ({consecutiveKnown} known pages in a row)", "RemoteIndexService");
                stoppedEarly = true;
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

        // A non-incremental pass reaching this line covered every page (only incrementals stop
        // early) — record the completion so future syncs may go incremental. Interrupted/cancelled
        // runs never get here, so a partial crawl can't unlock early-stopping.
        _ = stoppedEarly; // (documented above — early stop implies incremental)
        var fullCompleted = incremental ? meta!.FullSyncCompletedUtc : DateTime.UtcNow;

        await _repository.SetMetaAsync(new RemoteIndexMetaRow
        {
            SourceId = source.Id,
            ListId = listId,
            SyncedAtUtc = DateTime.UtcNow,
            TotalPages = totalPages,
            Generation = generation,
            FullSyncCompletedUtc = fullCompleted,
        }).ConfigureAwait(false);

        var count = await _repository.CountAsync(source.Id, listId).ConfigureAwait(false);
        _logger.Info($"[Remote] Index synced: {source.Id}_{listId} — {count} entries, crawled {crawledPages}/{totalPages} pages, pruned {pruned} ({(incremental ? "update" : "full")})",
            "RemoteIndexService");
    }

    /// <summary>Per-worker politeness delay between detail fetches during enrichment.</summary>
    private static readonly TimeSpan DetailDelay = TimeSpan.FromMilliseconds(150);
    /// <summary>Small parallelism for the detail backfill (per user: "allow for small parallel").</summary>
    private const int DetailParallelism = 3;
    /// <summary>Abort enrichment after this many failures within one batch (site down / blocking).</summary>
    private const int MaxBatchDetailFailures = 15;

    private async Task EnrichDetailsAsync(string sourceId, string listId, string listName, string procId, CancellationToken ct)
    {
        var total = await _repository.CountUnenrichedAsync(sourceId, listId).ConfigureAwait(false);
        if (total == 0) return;

        var processed = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var batch = await _repository.GetUnenrichedAsync(sourceId, listId, 200).ConfigureAwait(false);
            if (batch.Count == 0) break;

            var batchFailures = 0;
            await Parallel.ForEachAsync(batch,
                new ParallelOptions { MaxDegreeOfParallelism = DetailParallelism, CancellationToken = ct },
                async (entry, token) =>
                {
                    await Task.Delay(DetailDelay, token).ConfigureAwait(false);
                    try
                    {
                        var detail = await _browse.GetDetailAsync(sourceId, entry.DetailUrl, listId, token).ConfigureAwait(false);
                        if (detail.Tags.Count > 0)
                            await _repository.MergeEntryTagsAsync(sourceId, listId, entry.Id, detail.Tags).ConfigureAwait(false);
                        // Cache the detail CONTENT too (not just tags) so the detail screen has an offline
                        // fallback — this backfill IS the bulk "sync detail content" pass.
                        await _repository.UpsertDetailAsync(sourceId, listId, entry.Id,
                            global::System.Text.Json.JsonSerializer.Serialize(detail)).ConfigureAwait(false);
                        await _repository.MarkEnrichedAsync(sourceId, listId, entry.Id).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.Warn($"[Remote] Detail enrichment failed for {entry.DetailUrl}: {ex.Message}", "RemoteIndexService");
                        Interlocked.Increment(ref batchFailures);
                    }
                    var done = Interlocked.Increment(ref processed);
                    if (done % 5 == 0 || done == total)
                    {
                        // Rich progress: what's processing now + done/total (percent drives the bar).
                        _processRegistry.Report(procId, (int)Math.Min(100, done * 100.0 / total),
                            $"{entry.Title} · {done}/{total}", detailKey: "process.stage.enrichingDetails");
                    }
                }).ConfigureAwait(false);

            if (batchFailures >= MaxBatchDetailFailures)
            {
                _logger.Warn($"[Remote] Enrichment aborted: {batchFailures} failures in one batch", "RemoteIndexService");
                return; // failed rows stay unmarked and retry on the next sync
            }
            if (batchFailures >= batch.Count)
            {
                // Every row in the batch failed — nothing was marked, so looping would re-fetch the
                // same rows forever. Bail; they retry on the next sync.
                _logger.Warn("[Remote] Enrichment aborted: batch made no progress", "RemoteIndexService");
                return;
            }
        }
        _logger.Info($"[Remote] Detail enrichment: processed {processed}/{total} entries for {sourceId}_{listId}", "RemoteIndexService");
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
                Tags = card.Tags,
                DateHint = card.DateHint ?? ExtractDateHint(source, card.ImageUrl),
                Sensitive = card.Sensitive,
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
