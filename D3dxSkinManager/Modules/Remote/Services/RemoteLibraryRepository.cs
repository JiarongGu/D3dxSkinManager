using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE storage for the configured remote LIBRARIES (RemoteLibraries table in the profile
/// SQLite DB — migration 202607120001). Replaces {profile}/remote-libraries.json so library data is
/// native to SQL. Dapper, SYNCHRONOUS (a handful of rows) so <see cref="RemoteLibraryStore"/> keeps its
/// synchronous <c>IRemoteLibraryStore</c> contract. TagRules are stored as a JSON array in one column.
/// </summary>
public interface IRemoteLibraryRepository
{
    /// <summary>All libraries in display order (SortOrder ASC).</summary>
    List<RemoteLibrary> GetAll();
    /// <summary>The id of the row with Active = 1 (null if none).</summary>
    string? GetActiveId();
    int Count();
    void Insert(RemoteLibrary library, long sortOrder, bool active);
    /// <summary>Update name + tag rules only (source/list identity is fixed). True if a row changed.</summary>
    bool Update(RemoteLibrary library);
    bool Delete(string id);
    /// <summary>Set exactly one row Active = 1 (all others 0).</summary>
    void SetActive(string? id);
    /// <summary>Next SortOrder to append at the end.</summary>
    long NextSortOrder();
}

public class RemoteLibraryRepository : IRemoteLibraryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _connectionString;

    public RemoteLibraryRepository(IProfilePathService profilePaths)
    {
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    public List<RemoteLibrary> GetAll()
    {
        using var c = Open();
        var rows = c.Query<Row>(
            "SELECT Id, SourceId, ListId, Name, TagRules, AddedAtUtc FROM RemoteLibraries ORDER BY SortOrder ASC");
        return rows.Select(ToLibrary).ToList();
    }

    public string? GetActiveId()
    {
        using var c = Open();
        return c.ExecuteScalar<string?>("SELECT Id FROM RemoteLibraries WHERE Active = 1 LIMIT 1");
    }

    public int Count()
    {
        using var c = Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM RemoteLibraries");
    }

    public long NextSortOrder()
    {
        using var c = Open();
        // COALESCE(MAX, -1)+1 → 0 for an empty table, else append after the last.
        return c.ExecuteScalar<long>("SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM RemoteLibraries");
    }

    public void Insert(RemoteLibrary library, long sortOrder, bool active)
    {
        using var c = Open();
        c.Execute(@"
            INSERT INTO RemoteLibraries (Id, SourceId, ListId, Name, TagRules, Active, SortOrder, AddedAtUtc)
            VALUES (@Id, @SourceId, @ListId, @Name, @TagRules, @Active, @SortOrder, @AddedAtUtc)",
            new
            {
                library.Id,
                library.SourceId,
                library.ListId,
                library.Name,
                TagRules = JsonSerializer.Serialize(library.TagRules ?? new(), JsonOptions),
                Active = active ? 1 : 0,
                SortOrder = sortOrder,
                library.AddedAtUtc,
            });
    }

    public bool Update(RemoteLibrary library)
    {
        using var c = Open();
        return c.Execute(
            "UPDATE RemoteLibraries SET Name = @Name, TagRules = @TagRules WHERE Id = @Id",
            new
            {
                library.Id,
                library.Name,
                TagRules = JsonSerializer.Serialize(library.TagRules ?? new(), JsonOptions),
            }) > 0;
    }

    public bool Delete(string id)
    {
        using var c = Open();
        return c.Execute("DELETE FROM RemoteLibraries WHERE Id = @id", new { id }) > 0;
    }

    public void SetActive(string? id)
    {
        using var c = Open();
        c.Execute("UPDATE RemoteLibraries SET Active = CASE WHEN Id = @id THEN 1 ELSE 0 END", new { id });
    }

    private static RemoteLibrary ToLibrary(Row r) => new()
    {
        Id = r.Id,
        SourceId = r.SourceId,
        ListId = r.ListId,
        Name = r.Name,
        TagRules = string.IsNullOrWhiteSpace(r.TagRules)
            ? new()
            : (JsonSerializer.Deserialize<List<RemoteTagRule>>(r.TagRules, JsonOptions) ?? new()),
        AddedAtUtc = r.AddedAtUtc,
    };

    /// <summary>Raw row shape (TagRules stays a JSON string until deserialized).</summary>
    private sealed class Row
    {
        public string Id { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string ListId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? TagRules { get; set; }
        public DateTime AddedAtUtc { get; set; }
    }
}
