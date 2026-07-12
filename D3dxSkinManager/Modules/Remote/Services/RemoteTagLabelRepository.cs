using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE storage for remote tag labels/aliases (RemoteTagLabels table — migration 202607120002).
/// Replaces {profile}/remote-tag-labels.json. Dapper, SYNCHRONOUS (few rows) so
/// <see cref="RemoteTagLabelStore"/> keeps its synchronous contract. Shape: sourceId → lang → rawTag → label.
/// </summary>
public interface IRemoteTagLabelRepository
{
    /// <summary>Per-language labels for a source (lang → rawTag → label); empty if none.</summary>
    Dictionary<string, Dictionary<string, string>> GetForSource(string sourceId);
    bool HasSource(string sourceId);
    int Count();
    /// <summary>Replace ALL labels for a source (used for seed + JSON migration).</summary>
    void ReplaceSource(string sourceId, Dictionary<string, Dictionary<string, string>> labels);
    /// <summary>Replace one language's labels for a source (empty map clears that language).</summary>
    void ReplaceLang(string sourceId, string lang, Dictionary<string, string> labels);
}

public class RemoteTagLabelRepository : IRemoteTagLabelRepository
{
    private readonly string _connectionString;

    public RemoteTagLabelRepository(IProfilePathService profilePaths)
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

    public Dictionary<string, Dictionary<string, string>> GetForSource(string sourceId)
    {
        using var c = Open();
        var rows = c.Query<(string Lang, string RawTag, string Label)>(
            "SELECT Lang, RawTag, Label FROM RemoteTagLabels WHERE SourceId = @sourceId", new { sourceId });
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var r in rows)
        {
            if (!result.TryGetValue(r.Lang, out var table))
                result[r.Lang] = table = new Dictionary<string, string>();
            table[r.RawTag] = r.Label;
        }
        return result;
    }

    public bool HasSource(string sourceId)
    {
        using var c = Open();
        return c.ExecuteScalar<int>(
            "SELECT EXISTS(SELECT 1 FROM RemoteTagLabels WHERE SourceId = @sourceId)", new { sourceId }) == 1;
    }

    public int Count()
    {
        using var c = Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM RemoteTagLabels");
    }

    public void ReplaceSource(string sourceId, Dictionary<string, Dictionary<string, string>> labels)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        c.Execute("DELETE FROM RemoteTagLabels WHERE SourceId = @sourceId", new { sourceId }, tx);
        foreach (var (lang, table) in labels)
            InsertLang(c, tx, sourceId, lang, table);
        tx.Commit();
    }

    public void ReplaceLang(string sourceId, string lang, Dictionary<string, string> labels)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        c.Execute("DELETE FROM RemoteTagLabels WHERE SourceId = @sourceId AND Lang = @lang", new { sourceId, lang }, tx);
        InsertLang(c, tx, sourceId, lang, labels);
        tx.Commit();
    }

    private static void InsertLang(SqliteConnection c, SqliteTransaction tx, string sourceId, string lang, Dictionary<string, string> table)
    {
        foreach (var (rawTag, label) in table)
        {
            if (string.IsNullOrWhiteSpace(rawTag) || string.IsNullOrWhiteSpace(label)) continue;
            c.Execute(
                "INSERT OR REPLACE INTO RemoteTagLabels (SourceId, Lang, RawTag, Label) VALUES (@sourceId, @lang, @rawTag, @label)",
                new { sourceId, lang, rawTag = rawTag.Trim(), label = label.Trim() }, tx);
        }
    }
}
