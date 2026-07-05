using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The SYNCED local index of a remote source list: a background crawl walks every list page once
/// and persists the entries ({data}/remote-sources/.cache/{source}_{list}.json), so browsing,
/// filtering and search afterwards are instant + offline — no per-query site requests. Entries are
/// keyed by the site's stable id (adapter EntryIdPattern), carry a date hint (from the image path)
/// and first/last-seen stamps, and keep the site's own recency order (SortKey).
/// Sync is fire-and-forget with ONE cancellable ProcessRegistry entry (background-task-tracking.md).
/// </summary>
public interface IRemoteIndexService
{
    /// <summary>Filtered + paged slice of the cached index (empty info when never synced).</summary>
    RemoteIndexPage Query(string sourceId, string listId, string? search, int page, int pageSize);

    /// <summary>Start a background crawl of all pages of the list. Returns the process id.</summary>
    string StartSync(string sourceId, string listId);
}

public class RemoteIndexService : IRemoteIndexService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    /// <summary>Politeness delay between page fetches during a sync.</summary>
    private static readonly TimeSpan PageDelay = TimeSpan.FromMilliseconds(250);
    /// <summary>Backstop when a site exposes no total-pages hint.</summary>
    private const int MaxPages = 500;

    private readonly IRemoteSourceStore _sources;
    private readonly IRemoteBrowseService _browse;
    private readonly IGlobalPathService _globalPaths;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;
    private readonly HashSet<string> _activeSyncs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    public RemoteIndexService(
        IRemoteSourceStore sources,
        IRemoteBrowseService browse,
        IGlobalPathService globalPaths,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _sources = sources;
        _browse = browse;
        _globalPaths = globalPaths;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public RemoteIndexPage Query(string sourceId, string listId, string? search, int page, int pageSize)
    {
        var cache = Load(sourceId, listId) ?? new RemoteIndexCache
        {
            Info = new RemoteIndexInfo { SourceId = sourceId, ListId = listId },
        };

        IEnumerable<RemoteIndexEntry> filtered = cache.Entries;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filtered = filtered.Where(e => terms.All(t => e.Title.Contains(t, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = filtered.OrderBy(e => e.SortKey).ToList();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        return new RemoteIndexPage
        {
            Info = cache.Info,
            Total = ordered.Count,
            Entries = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }

    public string StartSync(string sourceId, string listId)
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
                var cache = Load(sourceId, listId) ?? new RemoteIndexCache();
                var byId = cache.Entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
                var now = DateTime.UtcNow;

                _processRegistry.Report(procId, 1, "Page 1", detailKey: "process.stage.crawling");
                var first = await _browse.BrowseAsync(sourceId, listId, 1, ct).ConfigureAwait(false);
                var totalPages = Math.Min(first.TotalPages ?? MaxPages, MaxPages);
                Merge(byId, first.Cards, source, pageNumber: 1, now);

                for (var page = 2; page <= totalPages; page++)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(PageDelay, ct).ConfigureAwait(false);
                    var result = await _browse.BrowseAsync(sourceId, listId, page, ct).ConfigureAwait(false);
                    if (result.Cards.Count == 0 && first.TotalPages == null) break; // unknown total — stop at the first empty page
                    Merge(byId, result.Cards, source, page, now);

                    _processRegistry.Report(procId, (int)(page * 100.0 / totalPages), $"Page {page}/{totalPages}",
                        detailKey: "process.stage.crawling");
                    if (page % 20 == 0) Save(sourceId, listId, byId, totalPages, syncedAt: null); // crash checkpoint
                }

                Save(sourceId, listId, byId, totalPages, DateTime.UtcNow);
                _processRegistry.Complete(procId);
                _logger.Info($"[Remote] Index synced: {key} — {byId.Count} entries / {totalPages} pages", "RemoteIndexService");
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

    // ---- helpers ---------------------------------------------------------------------------

    private void Merge(Dictionary<string, RemoteIndexEntry> byId, IReadOnlyList<RemoteModCard> cards,
        RemoteSourceConfig source, int pageNumber, DateTime now)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var id = ExtractEntryId(source, card.DetailUrl);
            if (!byId.TryGetValue(id, out var entry))
            {
                entry = new RemoteIndexEntry { Id = id, FirstSeenUtc = now };
                byId[id] = entry;
            }
            entry.Title = string.IsNullOrEmpty(card.Title) ? entry.Title : card.Title;
            entry.DetailUrl = card.DetailUrl;
            entry.ImageUrl = card.ImageUrl;
            entry.DateHint = ExtractDateHint(source, card.ImageUrl) ?? entry.DateHint;
            entry.SortKey = pageNumber * 10000L + i;
            entry.LastSeenUtc = now;
        }
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

    private string CachePath(string sourceId, string listId)
    {
        var dir = Path.Combine(_globalPaths.RemoteSourcesDirectory, ".cache");
        Directory.CreateDirectory(dir);
        var safe = string.Concat($"{sourceId}_{listId}".Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
        return Path.Combine(dir, $"{safe}.json");
    }

    private RemoteIndexCache? Load(string sourceId, string listId)
    {
        var path = CachePath(sourceId, listId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<RemoteIndexCache>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Corrupt index cache {Path.GetFileName(path)}: {ex.Message}", "RemoteIndexService");
            return null;
        }
    }

    private void Save(string sourceId, string listId, Dictionary<string, RemoteIndexEntry> byId, int totalPages, DateTime? syncedAt)
    {
        var existing = syncedAt == null ? Load(sourceId, listId)?.Info.SyncedAtUtc : null;
        var cache = new RemoteIndexCache
        {
            Info = new RemoteIndexInfo
            {
                SourceId = sourceId,
                ListId = listId,
                SyncedAtUtc = syncedAt ?? existing,
                TotalPages = totalPages,
                EntryCount = byId.Count,
            },
            Entries = byId.Values.OrderBy(e => e.SortKey).ToList(),
        };
        File.WriteAllText(CachePath(sourceId, listId), JsonSerializer.Serialize(cache, JsonOptions));
    }
}
