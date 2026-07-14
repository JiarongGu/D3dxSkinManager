using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Loads remote-library site adapters as a 2-tier config (remote-library-redesign.md): the SHIPPED
/// base is {res}/remote-sources/*.json (read-only, csproj Content); a user OVERLAY in
/// {data}/remote-sources/*.json overrides it. The effective config = <c>Resolve(res, overlay)</c> —
/// a SPARSE overlay (only the keys it sets) so res updates to untouched fields flow straight through;
/// a data file with no matching res is a full CUSTOM source. Res is never edited; <see cref="Save"/>
/// writes only the DIFF vs res. The per-profile RemoteSources table (via the repository) is the runtime
/// store everything reads from, re-synced when a res/data mtime changes ("drop a JSON, no restart").
/// </summary>
public interface IRemoteSourceStore
{
    IReadOnlyList<RemoteSourceConfig> GetAll();
    RemoteSourceConfig GetById(string sourceId);

    /// <summary>Validate + persist an adapter. For a res-backed id, only the SPARSE diff vs res is
    /// written to {data}/remote-sources/{id}.json (so res updates keep flowing); a brand-new id is
    /// written in full (a custom source).</summary>
    RemoteSourceConfig Save(RemoteSourceConfig config);

    /// <summary>Remove the {data} overlay for this id. A res-backed source reverts to its shipped
    /// default; a custom (data-only) source is removed entirely. True when a file was removed.</summary>
    bool Delete(string sourceId);

    /// <summary>Per-source origin for the UI: "default" (shipped res, no overlay), "customized" (res +
    /// a local overlay), or "custom" (a data-only source with no res base).</summary>
    IReadOnlyDictionary<string, string> GetOrigins();

    /// <summary>The shipped RES DEFAULT for a source (no local overlay applied), resolved the same way
    /// <see cref="GetById"/> resolves the effective config so a field-by-field compare shows exactly what
    /// the local overlay changed. Null when the source has no res base (a fully custom source).</summary>
    RemoteSourceConfig? GetDefault(string sourceId);
}

public class RemoteSourceStore : IRemoteSourceStore
{
    private static readonly JsonSerializerOptions JsonOptions = RemoteJson.Pretty;

    private readonly IRemoteSourceRepository _repository;
    private readonly IRemoteSourceResolver _resolver;
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    private readonly object _cacheLock = new();
    private string? _lastSyncedSignature;

    public RemoteSourceStore(IRemoteSourceRepository repository, IRemoteSourceResolver resolver,
        IGlobalPathService globalPaths, ILogHelper logger)
    {
        _repository = repository;
        _resolver = resolver;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public IReadOnlyList<RemoteSourceConfig> GetAll()
    {
        var dataDir = _globalPaths.RemoteSourcesDirectory;
        var seedsDir = _globalPaths.RemoteSourceSeedsDirectory;
        Directory.CreateDirectory(dataDir);
        lock (_cacheLock)
        {
            var signature = ComputeSignature(seedsDir, dataDir);
            if (signature != _lastSyncedSignature)
            {
                // One-time cleanup: a legacy FULL copy that equals res is a pure seed → drop it so the
                // source inherits res live (res updates flow). Copies with real overrides are kept.
                if (RemoveNoOpOverlays(seedsDir, dataDir)) signature = ComputeSignature(seedsDir, dataDir);
                _repository.Sync(ResolveAll(seedsDir, dataDir));
                _lastSyncedSignature = signature;
            }
            return _repository.GetAll();
        }
    }

    /// <summary>Cheap change detector over BOTH tiers (res seeds + data overlays): file set + mtimes.</summary>
    private static string ComputeSignature(string seedsDir, string dataDir) =>
        string.Join("|", Files(seedsDir).Concat(Files(dataDir))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"{f}:{File.GetLastWriteTimeUtc(f).Ticks}"));

    private static IEnumerable<string> Files(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : Array.Empty<string>();

    /// <summary>Effective configs = for each id in (res ∪ data): res present → Resolve(res, overlayRaw);
    /// else the data file's own config (custom). Invalid/unparseable entries are skipped with a warning.</summary>
    private List<RemoteSourceConfig> ResolveAll(string seedsDir, string dataDir)
    {
        var res = LoadRawById(seedsDir);
        var data = LoadRawById(dataDir);
        var result = new List<RemoteSourceConfig>();
        foreach (var id in res.Keys.Union(data.Keys, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                RemoteSourceConfig effective;
                if (res.TryGetValue(id, out var baseEntry))
                    effective = _resolver.Resolve(baseEntry.Config, data.TryGetValue(id, out var ov) ? ov.Raw : null, null);
                else
                    effective = data[id].Config; // custom source with no res base
                if (!string.IsNullOrWhiteSpace(effective.Id) && !string.IsNullOrWhiteSpace(effective.BaseUrl))
                    result.Add(effective);
                else
                    _logger.Warn($"Remote source '{id}' resolved without id/baseUrl — skipped", "RemoteSourceStore");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to resolve remote source '{id}': {ex.Message}", "RemoteSourceStore");
            }
        }
        return result;
    }

    /// <summary>Parse every *.json in a dir → id → (parsed config, raw JSON text). Unparseable files skipped.</summary>
    private Dictionary<string, (RemoteSourceConfig Config, string Raw)> LoadRawById(string dir)
    {
        var map = new Dictionary<string, (RemoteSourceConfig, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files(dir).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var raw = File.ReadAllText(file);
                var config = JsonSerializer.Deserialize<RemoteSourceConfig>(raw, JsonOptions);
                if (config == null || string.IsNullOrWhiteSpace(config.Id)) continue;
                map[config.Id] = (config, raw); // last-wins for a duplicate id (deterministic by sorted name)
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse remote source {Path.GetFileName(file)}: {ex.Message}", "RemoteSourceStore");
            }
        }
        return map;
    }

