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

    /// <summary>An example adapter JSON (the shipped huihui seed) for the "add your own" editor.</summary>
    string GetTemplateJson();
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

    private readonly IGlobalPathService _globalPaths;
    private readonly ILogHelper _logger;

    public RemoteSourceStore(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _logger = logger;
    }

    public IReadOnlyList<RemoteSourceConfig> GetAll()
    {
        var dir = _globalPaths.RemoteSourcesDirectory;
        Directory.CreateDirectory(dir);
        var sources = LoadDirectory(dir);
        if (SeedMissing(dir, sources)) sources = LoadDirectory(dir);
        return sources;
    }

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
        if (removed) _logger.Info($"Deleted remote source adapter: {sourceId}", "RemoteSourceStore");
        return removed;
    }

    public string GetTemplateJson()
    {
        var seedsDir = _globalPaths.RemoteSourceSeedsDirectory;
        if (Directory.Exists(seedsDir))
        {
            var seed = Directory.GetFiles(seedsDir, "*.json").OrderBy(p => p).FirstOrDefault();
            if (seed != null) return File.ReadAllText(seed);
        }
        // Minimal skeleton when no seeds ship (shouldn't happen in a real install).
        return JsonSerializer.Serialize(new RemoteSourceConfig
        {
            Id = "mysite",
            Name = "My Site",
            BaseUrl = "https://example.com",
            Lists = [new RemoteListConfig { Id = "1", Name = "Game" }],
            ListUrlFirstPage = "/list/{list}/",
            ListUrlTemplate = "/list/{list}/page/{page}/",
            CardPattern = "<a[^>]+href=\"(?<url>[^\"]+)\"[^>]*><img[^>]+src=\"(?<image>[^\"]+)\"[^>]*alt=\"(?<title>[^\"]*)\"",
            DetailTitlePattern = "<h1[^>]*>(?<title>[\\s\\S]*?)</h1>",
            DetailImagePattern = "<img[^>]+src=\"(?<image>[^\"]+)\"",
            DownloadLinkPattern = "<a[^>]+href=\"(?<url>https?://[^\"]+)\"",
            Resolvers = [new RemoteResolverRule { Match = "\\.(zip|7z|rar)($|\\?)", Type = "direct", Name = "Direct" }],
        }, JsonOptions);
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
        if (string.IsNullOrWhiteSpace(config.ListUrlFirstPage)) Fail("listUrlFirstPage is required");

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
                if (label is "cardPattern" or "detailTitlePattern" or "downloadLinkPattern")
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
                    if (string.IsNullOrWhiteSpace(current.CardScopePattern) && !string.IsNullOrWhiteSpace(config.CardScopePattern))
                    {
                        current.CardScopePattern = config.CardScopePattern;
                        File.WriteAllText(Path.Combine(dir, $"{current.Id}.json"), JsonSerializer.Serialize(current, JsonOptions));
                        seeded = true;
                        _logger.Info($"Upgraded remote source adapter {current.Id}: added cardScopePattern", "RemoteSourceStore");
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
