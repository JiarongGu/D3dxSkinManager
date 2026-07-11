using System.IO.Compression;
using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

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

    private readonly IDownloadService _downloads;
    private readonly IProcessRegistry _processRegistry;
    private readonly IProfilePathService _profilePaths;
    private readonly IPluginLoader _pluginLoader;
    private readonly ILogHelper _logger;

    public PluginInstallService(
        IDownloadService downloads,
        IProcessRegistry processRegistry,
        IProfilePathService profilePaths,
        IPluginLoader pluginLoader,
        ILogHelper logger)
    {
        _downloads = downloads;
        _processRegistry = processRegistry;
        _profilePaths = profilePaths;
        _pluginLoader = pluginLoader;
        _logger = logger;
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

                // Extract into the profile's plugins dir; the pack owns its folder.
                _processRegistry.Report(procId, 85, detailKey: "process.stage.extracting");
                var target = Path.Combine(_profilePaths.PluginsDirectory, packId);
                Directory.CreateDirectory(target);
                ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
                try { File.Delete(zipPath); } catch { /* managed dir self-cleans anyway */ }

                // Load + init the freshly installed plugin(s) — live, no restart.
                _processRegistry.Report(procId, 95);
                await _pluginLoader.LoadPluginsAsync().ConfigureAwait(false);
                await _pluginLoader.InitPluginsAsync().ConfigureAwait(false);

                _processRegistry.Complete(procId);
                _logger.Info($"[PluginInstall] Pack '{packId}' installed + loaded", "PluginInstall");
            }
            catch (Exception ex)
            {
                _processRegistry.Fail(procId, ex.Message);
                _logger.Error($"[PluginInstall] Pack '{packId}' install failed: {ex.Message}", "PluginInstall", ex);
            }
        });
    }
}
