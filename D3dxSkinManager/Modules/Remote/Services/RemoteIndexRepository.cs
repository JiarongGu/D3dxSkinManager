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
    Task<int> CountAsync(string sourceId, string listId);
    Task<(int Total, List<RemoteIndexEntry> Entries)> QueryAsync(
        string sourceId, string listId, string? search, string? sort, int page, int pageSize);
}

/// <summary>Meta row for one source+list index (sync bookkeeping).</summary>
public class RemoteIndexMetaRow
{
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public DateTime? SyncedAtUtc { get; set; }
    public int TotalPages { get; set; }
    public long Generation { get; set; }
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
        return await connection.QuerySingleOrDefaultAsync<RemoteIndexMetaRow>(
            "SELECT * FROM RemoteIndexMeta WHERE SourceId = @sourceId AND ListId = @listId",
            new { sourceId, listId });
    }

    public async Task SetMetaAsync(RemoteIndexMetaRow meta)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO RemoteIndexMeta (SourceId, ListId, SyncedAtUtc, TotalPages, Generation)
            VALUES (@SourceId, @ListId, @SyncedAtUtc, @TotalPages, @Generation)
            ON CONFLICT(SourceId, ListId) DO UPDATE SET
                SyncedAtUtc = excluded.SyncedAtUtc,
                TotalPages = excluded.TotalPages,
                Generation = excluded.Generation", meta);
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
                    (SourceId, ListId, EntryId, Title, DetailUrl, ImageUrl, DateHint, Generation, SortKey, FirstSeenUtc, LastSeenUtc)
                VALUES (@sourceId, @listId, @Id, @Title, @DetailUrl, @ImageUrl, @DateHint, @generation, @SortKey, @Now, @Now)
                ON CONFLICT(SourceId, ListId, EntryId) DO UPDATE SET
                    Title = CASE WHEN excluded.Title != '' THEN excluded.Title ELSE Title END,
                    DetailUrl = excluded.DetailUrl,
                    ImageUrl = excluded.ImageUrl,
                    DateHint = COALESCE(excluded.DateHint, DateHint),
                    Generation = excluded.Generation,
                    SortKey = excluded.SortKey,
                    LastSeenUtc = excluded.LastSeenUtc,
                    RemovedUtc = NULL", // a re-seen entry is un-removed
                new { sourceId, listId, e.Id, e.Title, e.DetailUrl, e.ImageUrl, e.DateHint, generation, e.SortKey, Now = DateTime.UtcNow },
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

    public async Task<int> CountAsync(string sourceId, string listId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RemoteIndexEntries WHERE SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL",
            new { sourceId, listId });
    }

    public async Task<(int Total, List<RemoteIndexEntry> Entries)> QueryAsync(
        string sourceId, string listId, string? search, string? sort, int page, int pageSize)
    {
        var where = "SourceId = @sourceId AND ListId = @listId AND RemovedUtc IS NULL";
        var args = new DynamicParameters(new { sourceId, listId });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < terms.Length; i++)
            {
                // Escape LIKE wildcards in the user's term.
                var escaped = terms[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                where += $" AND Title LIKE @term{i} ESCAPE '\\'";
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
        var rows = await connection.QueryAsync<RemoteIndexEntry>($@"
            SELECT EntryId AS Id, Title, DetailUrl, ImageUrl, DateHint, SortKey, FirstSeenUtc, LastSeenUtc
            FROM RemoteIndexEntries WHERE {where}
            ORDER BY {order}
            LIMIT @limit OFFSET @offset", args);
        return (total, rows.ToList());
    }
}
