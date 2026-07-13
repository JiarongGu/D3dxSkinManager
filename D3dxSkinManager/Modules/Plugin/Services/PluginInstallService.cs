using System.IO.Compression;
using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Plugin.Models;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// Installs OFFICIAL plugin packs from the PLUGIN repo's GitHub releases. There is NO hard-coded catalog:
/// the available packs (id/name/description/version/asset/sdkContractVersion) come from the latest
/// release's public <c>plugins-manifest.json</c>, so the app never ships a plugin list. Same trust model as
/// the updater — a resolved asset URL must live under the official releases prefix. Installs into
/// {profile}/plugins/{packId}/ (fresh install loads live; an update stages + applies on restart).
/// </summary>
public interface IPluginInstallService
{
    /// <summary>The available official packs (from the plugin repo manifest), each flagged with
    /// compatibility + whether it's already installed. Network-failure tolerant (empty list).</summary>
    Task<IReadOnlyList<PluginPackInfo>> GetAvailablePacksAsync();

    /// <summary>Fire-and-forget pack download+install (ProcessRegistry progress).</summary>
    void StartPackInstall(string packId);

    /// <summary>Update status for each INSTALLED official pack (installed vs advertised version).
    /// Network-failure tolerant — empty list rather than throwing.</summary>
    Task<IReadOnlyList<PluginUpdateInfo>> CheckUpdatesAsync();

    /// <summary>Packs installed on disk that FAILED to load in the last load (contract mismatch after an
    /// app update, missing dependency, …), each enriched from the catalog with whether a COMPATIBLE build
    /// exists to fix it. Empty when everything loaded. Network-failure tolerant (returns the raw failures
    /// unenriched).</summary>
    Task<IReadOnlyList<PluginLoadFailure>> GetLoadFailuresAsync();

    /// <summary>Pack ids whose update is STAGED in <c>{plugins}/.pending</c> awaiting a restart to apply
    /// (mirrors the app-update "pending" state). Empty when nothing is staged.</summary>
    IReadOnlyList<string> GetPendingUpdates();
}

public class PluginInstallService : IPluginInstallService
{
    // The PLUGIN repo release LOCATIONS (API + trusted download prefix + manifest asset name) come from
    // IReleaseEndpointConfig — shipped defaults, overridable via data/settings/endpoints.json so the
    // location can move without a code change. Read offline; a failed FETCH is already non-fatal (empty catalog).

    private readonly IDownloadService _downloads;
    private readonly IProcessRegistry _processRegistry;
    private readonly IProfilePathService _profilePaths;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginRegistry _registry;
    private readonly IReleaseEndpointConfig _endpoints;
    private readonly ILogHelper _logger;

