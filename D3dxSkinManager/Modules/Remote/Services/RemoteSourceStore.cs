using System.Text.Json;
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
                if (config == null || string.IsNullOrWhiteSpace(config.Id) || known.Contains(config.Id)) continue;

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
