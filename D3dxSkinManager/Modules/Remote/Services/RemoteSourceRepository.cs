using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE mirror of the site adapter configs (RemoteSources table — migration 202607120002). The
/// GLOBAL {data}/remote-sources/*.json files remain the editable DEFINITION; RemoteSourceStore syncs them
/// into this table on load, and everything reads from here. The full RemoteSourceConfig is stored as JSON
/// in one column (a nested config read whole). Dapper, SYNCHRONOUS (few rows).
/// </summary>
public interface IRemoteSourceRepository
{
    List<RemoteSourceConfig> GetAll();
    RemoteSourceConfig? GetById(string id);
    int Count();
    void Upsert(RemoteSourceConfig config);
    bool Delete(string id);
    /// <summary>Make the table match the given definition set: upsert all + delete rows whose id is absent.</summary>
    void Sync(IReadOnlyList<RemoteSourceConfig> configs);
}

public class RemoteSourceRepository : IRemoteSourceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly string _connectionString;

    public RemoteSourceRepository(IProfilePathService profilePaths)
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

    public List<RemoteSourceConfig> GetAll()
    {
        using var c = Open();
        var jsons = c.Query<string>("SELECT ConfigJson FROM RemoteSources ORDER BY Id COLLATE NOCASE");
        return jsons.Select(Deserialize).Where(x => x != null).Select(x => x!).ToList();
    }

    public RemoteSourceConfig? GetById(string id)
    {
        using var c = Open();
        var json = c.ExecuteScalar<string?>("SELECT ConfigJson FROM RemoteSources WHERE Id = @id", new { id });
        return json == null ? null : Deserialize(json);
    }

    public int Count()
    {
        using var c = Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM RemoteSources");
    }

    public void Upsert(RemoteSourceConfig config)
    {
        using var c = Open();
        UpsertInternal(c, null, config);
    }

    public bool Delete(string id)
    {
        using var c = Open();
        return c.Execute("DELETE FROM RemoteSources WHERE Id = @id", new { id }) > 0;
    }

    public void Sync(IReadOnlyList<RemoteSourceConfig> configs)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var config in configs)
            UpsertInternal(c, tx, config);

        // Remove rows whose source no longer exists in the definition (JSON deleted).
        var keepIds = configs.Select(s => s.Id).ToList();
        if (keepIds.Count == 0)
            c.Execute("DELETE FROM RemoteSources", transaction: tx);
        else
            c.Execute("DELETE FROM RemoteSources WHERE Id NOT IN @keepIds", new { keepIds }, tx);
        tx.Commit();
    }

    private static void UpsertInternal(SqliteConnection c, SqliteTransaction? tx, RemoteSourceConfig config)
    {
        c.Execute(
            "INSERT OR REPLACE INTO RemoteSources (Id, ConfigJson) VALUES (@Id, @ConfigJson)",
            new { config.Id, ConfigJson = JsonSerializer.Serialize(config, JsonOptions) }, tx);
    }

    private static RemoteSourceConfig? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<RemoteSourceConfig>(json, JsonOptions); }
        catch { return null; }
    }
}
