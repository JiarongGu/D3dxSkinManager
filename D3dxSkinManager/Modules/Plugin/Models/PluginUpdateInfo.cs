namespace D3dxSkinManager.Modules.Plugin.Models;

/// <summary>
/// Update status for one INSTALLED official pack — the installed version vs the version advertised by
/// the latest release's public plugin manifest. Serialized camelCase to the frontend, which shows a
/// "vX available" badge + enables the Update button only when <see cref="UpdateAvailable"/> is true.
/// </summary>
public class PluginUpdateInfo
{
    /// <summary>The installed plugin id (e.g. "d3dx.content-veil-ai").</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>The pack id / manifest id (the install + download key, e.g. "content-veil-ai").</summary>
    public string PackId { get; set; } = string.Empty;

    public string InstalledVersion { get; set; } = string.Empty;
    public string AvailableVersion { get; set; } = string.Empty;

    /// <summary>The release advertises a NEWER version than the one installed.</summary>
    public bool UpdateAvailable { get; set; }
}
