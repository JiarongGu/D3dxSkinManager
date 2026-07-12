using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Loads remote-library site adapters from {data}/remote-sources/*.json. Shipped adapters live in
/// {data}/remote-source-seeds/ (csproj Content, like the language files); the SEEDER copies any
/// shipped adapter whose id isn't configured yet — so new adapters arrive with app updates while
/// user-edited configs are never overwritten. Users (or future UI) add a site by dropping another
/// JSON in remote-sources/ — the directory is re-read on every listing, so no restart/watcher.
/// </summary>
public interface IRemoteSourceStore
{
    IReadOnlyList<RemoteSourceConfig> GetAll();
    RemoteSourceConfig GetById(string sourceId);

    /// <summary>Validate + persist an adapter as {id}.json (creates or overwrites by id).</summary>
    RemoteSourceConfig Save(RemoteSourceConfig config);

    /// <summary>Delete the config file whose adapter has this id. True when something was removed.</summary>
    bool Delete(string sourceId);
}

public class RemoteSourceStore : IRemoteSourceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IRemoteSourceRepository _repository;
    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    // The GLOBAL {data}/remote-sources/*.json files are the editable DEFINITION; the per-profile
    // RemoteSources table (via _repository) is the runtime store everything reads from. An mtime
    // signature (file paths + mtimes — cheap stat calls) detects when the JSON changed (user dropped/
    // edited a file, seeding, Save/Delete) → re-sync JSON into SQLite; unchanged → read SQLite directly.
    // Preserves the "drop a JSON, no restart" contract while driving reads from SQLite.
    private readonly object _cacheLock = new();
    private string? _lastSyncedSignature;

    public RemoteSourceStore(IRemoteSourceRepository repository, IGlobalPathService globalPaths, ILogHelper logger)
    {
        _repository = repository;
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public IReadOnlyList<RemoteSourceConfig> GetAll()
    {
        var dir = _globalPaths.RemoteSourcesDirectory;
        Directory.CreateDirectory(dir);
        lock (_cacheLock)
        {
            var signature = ComputeSignature(dir);
            if (signature != _lastSyncedSignature)
            {
                // JSON changed (or first access this session) → re-read the definition, seed shipped
                // adapters, then sync into the per-profile SQLite mirror. Reads come from SQLite after.
                var sources = LoadDirectory(dir);
                if (SeedMissing(dir, sources))
                {
                    sources = LoadDirectory(dir);
                    signature = ComputeSignature(dir); // seeding wrote files — re-stamp
                }
                _repository.Sync(sources);
                _lastSyncedSignature = signature;
            }
            return _repository.GetAll();
        }
    }

    /// <summary>Cheap change detector: adapter file set + last-write times (no reads/parses).</summary>
    private static string ComputeSignature(string dir) =>
        string.Join("|", Directory.GetFiles(dir, "*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"{f}:{File.GetLastWriteTimeUtc(f).Ticks}"));

    private List<RemoteSourceConfig> LoadDirectory(string dir)
    {
        var sources = new List<RemoteSourceConfig>();
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var config = JsonSerializer.Deserialize<RemoteSourceConfig>(File.ReadAllText(file), JsonOptions);
                if (config == null || string.IsNullOrWhiteSpace(config.Id) || string.IsNullOrWhiteSpace(config.BaseUrl))
                {
                    _logger.Warn($"Remote source config missing id/baseUrl, skipped: {Path.GetFileName(file)}", "RemoteSourceStore");
                    continue;
                }
                sources.Add(config);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to parse remote source {Path.GetFileName(file)}: {ex.Message}", "RemoteSourceStore");
            }
        }
        return sources;
    }

    public RemoteSourceConfig GetById(string sourceId)
    {
        var source = GetAll().FirstOrDefault(s => string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        return source ?? throw new OperationException("REMOTE_SOURCE_NOT_FOUND", "id", sourceId);
    }

    public RemoteSourceConfig Save(RemoteSourceConfig config)
    {
        Validate(config);
        var dir = _globalPaths.RemoteSourcesDirectory;
        Directory.CreateDirectory(dir);

        // One file per adapter id — remove any OTHER file that carries the same id (rename case).
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<RemoteSourceConfig>(File.ReadAllText(file), JsonOptions);
                if (existing != null && string.Equals(existing.Id, config.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileNameWithoutExtension(file), config.Id, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
            catch { /* unparseable neighbours are not our problem here */ }
        }

        File.WriteAllText(Path.Combine(dir, $"{config.Id}.json"), JsonSerializer.Serialize(config, JsonOptions));
        // JSON is the definition; mirror the edit into SQLite immediately + force a re-sync on next read.
        _repository.Upsert(config);
        _lastSyncedSignature = null;
        _logger.Info($"Saved remote source adapter: {config.Id}", "RemoteSourceStore");
        return config;
    }

    public bool Delete(string sourceId)
    {
        var dir = _globalPaths.RemoteSourcesDirectory;
        if (!Directory.Exists(dir)) return false;
        var removed = false;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
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
            _lastSyncedSignature = null;
            _logger.Info($"Deleted remote source adapter: {sourceId}", "RemoteSourceStore");
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

        // The "gamebanana" engine is a JSON API — it needs none of the HTML url-templates/regex fields
        // (the engine builds apiv11 URLs + parses JSON itself). Only validate the optional patterns it
        // may still carry (entryIdPattern, imageDatePattern) below; skip the http-only requirements.
        var isGameBanana = string.Equals(config.Engine, "gamebanana", StringComparison.OrdinalIgnoreCase);
        if (!isGameBanana && string.IsNullOrWhiteSpace(config.ListUrlFirstPage)) Fail("listUrlFirstPage is required");

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

    /// <summary>
    /// Copy every SHIPPED adapter ({data}/remote-source-seeds/*.json, ships with the app like the
    /// language files) whose id has no config yet. Existing configs are never overwritten, so user
    /// edits (e.g. a changed baseUrl) survive both re-runs and app updates.
    /// </summary>
    private bool SeedMissing(string dir, List<RemoteSourceConfig> existing)
    {
        var seedsDir = _globalPaths.RemoteSourceSeedsDirectory;
        if (!Directory.Exists(seedsDir)) return false;

        var known = new HashSet<string>(existing.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
        var seeded = false;
        foreach (var seedFile in Directory.GetFiles(seedsDir, "*.json"))
        {
            try
            {
                var config = JsonSerializer.Deserialize<RemoteSourceConfig>(File.ReadAllText(seedFile), JsonOptions);
                if (config == null || string.IsNullOrWhiteSpace(config.Id)) continue;

                // ADDITIVE upgrade: an existing config missing a field a newer seed provides gets
                // just that field filled in (never overwrites values the user set).
                var current = existing.FirstOrDefault(s => string.Equals(s.Id, config.Id, StringComparison.OrdinalIgnoreCase));
                if (current != null)
                {
                    var upgraded = new List<string>();
                    if (string.IsNullOrWhiteSpace(current.CardScopePattern) && !string.IsNullOrWhiteSpace(config.CardScopePattern))
                    {
                        current.CardScopePattern = config.CardScopePattern;
                        upgraded.Add("cardScopePattern");
                    }
                    if (string.IsNullOrWhiteSpace(current.TitleTagPattern) && !string.IsNullOrWhiteSpace(config.TitleTagPattern))
                    {
                        current.TitleTagPattern = config.TitleTagPattern;
                        upgraded.Add("titleTagPattern");
                    }
                    if (upgraded.Count > 0)
                    {
                        File.WriteAllText(Path.Combine(dir, $"{current.Id}.json"), JsonSerializer.Serialize(current, JsonOptions));
                        seeded = true;
                        _logger.Info($"Upgraded remote source adapter {current.Id}: added {string.Join(", ", upgraded)}", "RemoteSourceStore");
                    }
                    continue;
                }
                if (known.Contains(config.Id)) continue;

                var target = Path.Combine(dir, Path.GetFileName(seedFile));
                if (File.Exists(target)) continue; // same file name but unparseable/other id — don't clobber
                File.Copy(seedFile, target);
                seeded = true;
                _logger.Info($"Seeded remote source adapter: {config.Id}", "RemoteSourceStore");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to seed remote source {Path.GetFileName(seedFile)}: {ex.Message}", "RemoteSourceStore");
            }
        }
        return seeded;
    }
}