    public PluginInstallService(
        IDownloadService downloads,
        IProcessRegistry processRegistry,
        IProfilePathService profilePaths,
        IPluginLoader pluginLoader,
        IPluginRegistry registry,
        IReleaseEndpointConfig endpoints,
        ILogHelper logger)
    {
        _downloads = downloads;
        _processRegistry = processRegistry;
        _profilePaths = profilePaths;
        _pluginLoader = pluginLoader;
        _registry = registry;
        _endpoints = endpoints;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginPackInfo>> GetAvailablePacksAsync()
    {
        try
        {
            var (packs, _) = await FetchCatalogAsync().ConfigureAwait(false);
            var installedPackIds = InstalledPackIds();
            foreach (var p in packs) p.Installed = installedPackIds.Contains(p.Id);
            return packs;
        }
        catch (Exception ex)
        {
            _logger.Info($"[PluginInstall] catalog fetch skipped: {ex.Message}", "PluginInstall");
            return Array.Empty<PluginPackInfo>();
        }
    }

    public async Task<IReadOnlyList<PluginUpdateInfo>> CheckUpdatesAsync()
    {
        var installed = _registry.GetAllEntries();
        if (installed.Count == 0) return Array.Empty<PluginUpdateInfo>();

        List<PluginPackInfo> packs;
        try
        {
            (packs, _) = await FetchCatalogAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Info($"[PluginInstall] update check skipped: {ex.Message}", "PluginInstall");
            return Array.Empty<PluginUpdateInfo>();
        }

        var byPackId = packs.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<PluginUpdateInfo>();
        foreach (var entry in installed)
        {
            var pluginId = entry.Plugin.Id;
            var packId = ToPackId(pluginId);
            if (!byPackId.TryGetValue(packId, out var pack)) continue; // only packs the manifest knows

            result.Add(new PluginUpdateInfo
            {
                PluginId = pluginId,
                PackId = packId,
                InstalledVersion = entry.Plugin.Version,
                AvailableVersion = pack.Version,
                UpdateAvailable = IsNewer(pack.Version, entry.Plugin.Version),
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<PluginLoadFailure>> GetLoadFailuresAsync()
    {
        var failures = _pluginLoader.LoadFailures;
        if (failures.Count == 0) return Array.Empty<PluginLoadFailure>();

        // Enrich each failure from the catalog: is there a COMPATIBLE build that fixes it? A failed pack
        // never registered, so CheckUpdatesAsync (which walks the registry) misses it — match by pack folder
        // id instead. Network-tolerant: a failed fetch leaves the failures unenriched (Name null, no update).
        var byPackId = new Dictionary<string, PluginPackInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var (packs, _) = await FetchCatalogAsync().ConfigureAwait(false);
            byPackId = packs.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.Info($"[PluginInstall] load-failure enrichment skipped: {ex.Message}", "PluginInstall");
        }

        return failures.Select(f =>
        {
            byPackId.TryGetValue(f.PackId, out var pack);
            return new PluginLoadFailure
            {
                PackId = f.PackId,
                DllName = f.DllName,
                Reason = f.Reason,
                Name = pack?.Name,
                UpdateAvailable = pack is { Compatible: true },
                AvailableVersion = pack is { Compatible: true } ? pack.Version : null,
            };
        }).ToList();
    }

    public IReadOnlyList<string> GetPendingUpdates()
    {
        var pendingRoot = Path.Combine(_profilePaths.PluginsDirectory, PluginLoader.PendingDirName);
        if (!Directory.Exists(pendingRoot)) return Array.Empty<string>();
        return Directory.GetDirectories(pendingRoot)
            .Select(d => Path.GetFileName(d))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList()!;
    }

    public void StartPackInstall(string packId)
    {
        _ = Task.Run(async () =>
        {
            var procId = _processRegistry.Start(ProcessType.Download,
                $"Downloading plugin pack: {packId}", titleKey: "process.pluginDownload", titleArg: packId);
            try
            {
                var (packs, assets) = await FetchCatalogAsync().ConfigureAwait(false);
                var pack = packs.FirstOrDefault(p => string.Equals(p.Id, packId, StringComparison.OrdinalIgnoreCase))
                           ?? throw new OperationException("PLUGIN_PACK_UNKNOWN", "packId", packId);
                if (!pack.Compatible)
                    throw new OperationException("PLUGIN_PACK_INCOMPATIBLE", "packId", packId);
                if (!assets.TryGetValue(pack.Asset, out var asset) ||
                    !asset.Url.StartsWith(_endpoints.PluginDownloadPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new OperationException("PLUGIN_PACK_NOT_AVAILABLE", "packId", packId);

                _processRegistry.Report(procId, 5, detailKey: "process.stage.downloading");
                var zipPath = Path.Combine(_downloads.ManagedDirectory, pack.Asset);
                var progress = new Progress<DownloadProgress>(p =>
                {
                    if (p.Percent is { } pc) _processRegistry.Report(procId, 5 + (int)(pc * 0.75));
                });
                await _downloads.DownloadAsync(new DownloadRequest { Url = asset.Url, DestinationPath = zipPath }, progress)
                    .ConfigureAwait(false);

                // Fresh install → extract into the live pack dir + load now. UPDATE (pack already installed →
                // its dll is LOADED + locked) → extract into {plugins}/.pending/{packId}; PluginLoader swaps
                // it in on next launch (ApplyPendingUpdates). Assemblies can't unload, so updates apply on restart.
                _processRegistry.Report(procId, 85, detailKey: "process.stage.extracting");
                var liveTarget = Path.Combine(_profilePaths.PluginsDirectory, packId);
                var isUpdate = Directory.Exists(liveTarget) && Directory.EnumerateFiles(liveTarget, "*.dll").Any();
                var extractTarget = isUpdate
                    ? Path.Combine(_profilePaths.PluginsDirectory, PluginLoader.PendingDirName, packId)
                    : liveTarget;
                if (Directory.Exists(extractTarget)) Directory.Delete(extractTarget, recursive: true);
                Directory.CreateDirectory(extractTarget);
                ZipFile.ExtractToDirectory(zipPath, extractTarget, overwriteFiles: true);
                try { File.Delete(zipPath); } catch { /* managed dir self-cleans anyway */ }

                if (isUpdate)
                {
                    _processRegistry.Report(procId, 100, detailKey: "process.stage.restartRequired");
                    _processRegistry.Complete(procId);
                    _logger.Info($"[PluginInstall] Pack '{packId}' update staged — applies on restart", "PluginInstall");
                }
                else
                {
                    _processRegistry.Report(procId, 95);
                    await _pluginLoader.LoadPluginsAsync().ConfigureAwait(false);
                    await _pluginLoader.InitPluginsAsync().ConfigureAwait(false);
                    _processRegistry.Complete(procId);
                    _logger.Info($"[PluginInstall] Pack '{packId}' installed + loaded", "PluginInstall");
                }
            }
            catch (Exception ex)
            {
                _processRegistry.Fail(procId, ex.Message);
                _logger.Error($"[PluginInstall] Pack '{packId}' install failed: {ex.Message}", "PluginInstall", ex);
            }
        });
    }

    // ---- manifest catalog --------------------------------------------------------------------

    /// <summary>Fetch the latest release once → parse the public plugins-manifest.json (the pack catalog)
    /// AND the release asset map (name → download url/size). The manifest URL + asset URLs are trusted only
    /// under the official releases prefix.</summary>
    private async Task<(List<PluginPackInfo> Packs, Dictionary<string, (string Url, long Size)> Assets)> FetchCatalogAsync()
    {
        var json = await _downloads.GetStringAsync(_endpoints.PluginReleaseApi).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var assets = new Dictionary<string, (string, long)>(StringComparer.OrdinalIgnoreCase);
        string? manifestUrl = null;
        if (doc.RootElement.TryGetProperty("assets", out var assetsEl))
        {
            foreach (var a in assetsEl.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                var size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                if (name == null || url == null) continue;
                assets[name] = (url, size);
                if (string.Equals(name, _endpoints.PluginManifestAsset, StringComparison.OrdinalIgnoreCase)) manifestUrl = url;
            }
        }

        var packs = new List<PluginPackInfo>();
        if (manifestUrl != null && manifestUrl.StartsWith(_endpoints.PluginDownloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var manifestJson = await _downloads.GetStringAsync(manifestUrl).ConfigureAwait(false);
            using var mdoc = JsonDocument.Parse(manifestJson);
            if (mdoc.RootElement.TryGetProperty("plugins", out var plugins))
            {
                foreach (var p in plugins.EnumerateArray())
                {
                    var id = Str(p, "id");
                    if (string.IsNullOrEmpty(id)) continue;
                    var contract = Str(p, "sdkContractVersion");
                    packs.Add(new PluginPackInfo
                    {
                        Id = id!,
                        Name = Str(p, "name") ?? id!,
                        Description = Str(p, "description") ?? string.Empty,
                        Version = Str(p, "version") ?? string.Empty,
                        Asset = Str(p, "asset") ?? string.Empty,
                        SdkContractVersion = contract ?? string.Empty,
                        Compatible = PluginContract.IsCompatible(contract),
                    });
                }
            }
        }
        return (packs, assets);
    }

    private static string? Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>pack id = plugin id minus the "d3dx." prefix (the install/download convention).</summary>
    private static string ToPackId(string pluginId) =>
        pluginId.StartsWith("d3dx.", StringComparison.OrdinalIgnoreCase) ? pluginId["d3dx.".Length..] : pluginId;

    private HashSet<string> InstalledPackIds() =>
        _registry.GetAllEntries().Select(e => ToPackId(e.Plugin.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool IsNewer(string available, string installed)
    {
        if (Version.TryParse(available, out var av) && Version.TryParse(installed, out var iv))
            return av > iv;
        return !string.Equals(available, installed, StringComparison.OrdinalIgnoreCase);
    }
}
