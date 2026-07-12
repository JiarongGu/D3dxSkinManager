using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE remote tag display labels / aliases ({profile}/remote-tag-labels.json). Tag aliases
/// used to live on the GLOBAL <c>RemoteSourceConfig.TagLabels</c> (in {data}/remote-sources), so editing
/// them in one profile changed every profile. They now live here, per profile.
///
/// Shape: sourceId → language code → raw tag → display label. On first access for a source the profile
/// copy is SEEDED from the source config's shipped/global defaults (so nothing is lost + shipped labels
/// still appear), then it is authoritative — later edits write here only and never leak across profiles.
/// </summary>
public interface IRemoteTagLabelStore
{
    /// <summary>Effective per-language labels for a source (lang → rawTag → label). Seeded once from
    /// <paramref name="globalDefaults"/> (the source config's <c>TagLabels</c>) if this profile has none
    /// yet; authoritative for this profile thereafter.</summary>
    Dictionary<string, Dictionary<string, string>> GetForSource(
        string sourceId, Dictionary<string, Dictionary<string, string>>? globalDefaults);

    /// <summary>Replace the labels for ONE language of a source (blank tag/label pairs dropped). Other
    /// languages are seeded from <paramref name="globalDefaults"/> on first write so their defaults are
    /// not lost when only one language is edited.</summary>
    void SetLangLabels(
        string sourceId, string lang, Dictionary<string, string> labels,
        Dictionary<string, Dictionary<string, string>>? globalDefaults);
}

public class RemoteTagLabelStore : IRemoteTagLabelStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();

    public RemoteTagLabelStore(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    private string FilePath => Path.Combine(_profilePaths.ProfilePath, "remote-tag-labels.json");

    public Dictionary<string, Dictionary<string, string>> GetForSource(
        string sourceId, Dictionary<string, Dictionary<string, string>>? globalDefaults)
    {
        lock (_lock)
        {
            var all = Load();
            if (all.TryGetValue(sourceId, out var existing))
                return Clone(existing);

            // First access for this source in this profile — seed from the global/shipped defaults so
            // labels are preserved, then persist so the profile owns an independent copy.
            var seeded = Clone(globalDefaults);
            if (seeded.Count > 0)
            {
                all[sourceId] = seeded;
                Save(all);
            }
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
            var all = Load();
            // Seed the source's OTHER languages from the global defaults on first write so editing one
            // language doesn't drop the defaults of the others.
            if (!all.TryGetValue(sourceId, out var forSource))
            {
                forSource = Clone(globalDefaults);
                all[sourceId] = forSource;
            }

            var cleaned = labels
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value.Trim());

            if (cleaned.Count == 0) forSource.Remove(lang);
            else forSource[lang] = cleaned;

            // Drop the source entirely when it holds nothing (keeps the file tidy + lets a later global
            // default re-seed if the user cleared everything).
            if (forSource.Count == 0) all.Remove(sourceId);

            Save(all);
        }
    }

    // ---- plumbing --------------------------------------------------------------------------

    private Dictionary<string, Dictionary<string, Dictionary<string, string>>> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(
                File.ReadAllText(FilePath), JsonOptions) ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Corrupt remote-tag-labels.json: {ex.Message}", "RemoteTagLabelStore");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(Dictionary<string, Dictionary<string, Dictionary<string, string>>> all) =>
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, JsonOptions));

    /// <summary>Deep-copy a lang → tag → label table (never hand out a reference to internal state).</summary>
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
