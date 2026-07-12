namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// OPTIONAL overrides for release/update LOCATIONS, read from <c>data/settings/endpoints.json</c>.
/// Any null/absent field keeps the shipped default (see <see cref="Services.ReleaseEndpointConfig"/>),
/// so the release location can move (repo rename, mirror) WITHOUT a code change. It is a LOCAL file,
/// read offline and never fetched — a missing/partial/corrupt file just falls back to defaults, so the
/// app always starts. NOT surfaced in the settings UI; edit the file directly.
/// </summary>
public sealed class EndpointConfig
{
    /// <summary>App self-update: GitHub "latest release" API.</summary>
    public string? AppReleaseApi { get; set; }

    /// <summary>App self-update: stable latest-release asset download base.</summary>
    public string? AppDownloadBase { get; set; }

    /// <summary>Plugin catalog: the plugin repo's latest-release API.</summary>
    public string? PluginReleaseApi { get; set; }

    /// <summary>Plugin catalog: the trusted download prefix (resolved asset URLs must start with this).</summary>
    public string? PluginDownloadPrefix { get; set; }

    /// <summary>Plugin catalog: the public manifest asset name attached to each release.</summary>
    public string? PluginManifestAsset { get; set; }
}
