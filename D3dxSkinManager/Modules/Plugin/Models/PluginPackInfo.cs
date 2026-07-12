namespace D3dxSkinManager.Modules.Plugin.Models;

/// <summary>
/// One available OFFICIAL plugin pack, from the plugin repo's public <c>plugins-manifest.json</c>. The app
/// has NO hard-coded plugin list — Settings → 插件 shows these + offers install/update. Serialized camelCase.
/// </summary>
public class PluginPackInfo
{
    /// <summary>Pack id (manifest id, e.g. "content-veil-ai") — the install/download key.</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    /// <summary>Release asset name — the install contract with the release workflow.</summary>
    public string Asset { get; set; } = string.Empty;

    /// <summary>SDK contract version the pack was built against (compatibility gate).</summary>
    public string SdkContractVersion { get; set; } = string.Empty;

    /// <summary>The pack's SDK contract major matches the host's — safe to install.</summary>
    public bool Compatible { get; set; } = true;

    /// <summary>An instance of this pack is already installed + registered in this profile.</summary>
    public bool Installed { get; set; }
}
