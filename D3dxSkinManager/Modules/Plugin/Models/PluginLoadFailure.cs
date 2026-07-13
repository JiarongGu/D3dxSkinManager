namespace D3dxSkinManager.Modules.Plugin.Models;

/// <summary>
/// A plugin pack that is installed on disk but FAILED to load — almost always an SDK/contract mismatch
/// after an app update (the pack was built against an older Core contract and needs a newer build).
/// Because it produced no usable <c>IPlugin</c> it is NOT in <c>GET_ALL</c>; this surfaces it so the UI
/// can flag "requires update" and offer a download. Serialized camelCase to the frontend.
/// </summary>
public class PluginLoadFailure
{
    /// <summary>The pack folder name under <c>{profile}/plugins</c> — the download/update key.</summary>
    public string PackId { get; set; } = string.Empty;

    /// <summary>The dll that failed to load (for the detail line / logs).</summary>
    public string DllName { get; set; } = string.Empty;

    /// <summary>Human-readable reason (e.g. an SDK/contract mismatch).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Display name from the catalog when the pack is known there; else the pack id.</summary>
    public string? Name { get; set; }

    /// <summary>A COMPATIBLE newer build exists in the live catalog → the user can download it to fix this.</summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>The version the catalog offers (set only when <see cref="UpdateAvailable"/>).</summary>
    public string? AvailableVersion { get; set; }
}