    /// <summary>The single "is this a real override?" test — shared by Save's drop-on-revert AND the
    /// origin / no-op-sweep logic, so the `id`-only threshold can't drift between callers. True when
    /// `effective` differs from `master` by MORE than the always-present "id" key.</summary>
    private bool HasRealDiff(RemoteSourceConfig master, RemoteSourceConfig effective)
    {
        var diff = JsonNode.Parse(_resolver.Diff(master, effective)) as JsonObject;
        return diff != null && diff.Count > 1; // more than just the always-present "id" key
    }

    /// <summary>True when a data overlay actually changes the EFFECTIVE config vs the res master (carries a
    /// REAL override). A sparse overlay that resolves back to master — the user reverted every field, or a
    /// later res update caught up to the override — has no real diff, so the source is really "default" and
    /// the overlay should be dropped (refer back to master). Compares the RESOLVED effective config, so a
    /// sparse overlay is judged by its effect, not by the raw keys it happens to list.</summary>
    private bool OverlayHasRealDiff(RemoteSourceConfig master, string overlayRaw)
        => HasRealDiff(master, _resolver.Resolve(master, overlayRaw, null));

    /// <summary>Delete data overlays that carry NO real override vs res (a legacy full copy, an emptied
    /// overlay, or one whose overrides res later matched) so the source refers to master. Returns true if
    /// any file was removed.</summary>
    private bool RemoveNoOpOverlays(string seedsDir, string dataDir)
    {
        var res = LoadRawById(seedsDir);
        var removed = false;
        foreach (var (id, entry) in LoadRawById(dataDir))
        {
            if (!res.TryGetValue(id, out var baseEntry)) continue; // custom source — keep
            if (!OverlayHasRealDiff(baseEntry.Config, entry.Raw))
            {
                try
                {
                    File.Delete(Path.Combine(dataDir, $"{id}.json"));
                    removed = true;
                    _logger.Info($"Remote source '{id}': overlay matches master → dropped, refers to default", "RemoteSourceStore");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Remote source '{id}': failed to drop no-op overlay: {ex.Message}", "RemoteSourceStore");
                }
            }
        }
        return removed;
    }

    public RemoteSourceConfig GetById(string sourceId)
    {
        var source = GetAll().FirstOrDefault(s => string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        return source ?? throw new OperationException("REMOTE_SOURCE_NOT_FOUND", "id", sourceId);
    }

    public IReadOnlyDictionary<string, string> GetOrigins()
    {
        var res = LoadRawById(_globalPaths.RemoteSourceSeedsDirectory);
        var data = LoadRawById(_globalPaths.RemoteSourcesDirectory);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in res.Keys.Union(data.Keys, StringComparer.OrdinalIgnoreCase))
        {
            if (!res.TryGetValue(id, out var baseEntry))
                origins[id] = "custom"; // data-only source, no res master
            else if (data.TryGetValue(id, out var overlay) && OverlayHasRealDiff(baseEntry.Config, overlay.Raw))
                origins[id] = "customized"; // res + an overlay that REALLY differs from master
            else
                origins[id] = "default"; // shipped as-is, OR a no-op overlay that resolves back to master
        }
        return origins;
    }

    public RemoteSourceConfig? GetDefault(string sourceId)
    {
        var res = LoadRawById(_globalPaths.RemoteSourceSeedsDirectory);
        if (!res.TryGetValue(sourceId, out var baseEntry)) return null; // custom source — no res default
        // Resolve res with NO overlay (params filled from declared defaults, same as GetById) so the diff
        // isolates exactly the local overlay's overrides.
        return _resolver.Resolve(baseEntry.Config, null, null);
    }

