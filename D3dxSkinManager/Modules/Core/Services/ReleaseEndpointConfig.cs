using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Resolved release/update LOCATIONS for the app self-updater (<c>UpdateService</c>) and the plugin
/// catalog (<c>PluginInstallService</c>). Layered, highest priority first:
///   1. <c>data/settings/endpoints.json</c> — an operator OVERRIDE (per-field; not shipped, not in the UI).
///   2. <c>res/endpoints.json</c> — the SHIPPED, repo-managed default (released alongside res/remote-sources).
///   3. the code constants below — last-resort fallback (res file missing/corrupt).
/// So the release location can move by editing the repo-managed res file (or an operator's override) with
/// NO recompile of the consumers. All LOCAL reads, never the network — a missing/partial/corrupt file just
/// falls to the next layer, so the app starts fully OFFLINE. NOT shown in the settings UI.
/// See .claude/knowledge/release-endpoints-config.md.
/// </summary>
public interface IReleaseEndpointConfig
{
    string AppReleaseApi { get; }
    string AppDownloadBase { get; }
    string PluginReleaseApi { get; }
    string PluginDownloadPrefix { get; }
    string PluginManifestAsset { get; }
}

public sealed class ReleaseEndpointConfig : IReleaseEndpointConfig
{
    // Last-resort fallback = the shipped res/endpoints.json values (kept in sync). Only used if that
    // file is missing/corrupt, so the app is never left without a usable location.
    public const string DefaultAppReleaseApi = "https://api.github.com/repos/JiarongGu/D3dxSkinManager/releases/latest";
    public const string DefaultAppDownloadBase = "https://github.com/JiarongGu/D3dxSkinManager/releases/latest/download/";
    public const string DefaultPluginReleaseApi = "https://api.github.com/repos/JiarongGu/D3dxSkinManager.Plugins/releases/latest";
    public const string DefaultPluginDownloadPrefix = "https://github.com/JiarongGu/D3dxSkinManager.Plugins/releases/download/";
    public const string DefaultPluginManifestAsset = "plugins-manifest.json";

    public const string FileName = "endpoints.json";

    private readonly EndpointConfig _resolved;

    /// <summary>DI constructor — layers the operator override (data/settings) over the shipped default
    /// (res) over the code fallback. Both are LOCAL reads at startup (offline-safe).</summary>
    [ActivatorUtilitiesConstructor]
    public ReleaseEndpointConfig(IGlobalPathService globalPaths, ILogHelper logger)
        : this(
            LoadJson(CombineOrNull(globalPaths.GlobalSettingsDirectory, FileName), logger), // 1. operator override
            LoadJson(CombineOrNull(globalPaths.ResourcesPath, FileName), logger))           // 2. shipped default (res)
    { }

    private static string? CombineOrNull(string? dir, string file) =>
        string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, file);

    /// <summary>Testable constructor — override layers, HIGHEST priority first. A null layer or blank
    /// field falls through to the next layer, then the code constant. No file, no network.</summary>
    public ReleaseEndpointConfig(params EndpointConfig?[] layers)
    {
        _resolved = Resolve(layers);
    }

    public string AppReleaseApi => _resolved.AppReleaseApi!;
    public string AppDownloadBase => _resolved.AppDownloadBase!;
    public string PluginReleaseApi => _resolved.PluginReleaseApi!;
    public string PluginDownloadPrefix => _resolved.PluginDownloadPrefix!;
    public string PluginManifestAsset => _resolved.PluginManifestAsset!;

    /// <summary>Resolve each field from the first layer that sets it (highest priority first), else the
    /// shipped code constant. Pure — the testable core.</summary>
    public static EndpointConfig Resolve(params EndpointConfig?[] layers) => new()
    {
        AppReleaseApi = Pick(l => l.AppReleaseApi, DefaultAppReleaseApi, layers),
        AppDownloadBase = Pick(l => l.AppDownloadBase, DefaultAppDownloadBase, layers),
        PluginReleaseApi = Pick(l => l.PluginReleaseApi, DefaultPluginReleaseApi, layers),
        PluginDownloadPrefix = Pick(l => l.PluginDownloadPrefix, DefaultPluginDownloadPrefix, layers),
        PluginManifestAsset = Pick(l => l.PluginManifestAsset, DefaultPluginManifestAsset, layers),
    };

    private static string Pick(Func<EndpointConfig, string?> select, string fallback, EndpointConfig?[] layers)
    {
        foreach (var layer in layers)
        {
            if (layer is null) continue;
            var value = select(layer);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return fallback;
    }

    private static EndpointConfig? LoadJson(string? path, ILogHelper logger)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var cfg = JsonSerializer.Deserialize<EndpointConfig>(
                File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            logger.Info($"[Endpoints] loaded release locations from {path}", "Endpoints");
            return cfg;
        }
        catch (Exception ex)
        {
            // Corrupt/unreadable → skip this layer. NEVER fatal (offline-first, config is best-effort).
            logger.Info($"[Endpoints] skipped {path} ({ex.Message}) — using next layer/default", "Endpoints");
            return null;
        }
    }
}
