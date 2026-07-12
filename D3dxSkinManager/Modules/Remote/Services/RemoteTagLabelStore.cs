using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE remote tag display labels / aliases. Storage is the profile SQLite DB (RemoteTagLabels
/// table via <see cref="IRemoteTagLabelRepository"/>) — moved off {profile}/remote-tag-labels.json so all
/// remote data is native to SQL. On first access the legacy JSON is migrated into the table once, then
/// removed.
///
/// Shape: sourceId → language code → raw tag → display label. On first access for a source the profile
/// copy is SEEDED from the source config's shipped/global defaults (nothing lost, shipped labels still
/// appear), then it is authoritative — later edits write here only and never leak across profiles.
/// </summary>
public interface IRemoteTagLabelStore
{
    /// <summary>Effective per-language labels for a source (lang → rawTag → label). Seeded once from
    /// <paramref name="globalDefaults"/> (the source config's <c>TagLabels</c>) if this profile has none yet.</summary>
    Dictionary<string, Dictionary<string, string>> GetForSource(
        string sourceId, Dictionary<string, Dictionary<string, string>>? globalDefaults);

    /// <summary>Replace the labels for ONE language of a source (blank pairs dropped). Other languages are
    /// seeded from <paramref name="globalDefaults"/> on first write so their defaults are not lost.</summary>
    void SetLangLabels(
        string sourceId, string lang, Dictionary<string, string> labels,
        Dictionary<string, Dictionary<string, string>>? globalDefaults);
}

public class RemoteTagLabelStore : IRemoteTagLabelStore
{
    private static readonly JsonSerializerOptions JsonOptions = RemoteJson.Compact;

    private readonly IRemoteTagLabelRepository _repository;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private bool _migrationChecked;

    public RemoteTagLabelStore(IRemoteTagLabelRepository repository, IProfilePathService profilePaths, ILogHelper logger)
    {
        _repository = repository;
        _profilePaths = profilePaths;
        _logger = logger;
    }

    private string LegacyJsonPath => Path.Combine(_profilePaths.ProfilePath, "remote-tag-labels.json");

    public Dictionary<string, Dictionary<string, string>> GetForSource(
        string sourceId, Dictionary<string, Dictionary<string, string>>? globalDefaults)
    {
        lock (_lock)
        {
            EnsureMigrated();
            if (_repository.HasSource(sourceId))
                return _repository.GetForSource(sourceId);

            // First access for this source in this profile — seed from the shipped/global defaults, then
            // the profile owns an independent copy.
            var seeded = Clone(globalDefaults);
            if (seeded.Count > 0)
                _repository.ReplaceSource(sourceId, seeded);
            return seeded;
        }
    }

    public void SetLangLabels(
        string sourceId, string lang, Dictionary<string, string> labels,
        Dictionary<string, Dictionary<string, string>>? globalDefaults)
    {
        if (string.IsNullOrWhiteSpace(lang)) return;
        lock (_lock)
        {
            EnsureMigrated();
            // Seed other languages from the defaults on first write so editing one language doesn't drop them.
            if (!_repository.HasSource(sourceId))
            {
                var seeded = Clone(globalDefaults);
                if (seeded.Count > 0) _repository.ReplaceSource(sourceId, seeded);
            }

            var cleaned = labels
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value.Trim());
            _repository.ReplaceLang(sourceId, lang, cleaned);
        }
    }

    // ---- one-time JSON → SQLite migration --------------------------------------------------

    private void EnsureMigrated()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;
        try
        {
            if (_repository.Count() > 0) return;         // already has data
            if (!File.Exists(LegacyJsonPath)) return;    // nothing to migrate

            var all = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(
                File.ReadAllText(LegacyJsonPath), JsonOptions);
            if (all != null)
            {
                foreach (var (sourceId, labels) in all)
                    if (labels != null && labels.Count > 0)
                        _repository.ReplaceSource(sourceId, labels);
                _logger.Info($"[Remote] Migrated tag labels for {all.Count} source(s) from JSON into SQLite", "RemoteTagLabelStore");
            }
            TryDelete(LegacyJsonPath);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Tag-label JSON→SQLite migration skipped: {ex.Message}", "RemoteTagLabelStore");
            TryDelete(LegacyJsonPath); // don't retry a corrupt file forever
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static Dictionary<string, Dictionary<string, string>> Clone(
        Dictionary<string, Dictionary<string, string>>? source)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (source == null) return result;
        foreach (var (lang, table) in source)
            result[lang] = new Dictionary<string, string>(table);
        return result;
    }
}
