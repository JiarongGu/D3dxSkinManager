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
/// Downloads OFFICIAL plugin packs from this repo's GitHub releases (same trust model as the app
/// updater: the resolved asset URL must live under the official releases prefix, never an
/// arbitrary host) and installs them into {profile}/plugins/{packId}/. Newly installed dlls are
/// loaded + initialized at runtime — no restart needed for a fresh install (REMOVING a loaded
/// pack still needs one; assemblies can't unload).
/// </summary>
public interface IPluginInstallService
{
    /// <summary>Fire-and-forget pack download+install (ProcessRegistry progress). Throws
    /// synchronously only for an unknown pack id.</summary>
    void StartPackInstall(string packId);

    /// <summary>Update status for each INSTALLED official pack (installed version vs the latest
    /// release's advertised version). Network-failure tolerant — returns an empty list rather than
    /// throwing, so the UI simply shows no update badges when offline / no release exists.</summary>
    Task<IReadOnlyList<PluginUpdateInfo>> CheckUpdatesAsync();
}

public class PluginInstallService : IPluginInstallService
{
    private const string ReleaseApi = "https://api.github.com/repos/JiarongGu/D3dxSkinManager/releases/latest";
    public const string ReleaseDownloadPrefix = "https://github.com/JiarongGu/D3dxSkinManager/releases/download/";

    // Official packs: pack id → the release asset it ships in.
    private static readonly Dictionary<string, string> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["content-veil-ai"] = "ContentVeil-AI-Plugin.zip",
    };

    // Public plugin manifest attached to each release (id/name/description/version/asset).
    private const string PublicManifestAsset = "plugins-manifest.json";

    private readonly IDownloadService _downloads;
    private readonly IProcessRegistry _processRegistry;
    private readonly IProfilePathService _profilePaths;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginRegistry _registry;
    private readonly ILogHelper _logger;

    public PluginInstallService(
        IDownloadService downloads,
        IProcessRegistry processRegistry,
        IProfilePathService profilePaths,
        IPluginLoader pluginLoader,
        IPluginRegistry registry,
        ILogHelper logger)
    {
        _downloads = downloads;
        _processRegistry = processRegistry;
        _profilePaths = profilePaths;
        _pluginLoader = pluginLoader;
        _registry = registry;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginUpdateInfo>> CheckUpdatesAsync()
    {
        var installed = _registry.GetAllEntries();
        if (installed.Count == 0) return Array.Empty<PluginUpdateInfo>();

        Dictionary<string, string> available;
        try
        {
            available = await FetchAvailableVersionsAsync().ConfigureAwait(false); // packId → version
        }
        catch (Exception ex)
        {
            // Offline / no release / no manifest asset — no badges, not an error the user must see.
            _logger.Info($"[PluginInstall] update check skipped: {ex.Message}", "PluginInstall");
            return Array.Empty<PluginUpdateInfo>();
        }

        var result = new List<PluginUpdateInfo>();
        foreach (var entry in installed)
        {
            var pluginId = entry.Plugin.Id;
            // pack id = plugin id minus the "d3dx." prefix (the install/download convention).
            var packId = pluginId.StartsWith("d3dx.", StringComparison.OrdinalIgnoreCase)
                ? pluginId["d3dx.".Length..]
                : pluginId;
            if (!Catalog.ContainsKey(packId)) continue;                 // OFFICIAL packs only
            if (!available.TryGetValue(packId, out var availableVersion)) continue;

            var installedVersion = entry.Plugin.Version;
            result.Add(new PluginUpdateInfo
            {
                PluginId = pluginId,
                PackId = packId,
                InstalledVersion = installedVersion,
                AvailableVersion = availableVersion,
                UpdateAvailable = IsNewer(availableVersion, installedVersion),
            });
        }
        return result;
    }

    /// <summary>packId → version from the latest release's public <c>plugins-manifest.json</c> asset
    /// (trusted only when its URL is under the official releases prefix — same model as install).</summary>
    private async Task<Dictionary<string, string>> FetchAvailableVersionsAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var json = await _downloads.GetStringAsync(ReleaseApi).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        string? manifestUrl = null;
        if (doc.RootElement.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (string.Equals(asset.GetProperty("name").GetString(), PublicManifestAsset, StringComparison.OrdinalIgnoreCase))
                {
                    manifestUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }
        if (manifestUrl == null || !manifestUrl.StartsWith(ReleaseDownloadPrefix, StringComparison.OrdinalIgnoreCase))
            return map;

        var manifestJson = await _downloads.GetStringAsync(manifestUrl).ConfigureAwait(false);
        using var mdoc = JsonDocument.Parse(manifestJson);
        if (mdoc.RootElement.TryGetProperty("plugins", out var plugins))
        {
            foreach (var p in plugins.EnumerateArray())
            {
                var id = p.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var ver = p.TryGetProperty("version", out var vEl) ? vEl.GetString() : null;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(ver)) map[id] = ver;
            }
        }
        return map;
    }

    /// <summary>True when <paramref name="available"/> is a higher version than
    /// <paramref name="installed"/> (numeric compare; falls back to any string difference).</summary>
    private static bool IsNewer(string available, string installed)
    {
        if (Version.TryParse(available, out var av) && Version.TryParse(installed, out var iv))
            return av > iv;
        return !string.Equals(available, installed, StringComparison.OrdinalIgnoreCase);
    }

    public void StartPackInstall(string packId)
    {
        if (!Catalog.TryGetValue(packId, out var assetName))
            throw new OperationException("PLUGIN_PACK_UNKNOWN", "packId", packId);

        _ = Task.Run(async () =>
        {
            var procId = _processRegistry.Start(ProcessType.Download,
                $"Downloading plugin pack: {packId}", titleKey: "process.pluginDownload", titleArg: packId);
            try
            {
                // Resolve the latest release asset (same pattern as the XXMI installer assist).
                var json = await _downloads.GetStringAsync(ReleaseApi).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                string? url = null;
                long size = 0;
                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString();
                        if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                        {
                            url = asset.GetProperty("browser_download_url").GetString();
                            size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                            break;
                        }
                    }
                }
                if (url == null || !url.StartsWith(ReleaseDownloadPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new OperationException("PLUGIN_PACK_NOT_AVAILABLE", "packId", packId);

                _processRegistry.Report(procId, 5, detailKey: "process.stage.downloading");
                var zipPath = Path.Combine(_downloads.ManagedDirectory, assetName);
                var progress = new Progress<DownloadProgress>(p =>
                {
                    if (p.Percent is { } pc) _processRegistry.Report(procId, 5 + (int)(pc * 0.75));
                });
                await _downloads.DownloadAsync(new DownloadRequest
                {
                    Url = url,
                    DestinationPath = zipPath,
                }, progress).ConfigureAwait(false);

                // A fresh install extracts into the live pack dir and loads immediately. An UPDATE
                // (the pack is already installed → its dll is LOADED + locked) can't overwrite in place,
                // so it extracts into {plugins}/.pending/{packId} and PluginLoader swaps it in on the
                // next launch (see PluginLoader.ApplyPendingUpdates).
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
                    // Staged — applies on restart (assemblies can't unload; the live dll is locked).
                    _processRegistry.Report(procId, 100, detailKey: "process.stage.restartRequired");
                    _processRegistry.Complete(procId);
                    _logger.Info($"[PluginInstall] Pack '{packId}' update staged — applies on restart", "PluginInstall");
                }
                else
                {
                    // Load + init the freshly installed plugin(s) — live, no restart.
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
}