    public RemoteSourceConfig Save(RemoteSourceConfig config)
    {
        Validate(config);
        var dataDir = _globalPaths.RemoteSourcesDirectory;
        Directory.CreateDirectory(dataDir);

        // One file per adapter id — remove any OTHER-named file carrying the same id (rename case).
        foreach (var file in Files(dataDir))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<RemoteSourceConfig>(File.ReadAllText(file), JsonOptions);
                if (existing != null && string.Equals(existing.Id, config.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileNameWithoutExtension(file), config.Id, StringComparison.OrdinalIgnoreCase))
                    File.Delete(file);
            }
            catch { /* unparseable neighbours are not our problem here */ }
        }

        // Res-backed id → persist only the SPARSE diff (res updates keep flowing); brand-new id → full custom.
        var res = LoadRawById(_globalPaths.RemoteSourceSeedsDirectory);
        var overlayPath = Path.Combine(dataDir, $"{config.Id}.json");
        if (res.TryGetValue(config.Id, out var baseEntry))
        {
            if (!HasRealDiff(baseEntry.Config, config)) // only "id" survives → no real override vs master
            {
                // DROP the overlay (don't write a no-op file) so the source is 'default' again — no
                // misleading "modified" chip after the user reverts every field / it matches master.
                if (File.Exists(overlayPath)) File.Delete(overlayPath);
                _logger.Info($"Remote source '{config.Id}': save matched master → overlay dropped (reverted to default)", "RemoteSourceStore");
            }
            else
            {
                File.WriteAllText(overlayPath, _resolver.Diff(baseEntry.Config, config)); // persist only the sparse diff
            }
        }
        else
        {
            File.WriteAllText(overlayPath, JsonSerializer.Serialize(config, JsonOptions)); // full custom source
        }

        _repository.Upsert(config); // mirror the EFFECTIVE config the caller saved
        _lastSyncedSignature = null; // force a re-sync on next read
        _logger.Info($"Saved remote source adapter: {config.Id}", "RemoteSourceStore");
        return config;
    }

    public bool Delete(string sourceId)
    {
        var dataDir = _globalPaths.RemoteSourcesDirectory;
        if (!Directory.Exists(dataDir)) return false;
        var removed = false;
        foreach (var file in Files(dataDir))
        {
            try
            {
                var config = JsonSerializer.Deserialize<RemoteSourceConfig>(File.ReadAllText(file), JsonOptions);
                if (config != null && string.Equals(config.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    removed = true;
                }
            }
            catch { /* skip unparseable */ }
        }
        if (removed)
        {
            _repository.Delete(sourceId);   // drop the SQLite mirror row too
            _lastSyncedSignature = null;    // next read re-adds the res base if this id is shipped
            _logger.Info($"Removed remote source overlay: {sourceId}", "RemoteSourceStore");
        }
        return removed;
    }

    /// <summary>Reject configs that could not work: bad id/baseUrl, missing lists, non-compiling regexes.</summary>
    private static void Validate(RemoteSourceConfig config)
    {
        void Fail(string reason) => throw new OperationException("REMOTE_SOURCE_INVALID", "reason", reason);

        if (string.IsNullOrWhiteSpace(config.Id) || !config.Id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            Fail("id must be letters/digits/-/_");
        if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            Fail("baseUrl must be an absolute http(s) URL");
        if (string.IsNullOrWhiteSpace(config.Name)) Fail("name is required");
        if (config.Lists.Count == 0 || config.Lists.Any(l => string.IsNullOrWhiteSpace(l.Id)))
            Fail("at least one list with an id is required");

        // JSON-API engines (gamebanana, woocommerce) drive off an API, not regex-over-HTML — the
        // regex/URL-template fields don't apply to them. Only the "http" (regex) engine requires them.
        var isRegexEngine = string.IsNullOrWhiteSpace(config.Engine)
            || string.Equals(config.Engine, "http", StringComparison.OrdinalIgnoreCase);
        var isGameBanana = !isRegexEngine;
        if (isRegexEngine && string.IsNullOrWhiteSpace(config.ListUrlFirstPage)) Fail("listUrlFirstPage is required");

        foreach (var (label, pattern) in new (string, string?)[]
        {
            ("cardPattern", config.CardPattern),
            ("detailTitlePattern", config.DetailTitlePattern),
            ("detailImagePattern", config.DetailImagePattern),
            ("downloadLinkPattern", config.DownloadLinkPattern),
            ("cardScopePattern", config.CardScopePattern),
            ("totalPagesPattern", config.TotalPagesPattern),
            ("entryIdPattern", config.EntryIdPattern),
            ("imageDatePattern", config.ImageDatePattern),
        })
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                if (!isGameBanana && label is "cardPattern" or "detailTitlePattern" or "downloadLinkPattern")
                    Fail($"{label} is required");
                continue;
            }
            try { _ = new Regex(pattern.Replace("{list}", "1")); }
            catch (Exception ex) { Fail($"{label} does not compile: {ex.Message}"); }
        }

        foreach (var rule in config.Resolvers)
        {
            try { _ = new Regex(rule.Match); }
            catch (Exception ex) { Fail($"resolver match does not compile: {ex.Message}"); }
        }
    }
}
