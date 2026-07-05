using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Loads remote-library site adapters from {data}/remote-sources/*.json and seeds the built-in
/// huihui adapter on first run. Users (or future UI) add a site by dropping another JSON there —
/// the directory is re-read on every listing, so no restart/watcher is needed.
/// </summary>
public interface IRemoteSourceStore
{
    IReadOnlyList<RemoteSourceConfig> GetAll();
    RemoteSourceConfig GetById(string sourceId);
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
        SeedIfEmpty(dir);

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

    /// <summary>Write the built-in adapter(s) when the directory has no configs yet.</summary>
    private void SeedIfEmpty(string dir)
    {
        if (Directory.EnumerateFiles(dir, "*.json").Any()) return;

        try
        {
            var path = Path.Combine(dir, "huihui.json");
            File.WriteAllText(path, JsonSerializer.Serialize(BuildHuihuiSeed(), JsonOptions));
            _logger.Info("Seeded built-in remote source: huihui", "RemoteSourceStore");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to seed remote sources: {ex.Message}", "RemoteSourceStore");
        }
    }

    /// <summary>
    /// The built-in huihui168.org adapter — patterns verified against the live site 2026-07-05
    /// (see .claude/rules/remote-library.md). Users can edit the JSON (e.g. baseUrl when the site
    /// moves hosts) — this seed is only written when NO config exists.
    /// </summary>
    public static RemoteSourceConfig BuildHuihuiSeed() => new()
    {
        Id = "huihui",
        Name = "Hui站",
        BaseUrl = "https://huihui168.org",
        Engine = "http",
        Lists =
        [
            new RemoteListConfig { Id = "2", Name = "绝区零" },
            new RemoteListConfig { Id = "1", Name = "鸣潮" },
            new RemoteListConfig { Id = "3", Name = "星穹铁道" },
            new RemoteListConfig { Id = "4", Name = "终末地" },
        ],
        ListUrlFirstPage = "/?list_{list}/",
        ListUrlTemplate = "/?list_{list}_{page}/",
        SearchUrlTemplate = "/?keyword={query}",
        CardPattern = "<a[^>]+href=\"(?<url>/\\?news_[^\"]+)\"[^>]*>[\\s\\S]{0,600}?<img[^>]+src=\"(?<image>[^\"]+)\"[^>]*alt=\"(?<title>[^\"]*)\"",
        TotalPagesPattern = "href=\"/\\?list_{list}_(?<pages>\\d+)/\"",
        DetailTitlePattern = "<h1[^>]*>(?<title>[\\s\\S]*?)</h1>",
        DetailImagePattern = "<img[^>]+src=\"(?<image>/static/upload/[^\"]+)\"",
        DownloadLinkPattern = "<a[^>]+href=\"(?<url>https?://[^\"]+)\"",
        Resolvers =
        [
            new RemoteResolverRule { Match = "^https?://cloudreve\\.", Type = "cloudreve", Name = "Hui盘" },
            new RemoteResolverRule { Match = "^https?://pan\\.quark\\.cn/", Type = "external", Name = "夸克" },
        ],
    };
}
