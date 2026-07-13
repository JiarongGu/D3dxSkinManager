using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE storage for the synced remote-library index (RemoteIndexEntries/RemoteIndexMeta in
/// the profile SQLite DB — migration 202607050002). Dapper, same conventions as the other
/// profile repositories. Filtering/sorting/paging run in SQL so the UI never loads the full list.
/// </summary>
public interface IRemoteIndexRepository
{
    Task<RemoteIndexMetaRow?> GetMetaAsync(string sourceId, string listId);
    Task SetMetaAsync(RemoteIndexMetaRow meta);
    Task<HashSet<string>> GetKnownIdsAsync(string sourceId, string listId);
    Task UpsertEntriesAsync(string sourceId, string listId, IReadOnlyList<RemoteIndexEntry> entries, long generation);
    /// <summary>Soft-delete entries a FULL crawl no longer saw (Generation &lt; the crawl's generation).
    /// Returns the number marked removed. Never call after an incremental crawl — it stops early.</summary>
    Task<int> PruneStaleAsync(string sourceId, string listId, long currentGeneration);

    /// <summary>Merge extra tags into one entry's tag list (e.g. the sub category a GameBanana detail
    /// page reveals — the subfeed only carries the super). Flat merge, order-preserving, deduped.</summary>
    Task MergeEntryTagsAsync(string sourceId, string listId, string entryId, IReadOnlyList<string> tags);

    /// <summary>Entries whose detail page hasn't been processed yet (EnrichedUtc NULL), newest first.</summary>
    Task<List<RemoteIndexEntry>> GetUnenrichedAsync(string sourceId, string listId, int limit);

    /// <summary>How many entries still need detail processing (drives the enrichment progress %).</summary>
    Task<int> CountUnenrichedAsync(string sourceId, string listId);

    /// <summary>Already-enriched entries whose cached detail is STALE — <c>DetailFetchedUtc</c> is NULL
    /// (enriched before detail-caching existed) or older than <paramref name="staleBefore"/> — for a
    /// proactive refresh (a mod's tags/description/downloads change on the site over time). Stalest first,
    /// capped at <paramref name="limit"/>.</summary>
    Task<List<RemoteIndexEntry>> GetStaleDetailAsync(string sourceId, string listId, DateTime staleBefore, int limit);

    /// <summary>Stamp an entry's detail as re-CHECKED now WITHOUT changing the cached content — used when a
    /// stale re-sync finds the mod removed (keep the last-good detail, but leave the stale window so a dead
    /// page isn't re-hit every sync).</summary>
    Task TouchDetailFetchedAsync(string sourceId, string listId, string entryId);

    /// <summary>Stamp an entry's detail as processed (with or without new tags).</summary>
    Task MarkEnrichedAsync(string sourceId, string listId, string entryId);

    /// <summary>Persist an entry's fetched DETAIL content (JSON: images/downloads/description) so the
    /// detail screen can fall back to it when a live re-fetch fails. Overwrites the previous copy; a
    /// no-op if the entry isn't indexed (only synced entries get a detail cache).</summary>
    Task UpsertDetailAsync(string sourceId, string listId, string entryId, string detailJson);

    /// <summary>The last-persisted detail JSON for an entry, or null if none cached yet.</summary>
    Task<string?> GetDetailJsonAsync(string sourceId, string listId, string entryId);

    Task<int> CountAsync(string sourceId, string listId);
    /// <summary><paramref name="tagLabels"/> = the source's per-language display labels; search terms
    /// matching a LABEL also match the raw tag (labels are searchable, not display-only).</summary>
    Task<(int Total, List<RemoteIndexEntry> Entries)> QueryAsync(
        string sourceId, string listId, string? search, string? tag, string? sort, int page, int pageSize,
        Dictionary<string, Dictionary<string, string>>? tagLabels = null, IReadOnlyCollection<string>? onlyEntryIds = null);
    /// <summary>Distinct site tags present in the index (for the filter dropdown), by frequency.</summary>
    Task<List<RemoteTagCount>> GetTagsAsync(string sourceId, string listId);
}

/// <summary>Meta row for one source+list index (sync bookkeeping).</summary>
public class RemoteIndexMetaRow
{
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public DateTime? SyncedAtUtc { get; set; }
    public int TotalPages { get; set; }
    public long Generation { get; set; }

    /// <summary>When a COMPLETE pass over every page last finished. Incremental early-stopping is
    /// only sound once this is set — otherwise the next sync must crawl everything (a partial first
    /// crawl + incrementals would leave a permanent hole).</summary>
    public DateTime? FullSyncCompletedUtc { get; set; }
}

public class RemoteIndexRepository : IRemoteIndexRepository
{
    private readonly string _connectionString;

    public RemoteIndexRepository(IProfilePathService profilePaths)
    {
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    public async Task<RemoteIndexMetaRow?> GetMetaAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var meta = await connection.QuerySingleOrDefaultAsync<RemoteIndexMetaRow>(
            "SELECT * FROM RemoteIndexMeta WHERE SourceId = @sourceId AND ListId = @listId",
            new { sourceId, listId });
        if (meta != null)
        {
            // SQLite loses DateTimeKind — re-mark as UTC so JSON serializes with the Z suffix.
            // Without it the frontend parses the timestamp as LOCAL time and (east of UTC) every
            // index looked hours stale → auto-sync fired on every library-page open (fixed 2026-07-06).
            meta.SyncedAtUtc = SpecifyUtc(meta.SyncedAtUtc);
            meta.FullSyncCompletedUtc = SpecifyUtc(meta.FullSyncCompletedUtc);
        }
        return meta;
    }

    private static DateTime? SpecifyUtc(DateTime? value) =>
        value is { Kind: DateTimeKind.Unspecified } dt ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : value;

    public async Task SetMetaAsync(RemoteIndexMetaRow meta)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO RemoteIndexMeta (SourceId, ListId, SyncedAtUtc, TotalPages, Generation, FullSyncCompletedUtc)
            VALUES (@SourceId, @ListId, @SyncedAtUtc, @TotalPages, @Generation, @FullSyncCompletedUtc)
            ON CONFLICT(SourceId, ListId) DO UPDATE SET
                SyncedAtUtc = excluded.SyncedAtUtc,
                TotalPages = excluded.TotalPages,
                Generation = excluded.Generation,
                FullSyncCompletedUtc = excluded.FullSyncCompletedUtc", meta);
    }

    public async Task<HashSet<string>> GetKnownIdsAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var ids = await connection.QueryAsync<string>(
            "SELECT EntryId FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId",
            new { sourceId, listId });
        return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpsertEntriesAsync(string sourceId, string listId, IReadOnlyList<RemoteIndexEntry> entries, long generation)
    {
        if (entries.Count == 0) return;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        foreach (var e in entries)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO RemoteIndexEntries
                    (SourceId, ListId, EntryId, Title, DetailUrl, ImageUrl, Tags, DateHint, Sensitive, Generation, SortKey, FirstSeenUtc, LastSeenUtc)
                VALUES (@sourceId, @listId, @Id, @Title, @DetailUrl, @ImageUrl, @Tags, @DateHint, @Sensitive, @generation, @SortKey, @Now, @Now)
                ON CONFLICT(SourceId, ListId, EntryId) DO UPDATE SET
                    Title = CASE WHEN excluded.Title != '' THEN excluded.Title ELSE Title END,
                    DetailUrl = excluded.DetailUrl,
                    ImageUrl = excluded.ImageUrl,
                    -- Keep the RICHER tag list: list pages carry only the coarse tag (GameBanana
                    -- subfeed = super category) while enrichment merges the detail page's tags
                    -- (sub category — what the card shows). A plain overwrite wiped the merged
                    -- tags on every re-sync (fixed 2026-07-06).
                    Tags = CASE
                        WHEN json_array_length(COALESCE(excluded.Tags, '[]')) > json_array_length(COALESCE(Tags, '[]'))
                        THEN excluded.Tags ELSE Tags END,
                    DateHint = COALESCE(excluded.DateHint, DateHint),
                    Sensitive = COALESCE(excluded.Sensitive, Sensitive),
                    Generation = excluded.Generation,
                    SortKey = excluded.SortKey,
                    LastSeenUtc = excluded.LastSeenUtc,
                    RemovedUtc = NULL", // a re-seen entry is un-removed
                new
                {
                    sourceId, listId, e.Id, e.Title, e.DetailUrl, e.ImageUrl,
                    Tags = e.Tags.Count > 0 ? global::System.Text.Json.JsonSerializer.Serialize(e.Tags) : null,
                    e.DateHint, e.Sensitive, generation, e.SortKey, Now = DateTime.UtcNow,
                },
                tx);
        }
        await tx.CommitAsync();
    }

    public async Task<int> PruneStaleAsync(string sourceId, string listId, long currentGeneration)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteAsync(@"
            UPDATE RemoteIndexEntries SET RemovedUtc = @Now
            WHERE SourceId = @sourceId AND ListId = @listId
              AND Generation < @currentGeneration AND RemovedUtc IS NULL",
            new { sourceId, listId, currentGeneration, Now = DateTime.UtcNow });
    }

    public async Task MergeEntryTagsAsync(string sourceId, string listId, string entryId, IReadOnlyList<string> tags)
    {
        if (tags.Count == 0) return;
        await using var connection = new SqliteConnection(_connectionString);
        var existingJson = await connection.ExecuteScalarAsync<string?>(
            "SELECT Tags FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { sourceId, listId, entryId });

        List<string> merged;
        try
        {
            merged = string.IsNullOrEmpty(existingJson)
                ? new List<string>()
                : global::System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingJson!) ?? new List<string>();
        }
        catch { merged = new List<string>(); }

        var before = merged.Count;
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !merged.Contains(tag, StringComparer.OrdinalIgnoreCase))
                merged.Add(tag);
        }
        if (merged.Count == before) return; // nothing new

        await connection.ExecuteAsync(
            "UPDATE RemoteIndexEntries SET Tags = @tags WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { tags = global::System.Text.Json.JsonSerializer.Serialize(merged), sourceId, listId, entryId });
    }

    public async Task<List<RemoteIndexEntry>> GetUnenrichedAsync(string sourceId, string listId, int limit)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<RemoteIndexEntry>(@"
            SELECT EntryId AS Id, Title, DetailUrl, ImageUrl, Tags AS TagsJson
            FROM RemoteIndexEntries
            WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL AND EnrichedUtc IS NULL
            ORDER BY Generation DESC, SortKey ASC
            LIMIT @limit", new { sourceId, listId, limit = Math.Max(1, limit) });
        return rows.ToList();
    }

    public async Task<int> CountUnenrichedAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL AND EnrichedUtc IS NULL",
            new { sourceId, listId });
    }

    public async Task<List<RemoteIndexEntry>> GetStaleDetailAsync(string sourceId, string listId, DateTime staleBefore, int limit)
    {
        await using var connection = new SqliteConnection(_connectionString);
        // Enriched (has been processed) but the detail is old/absent. NULL DetailFetchedUtc sorts first
        // under ASC, so entries never given cached content refresh before merely-old ones.
        var rows = await connection.QueryAsync<RemoteIndexEntry>(@"
            SELECT EntryId AS Id, Title, DetailUrl, ImageUrl, Tags AS TagsJson
            FROM RemoteIndexEntries
            WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL AND EnrichedUtc IS NOT NULL
              AND (DetailFetchedUtc IS NULL OR DetailFetchedUtc < @staleBefore)
            ORDER BY DetailFetchedUtc ASC
            LIMIT @limit", new { sourceId, listId, staleBefore, limit = Math.Max(1, limit) });
        return rows.ToList();
    }

    public async Task TouchDetailFetchedAsync(string sourceId, string listId, string entryId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE RemoteIndexEntries SET DetailFetchedUtc = @Now WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { sourceId, listId, entryId, Now = DateTime.UtcNow });
    }

    public async Task MarkEnrichedAsync(string sourceId, string listId, string entryId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE RemoteIndexEntries SET EnrichedUtc = @Now WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { sourceId, listId, entryId, Now = DateTime.UtcNow });
    }

    public async Task UpsertDetailAsync(string sourceId, string listId, string entryId, string detailJson)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "UPDATE RemoteIndexEntries SET DetailJson = @detailJson, DetailFetchedUtc = @Now WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { detailJson, sourceId, listId, entryId, Now = DateTime.UtcNow });
    }

    public async Task<string?> GetDetailJsonAsync(string sourceId, string listId, string entryId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteScalarAsync<string?>(
            "SELECT DetailJson FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId AND EntryId = @entryId",
            new { sourceId, listId, entryId });
    }

    public async Task<int> CountAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL",
            new { sourceId, listId });
    }

    public async Task<(int Total, List<RemoteIndexEntry> Entries)> QueryAsync(
        string sourceId, string listId, string? search, string? tag, string? sort, int page, int pageSize,
        Dictionary<string, Dictionary<string, string>>? tagLabels = null, IReadOnlyCollection<string>? onlyEntryIds = null)
    {
        var where = "SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL";
        var args = new DynamicParameters(new { sourceId, listId });

        if (onlyEntryIds != null)
        {
            // "Downloaded only" filter — restrict to the entry ids imported into this profile. Dapper
            // expands the IN list; an EMPTY set correctly matches nothing (nothing downloaded yet).
            where += " AND EntryId IN @onlyEntryIds";
            args.Add("onlyEntryIds", onlyEntryIds);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            // The filter value may be a raw tag OR a display LABEL. Labels merge several raw tags
            // (A,B → C), so selecting the merged "C" chip must match every raw tag that maps to C.
            // Expand the value through the labels (EXACT label match) + always include the value itself
            // (raw-tag case, and sources with no labels). Tags is a JSON array — json_each (bundled JSON1).
            var tagValues = ExpandLabelToRawTags(tagLabels, tag);
            tagValues.Add(tag);
            where += " AND EXISTS (SELECT 1 FROM json_each(RemoteIndexEntries.Tags) WHERE json_each.value IN @tagValues)";
            args.Add("tagValues", tagValues.Distinct().ToList());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < terms.Length; i++)
            {
                // Escape LIKE wildcards in the user's term.
                var escaped = terms[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                // Each term matches the TITLE or any TAG (comprehensive search, like the mod search).
                var clause = $"Title LIKE @term{i} ESCAPE '\\' OR EXISTS (" +
                             $"SELECT 1 FROM json_each(RemoteIndexEntries.Tags) WHERE json_each.value LIKE @term{i} ESCAPE '\\')";

                // Tag ALIASES are searchable too: a term matching a label in ANY language expands to
                // its raw tag(s) (e.g. "角色皮肤" finds mods tagged "Character Skins").
                var expanded = ExpandTermThroughLabels(tagLabels, terms[i]);
                if (expanded.Count > 0)
                {
                    clause += $" OR EXISTS (SELECT 1 FROM json_each(RemoteIndexEntries.Tags) WHERE json_each.value IN @exp{i})";
                    args.Add($"exp{i}", expanded);
                }

                where += $" AND ({clause})";
                args.Add($"term{i}", $"%{escaped}%");
            }
        }

        var order = string.Equals(sort, "date", StringComparison.OrdinalIgnoreCase)
            // Newest date hint first; undated entries sink, keeping their site order.
            ? "(DateHint IS NULL) ASC, DateHint DESC, Generation DESC, SortKey ASC"
            : "Generation DESC, SortKey ASC";

        await using var connection = new SqliteConnection(_connectionString);
        var total = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM RemoteIndexEntries WHERE {where}", args);

        args.Add("limit", Math.Clamp(pageSize, 1, 500));
        args.Add("offset", (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 500));
        var rows = (await connection.QueryAsync<RemoteIndexEntry>($@"
            SELECT EntryId AS Id, Title, DetailUrl, ImageUrl, Tags AS TagsJson, DateHint, Sensitive, SortKey, FirstSeenUtc, LastSeenUtc
            FROM RemoteIndexEntries WHERE {where}
            ORDER BY {order}
            LIMIT @limit OFFSET @offset", args)).ToList();
        foreach (var row in rows) // materialize the JSON tag list + re-mark UTC kinds for the wire
        {
            row.FirstSeenUtc = SpecifyUtc(row.FirstSeenUtc) ?? row.FirstSeenUtc;
            row.LastSeenUtc = SpecifyUtc(row.LastSeenUtc) ?? row.LastSeenUtc;
            if (string.IsNullOrEmpty(row.TagsJson)) continue;
            try { row.Tags = global::System.Text.Json.JsonSerializer.Deserialize<List<string>>(row.TagsJson) ?? new(); }
            catch { /* corrupt cache row — leave empty */ }
        }
        return (total, rows);
    }

    /// <summary>Raw tags whose alias (in ANY language) contains the search term, case-insensitive.</summary>
    private static List<string> ExpandTermThroughLabels(
        Dictionary<string, Dictionary<string, string>>? tagLabels, string term)
    {
        var result = new List<string>();
        if (tagLabels == null) return result;
        foreach (var lang in tagLabels.Values)
        {
            foreach (var (rawTag, label) in lang)
            {
                if (label.Contains(term, StringComparison.OrdinalIgnoreCase) && !result.Contains(rawTag))
                    result.Add(rawTag);
            }
        }
        return result;
    }

    /// <summary>Raw tags whose display label EXACTLY equals <paramref name="label"/> in any language —
    /// expands a merged label-filter (A,B → C) back to its raw tags. Empty for a plain raw tag / no
    /// labels (the caller then falls back to matching the value verbatim).</summary>
    private static List<string> ExpandLabelToRawTags(
        Dictionary<string, Dictionary<string, string>>? tagLabels, string label)
    {
        var result = new List<string>();
        if (tagLabels == null) return result;
        foreach (var lang in tagLabels.Values)
            foreach (var (rawTag, lbl) in lang)
                if (lbl.Equals(label, StringComparison.OrdinalIgnoreCase) && !result.Contains(rawTag))
                    result.Add(rawTag);
        return result;
    }

    public async Task<List<RemoteTagCount>> GetTagsAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<RemoteTagCount>(@"
            SELECT json_each.value AS Name, COUNT(*) AS Count
            FROM RemoteIndexEntries, json_each(RemoteIndexEntries.Tags)
            WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL
            GROUP BY json_each.value ORDER BY Count DESC, Name ASC",
            new { sourceId, listId });
        return rows.ToList();
    }
}
